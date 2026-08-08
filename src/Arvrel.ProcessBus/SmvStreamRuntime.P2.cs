#if ARIEC61850_SIBLING
using System.Buffers.Binary;
using System.Numerics;
using AR.Iec61850.Mms;
using AR.Iec61850.SampledValues;
using Arvrel.Protection;

namespace Arvrel.ProcessBus;

internal sealed class SmvStreamRuntime
{
    private const int RingCapacity = 16_384;
    private static readonly TimeSpan RecentIssueWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LiveStaleWindow = TimeSpan.FromMilliseconds(250);

    private readonly object _gate = new();
    private readonly ResearchProtectionEngine _protection;
    private readonly SampleRing _phaseA = new(RingCapacity);
    private readonly SampleRing _phaseB = new(RingCapacity);
    private readonly SampleRing _phaseC = new(RingCapacity);
    private readonly SampleRing _residual = new(RingCapacity);
    private readonly SampleRing _voltageA = new(RingCapacity);
    private readonly SampleRing _voltageB = new(RingCapacity);
    private readonly SampleRing _voltageC = new(RingCapacity);
    private readonly SampleRing _neutralVoltage = new(RingCapacity);
    private readonly Queue<string> _diagnostics = new();
    private SmvMeasurementContext _context;
    private DateTimeOffset? _firstSeen;
    private DateTimeOffset? _lastSeen;
    private DateTimeOffset? _lastSequenceIssue;
    private DateTimeOffset? _lastQualityIssue;
    private DateTimeOffset? _lastPayloadIssue;
    private DateTimeOffset? _lastConfigurationIssue;
    private ushort? _lastSampleCounter;
    private ushort _sampleCounter;
    private byte _sampleSynchronization;
    private int _samplesPerCycle = 80;
    private long _frameCount;
    private long _asduCount;
    private int _sequenceGaps;
    private int _duplicates;
    private int _outOfOrder;
    private int _invalidQuality;
    private bool _mappingResolved;
    private bool _voltageMappingResolved;
    private bool _neutralCurrentMappingResolved;
    private bool _neutralVoltageMappingResolved;
    private bool _scalingResolved;
    private bool _sclBound;
    private string _mappingSummary = "Awaiting payload";
    private string _scalingSummary = "Unresolved";
    private string _sclSummary = "No SCL binding";
    private string _sourceSummary = "Idle";
    private ProtectionSnapshot _protectionSnapshot;
    private MeasurementFrame _measurement;

    public SmvStreamRuntime(string key, SampledValuesFrame firstFrame, ProtectionSettings settings, SmvMeasurementContext context)
    {
        Key = key;
        AppId = firstFrame.AppId;
        Destination = firstFrame.Destination.ToString();
        VlanId = firstFrame.Vlan?.VlanId;
        SvId = firstFrame.Pdu.Asdus.FirstOrDefault()?.SvId ?? "unnamed";
        _context = context;
        _context.Validate();
        _protection = new ResearchProtectionEngine(settings);
        var timestamp = DateTimeOffset.UtcNow;
        _measurement = new MeasurementFrame(timestamp, 0, 0, 0, 0,
            new SmvTrustState(false, false, false, "INITIALIZING", "Waiting for a complete measurement window."));
        _protectionSnapshot = ProtectionSnapshot.Ready(timestamp);
    }

    public string Key { get; }
    public ushort AppId { get; }
    public string SvId { get; private set; }
    public string Destination { get; }
    public ushort? VlanId { get; }

    public void SetMeasurementContext(SmvMeasurementContext context)
    {
        context.Validate();
        lock (_gate)
            _context = context;
    }

    public void ResetProtection()
    {
        lock (_gate)
        {
            _protection.Reset();
            _protectionSnapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
        }
    }

