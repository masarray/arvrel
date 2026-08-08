using System.Numerics;
using Arvrel.Protection;

namespace Arvrel.ProcessBus;

public sealed record TransformerSvPairingSettings
{
    /// <summary>
    /// Maximum signed smpCnt separation accepted between the two latest stream
    /// snapshots. A small offset is phase-corrected before the pair is emitted.
    /// </summary>
    public int MaximumSampleCounterSkew { get; init; } = 1;

    /// <summary>
    /// Prevents a repeated smpCnt from a stale/wrapped stream being paired with a
    /// fresh stream. This is capture-arrival skew, not the electrical sample time.
    /// </summary>
    public TimeSpan MaximumCaptureTimestampSkew { get; init; } = TimeSpan.FromMilliseconds(20);

    public double FrequencyToleranceHz { get; init; } = 0.05;

    /// <summary>
    /// Conservative production-oriented default: both streams must advertise
    /// smpSynch=2 (global area clock). Set false only for controlled laboratory work.
    /// </summary>
    public bool RequireGlobalSynchronization { get; init; } = true;

    public void Validate()
    {
        if (MaximumSampleCounterSkew is < 0 or > 16)
            throw new ArgumentOutOfRangeException(nameof(MaximumSampleCounterSkew));
        if (MaximumCaptureTimestampSkew < TimeSpan.Zero || MaximumCaptureTimestampSkew > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(MaximumCaptureTimestampSkew));
        if (!double.IsFinite(FrequencyToleranceHz) || FrequencyToleranceHz < 0 || FrequencyToleranceHz > 5)
            throw new ArgumentOutOfRangeException(nameof(FrequencyToleranceHz));
    }
}

public sealed record TransformerSvPairDiagnostics(
    bool Aligned,
    string Code,
    string Detail,
    int SignedSampleCounterSkew,
    double PhaseCorrectionDegrees,
    TimeSpan CaptureTimestampSkew,
    byte HighVoltageSynchronization,
    byte LowVoltageSynchronization,
    int SamplesPerCycle,
    double FrequencyHz)
{
    public static TransformerSvPairDiagnostics Unavailable(string code, string detail) => new(
        false,
        code,
        detail,
        0,
        0,
        TimeSpan.Zero,
        0,
        0,
        0,
        0);
}

public sealed record TransformerSvPairSnapshot(
    string HighVoltageStreamKey,
    string LowVoltageStreamKey,
    TransformerMeasurementFrame? Measurement,
    TransformerSvPairDiagnostics Diagnostics)
{
    public bool IsAligned => Measurement is not null && Diagnostics.Aligned;
}

/// <summary>
/// Reads two selected SV runtimes in the raw secondary-current display domain and
/// creates one time-aligned transformer measurement frame. This adapter only aligns
/// fundamental current phasors; H2/H5 estimation is deliberately a later layer.
/// </summary>
public sealed class TransformerProcessBusAdapter
{
    private readonly SmvProcessBusController _controller;
    private readonly TransformerSvPairingSettings _settings;

    public TransformerProcessBusAdapter(
        SmvProcessBusController controller,
        TransformerSvPairingSettings? settings = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _settings = settings ?? new TransformerSvPairingSettings();
        _settings.Validate();
    }