    /// <summary>
    /// Records a frame rejected by the transactional ingress gate. No payload value is
    /// added to RMS, waveform or phasor buffers, and no protection timer is evaluated
    /// from the stale previous measurement. Telemetry and the trust projection are
    /// updated atomically while the engine's time anchor advances to the capture edge.
    /// </summary>
    public void ObserveIngressRejection(
        DateTimeOffset timestamp,
        SmvIngressDecision decision,
        ushort sampleCount,
        int asduCount,
        bool replay)
    {
        if (decision is not (SmvIngressDecision.DropDuplicate or SmvIngressDecision.DropOutOfOrder))
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Only rejected ingress decisions are accepted.");

        lock (_gate)
        {
            _firstSeen ??= timestamp;
            _lastSeen = timestamp;
            _frameCount++;
            _asduCount += Math.Max(1, asduCount);
            _sampleCounter = sampleCount;
            _lastSequenceIssue = timestamp;
            _sourceSummary = replay ? "PCAP/PCAPNG replay" : "Live Npcap capture";

            if (decision == SmvIngressDecision.DropDuplicate)
            {
                _duplicates++;
                AddDiagnostic($"Duplicate smpCnt {sampleCount} rejected before measurement ingestion.");
            }
            else
            {
                _outOfOrder++;
                AddDiagnostic($"Out-of-order smpCnt {sampleCount} rejected before measurement ingestion.");
            }

            var trust = SmvTrustState.TripBlocked(
                "SMPCNT_DISCONTINUITY",
                "Recent duplicate or out-of-order SV frame was rejected before measurement ingestion.");
            _measurement = _measurement with { SmvTrust = trust };
            _protection.ObserveRejectedFrameTimestamp(timestamp);
            _protectionSnapshot = _protectionSnapshot with
            {
                Timestamp = timestamp,
                TripRequested = false,
                DecisionReason = _protectionSnapshot.TripLatched
                    ? _protectionSnapshot.DecisionReason
                    : $"TRIP BLOCKED · {trust.Code} · {trust.Detail}",
                SmvTrust = trust
            };
        }
    }

    public void Observe(DateTimeOffset timestamp, SampledValuesFrame frame, SampledValuesPublisherProfile? profile, bool replay)
    {
        var asdus = frame.Pdu.Asdus;
        if (asdus.Count == 0)
        {
            lock (_gate)
            {
                _lastPayloadIssue = timestamp;
                AddDiagnostic("SV frame contains no ASDU.");
            }
            return;
        }

        lock (_gate)
        {
            _firstSeen ??= timestamp;
            _lastSeen = timestamp;
            _frameCount++;
            _asduCount += asdus.Count;
            _sourceSummary = replay ? "PCAP/PCAPNG replay" : "Live Npcap capture";
            _sclBound = profile is not null;
            _sclSummary = profile is null
                ? "No matching SampledValueControl"
                : $"SCL · {profile.Stream.ControlBlockReference}";

            for (var index = 0; index < asdus.Count; index++)
            {
                var asdu = asdus[index];
                SvId = string.IsNullOrWhiteSpace(asdu.SvId) ? SvId : asdu.SvId;
                _sampleCounter = asdu.SampleCount;
                _sampleSynchronization = asdu.SampleSynchronization;
                _samplesPerCycle = ResolveSamplesPerCycle(asdu, _context.NominalFrequencyHz);
                ObserveCounter(asdu.SampleCount, ResolveCounterWrap(asdu, _context.NominalFrequencyHz), timestamp);

                if (profile is not null)
                    ObserveConfiguration(frame, asdu, profile, timestamp);

                if (!TryDecode(asdu, profile, out var sample, out var decodeSummary, out var scalingSummary, out var qualityGood))
                {
                    _lastPayloadIssue = timestamp;
                    AddDiagnostic(decodeSummary);
                    continue;
                }

                _mappingResolved = true;
                _voltageMappingResolved = sample.HasVoltage;
                _neutralCurrentMappingResolved = sample.HasNeutralCurrent;
                _neutralVoltageMappingResolved = sample.HasNeutralVoltage;
                _mappingSummary = decodeSummary;
                _scalingResolved = sample.ScalingResolved;
                _scalingSummary = scalingSummary;
                if (!qualityGood)
                {
                    _invalidQuality++;
                    _lastQualityIssue = timestamp;
                    AddDiagnostic("Non-zero IEC 61850 quality bits observed; trip permission removed.");
                }

                _phaseA.Add(sample.PhaseA);
                _phaseB.Add(sample.PhaseB);
                _phaseC.Add(sample.PhaseC);
                _residual.Add(sample.Residual);
                if (sample.HasVoltage)
                {
                    _voltageA.Add(sample.VoltageA);
                    _voltageB.Add(sample.VoltageB);
                    _voltageC.Add(sample.VoltageC);
                    _neutralVoltage.Add(sample.NeutralVoltage);
                }

                if (_phaseA.Count < _samplesPerCycle)
                    continue;

                var sampleRate = Math.Max(1, _samplesPerCycle * _context.NominalFrequencyHz);
                var asduTimestamp = timestamp.AddSeconds(index / sampleRate);
                var trust = ResolveTrust(asduTimestamp, replay);
                var phasors = BuildPhasorsIfReady();
                _measurement = new MeasurementFrame(
                    asduTimestamp,
                    _phaseA.RmsLast(_samplesPerCycle),
                    _phaseB.RmsLast(_samplesPerCycle),
                    _phaseC.RmsLast(_samplesPerCycle),
                    _residual.RmsLast(_samplesPerCycle),
                    trust,
                    phasors);
                _protectionSnapshot = _protection.Evaluate(_measurement);
            }
        }
    }

    public SmvRuntimeSnapshot Snapshot(DateTimeOffset now, bool primaryDisplay, bool replay)
    {
        lock (_gate)
        {
            var trustReference = replay ? (_lastSeen ?? now) : now;
            var trust = ResolveTrust(trustReference, replay);
            var measurement = _measurement with { SmvTrust = trust };
            if (!ReferenceEquals(trust, _measurement.SmvTrust) && !trust.AllowsMeasurement)
                _protectionSnapshot = _protection.Evaluate(measurement);

            var currentDisplayRatio = primaryDisplay ? _context.PrimaryRatio : 1;
            var voltageDisplayRatio = primaryDisplay ? _context.VoltagePrimaryRatio : 1;
            var waveformCount = Math.Min(_phaseA.Count, Math.Max(2, _samplesPerCycle * 2));
            var voltageWaveformCount = Math.Min(_voltageA.Count, waveformCount);
            var waveform = new SmvWaveformWindow(
                Scale(_phaseA.DisplayWindow(waveformCount), currentDisplayRatio),
                Scale(_phaseB.DisplayWindow(waveformCount), currentDisplayRatio),
                Scale(_phaseC.DisplayWindow(waveformCount), currentDisplayRatio),
                Scale(_residual.DisplayWindow(waveformCount), currentDisplayRatio),
                _context.NominalFrequencyHz,
                _samplesPerCycle,
                Scale(_voltageA.DisplayWindow(voltageWaveformCount), voltageDisplayRatio),
                Scale(_voltageB.DisplayWindow(voltageWaveformCount), voltageDisplayRatio),
                Scale(_voltageC.DisplayWindow(voltageWaveformCount), voltageDisplayRatio),
                Scale(_neutralVoltage.DisplayWindow(voltageWaveformCount), voltageDisplayRatio));

            var displayMeasurement = measurement with
            {
                PhaseA = measurement.PhaseA * currentDisplayRatio,
                PhaseB = measurement.PhaseB * currentDisplayRatio,
                PhaseC = measurement.PhaseC * currentDisplayRatio,
                Residual = measurement.Residual * currentDisplayRatio,
                Phasors = ScalePhasors(measurement.Phasors, currentDisplayRatio, voltageDisplayRatio)
            };

            var duration = _firstSeen.HasValue && _lastSeen.HasValue
                ? Math.Max(0.001, (_lastSeen.Value - _firstSeen.Value).TotalSeconds)
                : 0.001;
            var health = !trust.AllowsMeasurement ? "BAD" : trust.AllowsTrip ? "GOOD" : "WARN";
            var stream = new SmvStreamInfo(Key, AppId, SvId, Destination, VlanId, health, _sclBound);

            return new SmvRuntimeSnapshot(
                stream,
                _lastSeen ?? now,
                displayMeasurement,
                _protectionSnapshot,
                waveform,
                _sampleCounter,
                _sampleSynchronization,
                _samplesPerCycle,
                _frameCount / duration,
                _frameCount,
                _asduCount,
                _sequenceGaps,
                _duplicates,
                _outOfOrder,
                _invalidQuality,
                _scalingResolved,
                _scalingSummary,
                _mappingSummary,
                $"{trust.Code} · {trust.Detail}",
                _sclSummary,
                _sourceSummary,
                _diagnostics.Reverse().ToArray());
        }
    }