    public TransformerSvPairSnapshot ReadPair(string highVoltageStreamKey, string lowVoltageStreamKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(highVoltageStreamKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(lowVoltageStreamKey);
        if (string.Equals(highVoltageStreamKey, lowVoltageStreamKey, StringComparison.Ordinal))
        {
            return Unavailable(
                highVoltageStreamKey,
                lowVoltageStreamKey,
                "PAIR_SAME_STREAM",
                "HV and LV transformer inputs must use two distinct SV streams.");
        }

        // Always read secondary display values. TransformerEngineeringAdapter owns
        // the CT-ratio conversion to the common per-unit differential current base.
        var highVoltage = _controller.GetSnapshot(highVoltageStreamKey, primaryDisplay: false);
        var lowVoltage = _controller.GetSnapshot(lowVoltageStreamKey, primaryDisplay: false);
        return AlignSnapshots(highVoltage, lowVoltage, _settings);
    }

    public static TransformerSvPairSnapshot AlignSnapshots(
        SmvRuntimeSnapshot highVoltage,
        SmvRuntimeSnapshot lowVoltage,
        TransformerSvPairingSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(highVoltage);
        ArgumentNullException.ThrowIfNull(lowVoltage);
        var policy = settings ?? new TransformerSvPairingSettings();
        policy.Validate();

        var hvKey = highVoltage.Stream.Key;
        var lvKey = lowVoltage.Stream.Key;
        if (string.IsNullOrWhiteSpace(hvKey) || string.IsNullOrWhiteSpace(lvKey))
            return Unavailable(hvKey, lvKey, "PAIR_STREAM_MISSING", "Both transformer SV streams must be discovered before pairing.");
        if (string.Equals(hvKey, lvKey, StringComparison.Ordinal))
            return Unavailable(hvKey, lvKey, "PAIR_SAME_STREAM", "HV and LV transformer inputs must use two distinct SV streams.");

        if (highVoltage.SamplesPerCycle != lowVoltage.SamplesPerCycle || highVoltage.SamplesPerCycle < 4)
        {
            return Unavailable(
                hvKey,
                lvKey,
                "PAIR_SAMPLE_RATE_MISMATCH",
                $"HV/LV samples-per-cycle differ ({highVoltage.SamplesPerCycle}/{lowVoltage.SamplesPerCycle}).");
        }

        var hvFrequency = highVoltage.Waveform.FrequencyHz;
        var lvFrequency = lowVoltage.Waveform.FrequencyHz;
        if (Math.Abs(hvFrequency - lvFrequency) > policy.FrequencyToleranceHz)
        {
            return Unavailable(
                hvKey,
                lvKey,
                "PAIR_FREQUENCY_MISMATCH",
                $"HV/LV nominal frequencies differ ({hvFrequency:0.###}/{lvFrequency:0.###} Hz).");
        }

        if (!SynchronizationAccepted(highVoltage.SampleSynchronization, lowVoltage.SampleSynchronization, policy))
        {
            return new TransformerSvPairSnapshot(
                hvKey,
                lvKey,
                null,
                new TransformerSvPairDiagnostics(
                    false,
                    "PAIR_NOT_SYNCHRONIZED",
                    policy.RequireGlobalSynchronization
                        ? "Both streams must advertise global synchronization (smpSynch=2)."
                        : "Both streams must advertise the same non-zero synchronization state.",
                    0,
                    0,
                    Abs(highVoltage.Timestamp - lowVoltage.Timestamp),
                    highVoltage.SampleSynchronization,
                    lowVoltage.SampleSynchronization,
                    highVoltage.SamplesPerCycle,
                    (hvFrequency + lvFrequency) / 2));
        }

        if (!TryEstimateCurrents(highVoltage, out var hvCurrents, out var hvNeutral, out var hvNeutralAvailable, out var hvReason))
            return Unavailable(hvKey, lvKey, "PAIR_HV_WINDOW_NOT_READY", hvReason);
        if (!TryEstimateCurrents(lowVoltage, out var lvCurrents, out var lvNeutral, out var lvNeutralAvailable, out var lvReason))
            return Unavailable(hvKey, lvKey, "PAIR_LV_WINDOW_NOT_READY", lvReason);

        var frequency = (hvFrequency + lvFrequency) / 2;
        var counterWrap = Math.Max(2, (int)Math.Round(highVoltage.SamplesPerCycle * frequency));
        var signedSkew = SignedCounterDistance(highVoltage.SampleCounter, lowVoltage.SampleCounter, counterWrap);
        if (Math.Abs(signedSkew) > policy.MaximumSampleCounterSkew)
        {
            return new TransformerSvPairSnapshot(
                hvKey,
                lvKey,
                null,
                new TransformerSvPairDiagnostics(
                    false,
                    "PAIR_SMPCNT_SKEW",
                    $"HV/LV smpCnt separation {signedSkew} exceeds allowed ±{policy.MaximumSampleCounterSkew} sample(s).",
                    signedSkew,
                    0,
                    Abs(highVoltage.Timestamp - lowVoltage.Timestamp),
                    highVoltage.SampleSynchronization,
                    lowVoltage.SampleSynchronization,
                    highVoltage.SamplesPerCycle,
                    frequency));
        }

        var captureSkew = Abs(highVoltage.Timestamp - lowVoltage.Timestamp);
        if (captureSkew > policy.MaximumCaptureTimestampSkew)
        {
            return new TransformerSvPairSnapshot(
                hvKey,
                lvKey,
                null,
                new TransformerSvPairDiagnostics(
                    false,
                    "PAIR_CAPTURE_SKEW",
                    $"HV/LV capture timestamp skew {captureSkew.TotalMilliseconds:0.###} ms exceeds the configured limit.",
                    signedSkew,
                    0,
                    captureSkew,
                    highVoltage.SampleSynchronization,
                    lowVoltage.SampleSynchronization,
                    highVoltage.SamplesPerCycle,
                    frequency));
        }

        // Reference the pair to the HV smpCnt. A newer LV window has advanced phase,
        // so rotate it backwards by signedSkew * sample angle; an older LV window is
        // rotated forward. This avoids introducing artificial Idiff for a one-sample
        // network-arrival offset between otherwise synchronized merging units.
        var phaseCorrectionDegrees = -signedSkew * 360.0 / highVoltage.SamplesPerCycle;
        var correctedLvCurrents = Rotate(lvCurrents, phaseCorrectionDegrees);
        var correctedLvNeutral = Rotate(lvNeutral, phaseCorrectionDegrees);

        var highVoltageMeasurement = new TransformerWindingMeasurement(hvCurrents, highVoltage.Measurement.SmvTrust)
        {
            NeutralCurrentA = hvNeutral,
            NeutralCurrentAvailable = hvNeutralAvailable
        };
        var lowVoltageMeasurement = new TransformerWindingMeasurement(correctedLvCurrents, lowVoltage.Measurement.SmvTrust)
        {
            NeutralCurrentA = correctedLvNeutral,
            NeutralCurrentAvailable = lvNeutralAvailable
        };

        var frame = new TransformerMeasurementFrame(
            highVoltage.Measurement.Timestamp,
            highVoltageMeasurement,
            lowVoltageMeasurement);
        var detail = signedSkew == 0
            ? "HV/LV SV streams aligned on the same synchronized smpCnt."
            : $"HV/LV SV streams aligned with {phaseCorrectionDegrees:+0.###;-0.###;0}° LV phasor correction for {signedSkew:+#;-#;0} sample skew.";

        return new TransformerSvPairSnapshot(
            hvKey,
            lvKey,
            frame,
            new TransformerSvPairDiagnostics(
                true,
                "PAIR_ALIGNED",
                detail,
                signedSkew,
                phaseCorrectionDegrees,
                captureSkew,
                highVoltage.SampleSynchronization,
                lowVoltage.SampleSynchronization,
                highVoltage.SamplesPerCycle,
                frequency));
    }

    private static bool TryEstimateCurrents(
        SmvRuntimeSnapshot snapshot,
        out TransformerPhaseCurrents currents,
        out Complex neutral,
        out bool neutralAvailable,
        out string reason)
    {
        var waveform = snapshot.Waveform;
        var samplesPerCycle = snapshot.SamplesPerCycle;
        if (waveform.PhaseA.Count < samplesPerCycle ||
            waveform.PhaseB.Count < samplesPerCycle ||
            waveform.PhaseC.Count < samplesPerCycle ||
            waveform.Residual.Count < samplesPerCycle)
        {
            currents = new TransformerPhaseCurrents(Complex.Zero, Complex.Zero, Complex.Zero);
            neutral = Complex.Zero;
            neutralAvailable = false;
            reason = "A complete one-cycle current window is required on both transformer sides.";
            return false;
        }

        currents = new TransformerPhaseCurrents(
            FundamentalPhasorEstimator.EstimateRms(waveform.PhaseA, samplesPerCycle),
            FundamentalPhasorEstimator.EstimateRms(waveform.PhaseB, samplesPerCycle),
            FundamentalPhasorEstimator.EstimateRms(waveform.PhaseC, samplesPerCycle));
        neutral = FundamentalPhasorEstimator.EstimateRms(waveform.Residual, samplesPerCycle);

        // A 4I+4V phasor set carries the authoritative channel-availability flag.
        // For current-only streams the existing runtime does not yet expose that flag,
        // therefore REF remains conservatively blocked rather than guessing that 3I0
        // is an independent neutral CT measurement.
        neutralAvailable = snapshot.Measurement.Phasors?.NeutralCurrentAvailable == true;
        reason = "Ready";
        return true;
    }

    private static bool SynchronizationAccepted(byte hv, byte lv, TransformerSvPairingSettings settings)
    {
        if (settings.RequireGlobalSynchronization)
            return hv == 2 && lv == 2;
        return hv != 0 && hv == lv;
    }

    private static int SignedCounterDistance(ushort highVoltage, ushort lowVoltage, int wrap)
    {
        var forward = ((int)lowVoltage - highVoltage + wrap) % wrap;
        return forward > wrap / 2 ? forward - wrap : forward;
    }

    private static TransformerPhaseCurrents Rotate(TransformerPhaseCurrents currents, double degrees)
    {
        var factor = Rotation(degrees);
        return new TransformerPhaseCurrents(
            currents.PhaseA * factor,
            currents.PhaseB * factor,
            currents.PhaseC * factor);
    }

    private static Complex Rotate(Complex value, double degrees) => value * Rotation(degrees);

    private static Complex Rotation(double degrees)
        => Complex.FromPolarCoordinates(1, degrees * Math.PI / 180);

    private static TimeSpan Abs(TimeSpan value) => value < TimeSpan.Zero ? -value : value;

    private static TransformerSvPairSnapshot Unavailable(string? hvKey, string? lvKey, string code, string detail) => new(
        hvKey ?? string.Empty,
        lvKey ?? string.Empty,
        null,
        TransformerSvPairDiagnostics.Unavailable(code, detail));
}