    private PhasorMeasurementSet? BuildPhasorsIfReady()
    {
        if (!_voltageMappingResolved ||
            _voltageA.Count < _samplesPerCycle ||
            _voltageB.Count < _samplesPerCycle ||
            _voltageC.Count < _samplesPerCycle ||
            _neutralVoltage.Count < _samplesPerCycle)
            return null;

        var phasors = FundamentalPhasorEstimator.Build(
            _phaseA.Last(_samplesPerCycle),
            _phaseB.Last(_samplesPerCycle),
            _phaseC.Last(_samplesPerCycle),
            _residual.Last(_samplesPerCycle),
            _voltageA.Last(_samplesPerCycle),
            _voltageB.Last(_samplesPerCycle),
            _voltageC.Last(_samplesPerCycle),
            _neutralVoltage.Last(_samplesPerCycle),
            _samplesPerCycle,
            _context.NominalFrequencyHz);
        return phasors with
        {
            NeutralCurrentAvailable = _neutralCurrentMappingResolved,
            NeutralVoltageAvailable = _neutralVoltageMappingResolved
        };
    }

    private SmvTrustState ResolveTrust(DateTimeOffset now, bool replay)
    {
        if (_phaseA.Count < _samplesPerCycle)
            return new SmvTrustState(false, false, false, "WINDOW_NOT_READY", "One-cycle measurement window is not complete.");
        if (!_mappingResolved)
            return new SmvTrustState(false, false, false, "MAPPING_UNRESOLVED", "Current channels could not be mapped from the SV payload.");
        if (!replay && (!_lastSeen.HasValue || now - _lastSeen.Value > LiveStaleWindow))
            return new SmvTrustState(false, false, false, "STREAM_STALE", "No fresh Sampled Values frame was received within 250 ms.");
        if (IsRecent(_lastPayloadIssue, now))
            return new SmvTrustState(false, false, false, "PAYLOAD_INVALID", "A recent SV payload could not be decoded.");
        if (IsRecent(_lastSequenceIssue, now))
            return SmvTrustState.TripBlocked("SMPCNT_DISCONTINUITY", "Recent smpCnt gap, duplicate, or out-of-order transition.");
        if (IsRecent(_lastQualityIssue, now))
            return SmvTrustState.TripBlocked("QUALITY_INVALID", "Recent non-zero IEC 61850 quality bits were observed.");
        if (IsRecent(_lastConfigurationIssue, now))
            return SmvTrustState.TripBlocked("SCL_MISMATCH", "Observed stream configuration differs from the imported SCL.");
        if (!_scalingResolved)
            return SmvTrustState.TripBlocked("SCALING_UNRESOLVED", "Current values are visible but engineering scaling is not proven.");
        if (!_sclBound)
            return SmvTrustState.TripBlocked("SCL_UNBOUND", "Stream is decoded but not bound to an imported SCL profile.");
        return SmvTrustState.Healthy;
    }

    private void ObserveCounter(ushort current, ushort wrap, DateTimeOffset timestamp)
    {
        if (!_lastSampleCounter.HasValue)
        {
            _lastSampleCounter = current;
            return;
        }

        var previous = _lastSampleCounter.Value;
        var expected = (ushort)((previous + 1) % wrap);
        if (current == expected)
        {
            _lastSampleCounter = current;
            return;
        }

        _lastSequenceIssue = timestamp;
        if (current == previous)
        {
            _duplicates++;
            AddDiagnostic($"Duplicate smpCnt {current}.");
        }
        else
        {
            var forward = (current - expected + wrap) % wrap;
            if (forward < wrap / 2)
            {
                _sequenceGaps++;
                AddDiagnostic($"smpCnt gap: expected {expected}, received {current}.");
            }
            else
            {
                _outOfOrder++;
                AddDiagnostic($"Out-of-order smpCnt: previous {previous}, received {current}.");
            }
        }
        _lastSampleCounter = current;
    }

    private void ObserveConfiguration(SampledValuesFrame frame, SampledValueAsdu asdu, SampledValuesPublisherProfile profile, DateTimeOffset timestamp)
    {
        var mismatch = frame.AppId != profile.AppId ||
                       !string.Equals(frame.Destination.ToString(), profile.Destination.ToString(), StringComparison.OrdinalIgnoreCase) ||
                       frame.Vlan?.VlanId != profile.Vlan?.VlanId ||
                       !string.Equals(asdu.SvId, profile.Stream.SvId, StringComparison.Ordinal) ||
                       (!string.IsNullOrWhiteSpace(asdu.DataSetReference) &&
                        !string.Equals(asdu.DataSetReference, profile.Stream.DataSetReference, StringComparison.Ordinal)) ||
                       asdu.ConfigurationRevision != profile.Stream.ConfigurationRevision;
        if (!mismatch)
            return;

        _lastConfigurationIssue = timestamp;
        AddDiagnostic("Observed APPID, destination, VLAN, svID, datSet, or confRev differs from the SCL profile.");
    }

    private static bool TryDecode(
        SampledValueAsdu asdu,
        SampledValuesPublisherProfile? profile,
        out DecodedSample sample,
        out string mappingSummary,
        out string scalingSummary,
        out bool qualityGood)
    {
        if (profile is not null && profile.PayloadLayout.IsFullySupported)
        {
            var decoded = SampledValuesPayloadDecoder.Decode(profile.PayloadLayout, asdu.SamplePayload);
            if (TryMapDecodedValues(decoded.Values, asdu.SamplePayload.Length, out sample, out qualityGood))
            {
                mappingSummary = sample.HasVoltage
                    ? $"SCL dataset order · 4I+4V phasor-capable · {profile.PayloadLayout.Elements.Count} element(s)"
                    : $"SCL dataset order · current-only · {profile.PayloadLayout.Elements.Count} element(s)";
                scalingSummary = sample.ScalingResolved
                    ? "IEC 61850 engineering scale · SCL-bound"
                    : "SCL-bound numeric values · raw engineering domain";
                return true;
            }
        }

        if (TryDecodeFixedValueQuality(asdu.SamplePayload, out sample, out qualityGood, out var channelCount))
        {
            mappingSummary = channelCount == 8
                ? "Auto fixed 4I+4V value-quality layout · phasor-capable"
                : $"Auto fixed current value-quality layout · {channelCount} channel(s)";
            scalingSummary = channelCount == 8
                ? "IEC 61850-9-2LE current/voltage scale · inferred"
                : "IEC 61850-9-2LE current scale · inferred";
            return true;
        }

        sample = default;
        qualityGood = false;
        mappingSummary = $"Unsupported SV payload layout · {asdu.SamplePayload.Length} byte(s)";
        scalingSummary = "Unresolved";
        return false;
    }

    private static bool TryMapDecodedValues(
        IReadOnlyList<SampledValuesDecodedValue> values,
        int payloadLength,
        out DecodedSample sample,
        out bool qualityGood)
    {
        double? ia = null;
        double? ib = null;
        double? ic = null;
        double? neutral = null;
        double? va = null;
        double? vb = null;
        double? vc = null;
        double? vn = null;
        qualityGood = true;
        var fixedLegacy = payloadLength == 64;

        foreach (var decoded in values)
        {
            if (decoded.Element.Kind == SampledValuePayloadElementKind.Quality)
            {
                if (decoded.RawBytes.Any(value => value != 0))
                    qualityGood = false;
                continue;
            }
            if (!TryGetNumeric(decoded.Value, out var numeric))
                continue;

            var channel = ClassifyAnalogChannel(decoded.Element.SignalReference);
            if (channel.Length == 0)
                continue;
            var engineering = fixedLegacy && decoded.Element.Kind == SampledValuePayloadElementKind.Int32
                ? numeric * (channel.StartsWith('V') ? 0.01 : 0.001)
                : numeric;

            switch (channel)
            {
                case "Ia": ia = engineering; break;
                case "Ib": ib = engineering; break;
                case "Ic": ic = engineering; break;
                case "In": neutral = engineering; break;
                case "Va": va = engineering; break;
                case "Vb": vb = engineering; break;
                case "Vc": vc = engineering; break;
                case "Vn": vn = engineering; break;
            }
        }

        if (!ia.HasValue || !ib.HasValue || !ic.HasValue)
        {
            sample = default;
            return false;
        }

        var residual = neutral ?? ia.Value + ib.Value + ic.Value;
        var hasVoltage = va.HasValue && vb.HasValue && vc.HasValue;
        sample = new DecodedSample(
            ia.Value,
            ib.Value,
            ic.Value,
            residual,
            hasVoltage ? va!.Value : 0,
            hasVoltage ? vb!.Value : 0,
            hasVoltage ? vc!.Value : 0,
            hasVoltage ? vn ?? va.Value + vb.Value + vc.Value : 0,
            hasVoltage,
            neutral.HasValue,
            hasVoltage && vn.HasValue,
            fixedLegacy);
        return true;
    }

    private static bool TryDecodeFixedValueQuality(byte[] payload, out DecodedSample sample, out bool qualityGood, out int channelCount)
    {
        sample = default;
        qualityGood = true;
        channelCount = payload.Length / 8;
        if (payload.Length < 24 || payload.Length % 8 != 0 || channelCount is not (3 or 4 or 8 or 12 or 15 or 16 or 20))
            return false;

        var values = new double[channelCount];
        for (var index = 0; index < channelCount; index++)
        {
            var offset = index * 8;
            var scale = channelCount == 8 && index >= 4 ? 0.01 : 0.001;
            values[index] = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset, 4)) * scale;
            if (HasNonZero(payload.AsSpan(offset + 4, 4)))
                qualityGood = false;
        }

        var residual = channelCount >= 4 ? values[3] : values[0] + values[1] + values[2];
        var hasVoltage = channelCount == 8;
        sample = new DecodedSample(
            values[0], values[1], values[2], residual,
            hasVoltage ? values[4] : 0,
            hasVoltage ? values[5] : 0,
            hasVoltage ? values[6] : 0,
            hasVoltage ? values[7] : 0,
            hasVoltage,
            channelCount >= 4,
            hasVoltage,
            true);
        return true;
    }

    private static string ClassifyAnalogChannel(string reference)
    {
        var text = reference.Replace('$', '.').Replace('/', '.').ToLowerInvariant();
        var voltage = text.Contains("tvtr", StringComparison.Ordinal) || text.Contains("vol", StringComparison.Ordinal) || text.Contains("voltage", StringComparison.Ordinal);
        if (!voltage && (text is "ia" or "amp.ia" or "current.ia" || text.Contains("tctr1", StringComparison.Ordinal) || text.Contains("phsa", StringComparison.Ordinal))) return "Ia";
        if (!voltage && (text is "ib" or "amp.ib" or "current.ib" || text.Contains("tctr2", StringComparison.Ordinal) || text.Contains("phsb", StringComparison.Ordinal))) return "Ib";
        if (!voltage && (text is "ic" or "amp.ic" or "current.ic" || text.Contains("tctr3", StringComparison.Ordinal) || text.Contains("phsc", StringComparison.Ordinal))) return "Ic";
        if (!voltage && (text is "in" or "amp.in" or "current.in" || text.Contains("tctr4", StringComparison.Ordinal) || text.Contains("neut", StringComparison.Ordinal) || text.Contains("phsn", StringComparison.Ordinal))) return "In";
        if (voltage && (text is "va" or "vol.va" or "voltage.va" || text.Contains("tvtr1", StringComparison.Ordinal) || text.Contains("phsa", StringComparison.Ordinal))) return "Va";
        if (voltage && (text is "vb" or "vol.vb" or "voltage.vb" || text.Contains("tvtr2", StringComparison.Ordinal) || text.Contains("phsb", StringComparison.Ordinal))) return "Vb";
        if (voltage && (text is "vc" or "vol.vc" or "voltage.vc" || text.Contains("tvtr3", StringComparison.Ordinal) || text.Contains("phsc", StringComparison.Ordinal))) return "Vc";
        if (voltage && (text is "vn" or "vol.vn" or "voltage.vn" || text.Contains("tvtr4", StringComparison.Ordinal) || text.Contains("phsn", StringComparison.Ordinal))) return "Vn";
        return string.Empty;
    }

    private static bool TryGetNumeric(MmsDataValue value, out double numeric)
    {
        numeric = value.Value switch
        {
            sbyte result => result,
            short result => result,
            int result => result,
            long result => result,
            byte result => result,
            ushort result => result,
            uint result => result,
            ulong result => result,
            float result => result,
            double result => result,
            _ => double.NaN
        };
        return double.IsFinite(numeric);
    }

    private static int ResolveSamplesPerCycle(SampledValueAsdu asdu, double nominalFrequency)
    {
        if (!asdu.SampleRate.HasValue || asdu.SampleRate.Value == 0)
            return 80;
        return asdu.SampleMode switch
        {
            0 => Math.Clamp(asdu.SampleRate.Value, (ushort)16, (ushort)512),
            1 => Math.Clamp((int)Math.Round(asdu.SampleRate.Value / nominalFrequency), 16, 512),
            _ when asdu.SampleRate.Value is 80 or 96 or 256 => asdu.SampleRate.Value,
            _ => 80
        };
    }

    private static ushort ResolveCounterWrap(SampledValueAsdu asdu, double nominalFrequency)
    {
        if (!asdu.SampleRate.HasValue || asdu.SampleRate.Value == 0)
            return 4000;
        var value = asdu.SampleMode switch
        {
            0 => asdu.SampleRate.Value * nominalFrequency,
            1 => asdu.SampleRate.Value,
            _ => asdu.SampleRate.Value is 80 or 96 ? asdu.SampleRate.Value * nominalFrequency : asdu.SampleRate.Value
        };
        return (ushort)Math.Clamp((int)Math.Round(value), 2, ushort.MaxValue);
    }

    private static bool HasNonZero(ReadOnlySpan<byte> source)
    {
        foreach (var value in source)
        {
            if (value != 0)
                return true;
        }
        return false;
    }

    private static bool IsRecent(DateTimeOffset? issue, DateTimeOffset now)
        => issue.HasValue && now >= issue.Value && now - issue.Value <= RecentIssueWindow;

    private static double[] Scale(double[] values, double ratio)
    {
        if (Math.Abs(ratio - 1) < 0.0000001)
            return values;
        for (var index = 0; index < values.Length; index++)
            values[index] *= ratio;
        return values;
    }

    private static PhasorMeasurementSet? ScalePhasors(PhasorMeasurementSet? source, double currentRatio, double voltageRatio)
    {
        if (source is null)
            return null;
        return source with
        {
            PhaseACurrent = source.PhaseACurrent * currentRatio,
            PhaseBCurrent = source.PhaseBCurrent * currentRatio,
            PhaseCCurrent = source.PhaseCCurrent * currentRatio,
            NeutralCurrent = source.NeutralCurrent * currentRatio,
            PhaseAVoltage = source.PhaseAVoltage * voltageRatio,
            PhaseBVoltage = source.PhaseBVoltage * voltageRatio,
            PhaseCVoltage = source.PhaseCVoltage * voltageRatio,
            NeutralVoltage = source.NeutralVoltage * voltageRatio
        };
    }

    private void AddDiagnostic(string message)
    {
        _diagnostics.Enqueue(message);
        while (_diagnostics.Count > 12)
            _diagnostics.Dequeue();
    }

    private readonly record struct DecodedSample(
        double PhaseA,
        double PhaseB,
        double PhaseC,
        double Residual,
        double VoltageA,
        double VoltageB,
        double VoltageC,
        double NeutralVoltage,
        bool HasVoltage,
        bool HasNeutralCurrent,
        bool HasNeutralVoltage,
        bool ScalingResolved);
}
#endif
