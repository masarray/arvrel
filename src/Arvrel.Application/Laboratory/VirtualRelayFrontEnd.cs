using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

public sealed record VirtualRelayFrontEndProfile(
    int AdcBits,
    double CurrentFullScaleRms,
    double VoltageFullScaleRms,
    int AdcSampleRateHz,
    TimeSpan MeasurementGroupDelay,
    bool QuantizationEnabled = true,
    bool ClippingEnabled = true)
{
    public static VirtualRelayFrontEndProfile Ideal { get; } = new(
        24,
        1_000_000_000,
        1_000_000_000,
        ClosedLoopVirtualTestBench.SimulationSampleRateHz,
        TimeSpan.Zero,
        QuantizationEnabled: false,
        ClippingEnabled: false);

    public static VirtualRelayFrontEndProfile NumericalRelayDefault { get; } = new(
        16,
        20,
        300,
        4_000,
        TimeSpan.FromMilliseconds(1.5),
        QuantizationEnabled: true,
        ClippingEnabled: true);

    public TimeSpan AdcSampleInterval => TimeSpan.FromTicks(TimeSpan.TicksPerSecond / AdcSampleRateHz);
    public double CurrentLeastSignificantRms => CurrentFullScaleRms / ((1L << (AdcBits - 1)) - 1d);
    public double VoltageLeastSignificantRms => VoltageFullScaleRms / ((1L << (AdcBits - 1)) - 1d);

    public void Validate()
    {
        if (AdcBits is < 8 or > 24)
            throw new ArgumentOutOfRangeException(nameof(AdcBits), "ADC resolution must be between 8 and 24 bits.");
        ValidateFullScale(CurrentFullScaleRms, nameof(CurrentFullScaleRms));
        ValidateFullScale(VoltageFullScaleRms, nameof(VoltageFullScaleRms));
        if (AdcSampleRateHz <= 0 ||
            AdcSampleRateHz > ClosedLoopVirtualTestBench.SimulationSampleRateHz ||
            ClosedLoopVirtualTestBench.SimulationSampleRateHz % AdcSampleRateHz != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AdcSampleRateHz),
                $"ADC sample rate must be a positive divisor of {ClosedLoopVirtualTestBench.SimulationSampleRateHz} Hz.");
        }
        if (MeasurementGroupDelay < TimeSpan.Zero || MeasurementGroupDelay > TimeSpan.FromMilliseconds(100))
            throw new ArgumentOutOfRangeException(nameof(MeasurementGroupDelay), "Measurement group delay must be between 0 and 100 ms.");
        if (MeasurementGroupDelay.Ticks % ClosedLoopVirtualTestBench.SimulationQuantum.Ticks != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MeasurementGroupDelay),
                $"Measurement group delay must align to the {ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds:0.###} ms deterministic grid.");
        }
    }

    public string Fingerprint()
    {
        Validate();
        var canonical = string.Join(
            '|',
            AdcBits.ToString(CultureInfo.InvariantCulture),
            CurrentFullScaleRms.ToString("R", CultureInfo.InvariantCulture),
            VoltageFullScaleRms.ToString("R", CultureInfo.InvariantCulture),
            AdcSampleRateHz.ToString(CultureInfo.InvariantCulture),
            MeasurementGroupDelay.Ticks.ToString(CultureInfo.InvariantCulture),
            QuantizationEnabled ? "1" : "0",
            ClippingEnabled ? "1" : "0");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static void ValidateFullScale(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0 || value > 1_000_000_000)
            throw new ArgumentOutOfRangeException(name, "Input full scale must be finite and between 0 and 1e9 RMS.");
    }
}

public sealed record VirtualRelayFrontEndSnapshot(
    DateTimeOffset? LastCapturedAt,
    DateTimeOffset? LastReleasedAt,
    int PendingFrames,
    long CaptureCount,
    long ClippedValueCount,
    long QuantizedValueCount,
    string ProfileFingerprint)
{
    public bool IsDelaying => PendingFrames > 0;
}

/// <summary>
/// Deterministic equivalent model of a numerical relay analog front end. The virtual
/// injector already owns waveform generation; this layer models what happens after the
/// signal reaches the relay terminals: sample-and-hold, finite input range, ADC
/// quantization and fixed measurement/filter group delay. Protection always receives a
/// frame timestamped at the current laboratory clock, while its values may come from an
/// older acquired sample when group delay is configured.
/// </summary>
internal sealed class VirtualRelayFrontEndRuntime
{
    private readonly VirtualRelayFrontEndProfile _profile;
    private readonly Queue<BufferedMeasurement> _pending = new();
    private MeasurementFrame? _held;
    private DateTimeOffset? _nextCaptureAt;
    private DateTimeOffset? _lastCapturedAt;
    private DateTimeOffset? _lastReleasedAt;
    private long _captureCount;
    private long _clippedValueCount;
    private long _quantizedValueCount;

    public VirtualRelayFrontEndRuntime(VirtualRelayFrontEndProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _profile.Validate();
    }

    public VirtualRelayFrontEndSnapshot Snapshot => new(
        _lastCapturedAt,
        _lastReleasedAt,
        _pending.Count,
        _captureCount,
        _clippedValueCount,
        _quantizedValueCount,
        _profile.Fingerprint());

    public MeasurementFrame Process(MeasurementFrame terminalMeasurement)
    {
        ArgumentNullException.ThrowIfNull(terminalMeasurement);
        var now = terminalMeasurement.Timestamp;

        if (_nextCaptureAt is null || now >= _nextCaptureAt.Value)
        {
            var captured = ConvertInput(terminalMeasurement);
            _pending.Enqueue(new BufferedMeasurement(now + _profile.MeasurementGroupDelay, captured));
            _lastCapturedAt = now;
            _captureCount++;
            _nextCaptureAt = now + _profile.AdcSampleInterval;
        }

        while (_pending.Count > 0 && _pending.Peek().AvailableAt <= now)
        {
            var released = _pending.Dequeue();
            _held = released.Measurement;
            _lastReleasedAt = now;
        }

        return _held is null
            ? ZeroLike(terminalMeasurement, now)
            : Retimestamp(_held, now);
    }

    public void Reset()
    {
        _pending.Clear();
        _held = null;
        _nextCaptureAt = null;
        _lastCapturedAt = null;
        _lastReleasedAt = null;
        _captureCount = 0;
        _clippedValueCount = 0;
        _quantizedValueCount = 0;
    }

    private MeasurementFrame ConvertInput(MeasurementFrame source)
    {
        if (source.Phasors is not { } phasors)
        {
            return source with
            {
                PhaseA = ConvertMagnitude(source.PhaseA, _profile.CurrentFullScaleRms),
                PhaseB = ConvertMagnitude(source.PhaseB, _profile.CurrentFullScaleRms),
                PhaseC = ConvertMagnitude(source.PhaseC, _profile.CurrentFullScaleRms),
                Residual = ConvertMagnitude(source.Residual, _profile.CurrentFullScaleRms)
            };
        }

        var converted = new PhasorMeasurementSet(
            ConvertPhasor(phasors.PhaseACurrent, _profile.CurrentFullScaleRms),
            ConvertPhasor(phasors.PhaseBCurrent, _profile.CurrentFullScaleRms),
            ConvertPhasor(phasors.PhaseCCurrent, _profile.CurrentFullScaleRms),
            ConvertPhasor(phasors.NeutralCurrent, _profile.CurrentFullScaleRms),
            ConvertPhasor(phasors.PhaseAVoltage, _profile.VoltageFullScaleRms),
            ConvertPhasor(phasors.PhaseBVoltage, _profile.VoltageFullScaleRms),
            ConvertPhasor(phasors.PhaseCVoltage, _profile.VoltageFullScaleRms),
            ConvertPhasor(phasors.NeutralVoltage, _profile.VoltageFullScaleRms),
            phasors.FrequencyHz)
        {
            NeutralCurrentAvailable = phasors.NeutralCurrentAvailable,
            NeutralVoltageAvailable = phasors.NeutralVoltageAvailable
        };

        return new MeasurementFrame(
            source.Timestamp,
            converted.PhaseACurrent.Magnitude,
            converted.PhaseBCurrent.Magnitude,
            converted.PhaseCCurrent.Magnitude,
            converted.ResidualCurrent.Magnitude,
            source.SmvTrust,
            converted);
    }

    private Complex ConvertPhasor(Complex value, double fullScale)
    {
        var magnitude = value.Magnitude;
        if (magnitude <= double.Epsilon)
            return Complex.Zero;
        var converted = ConvertMagnitude(magnitude, fullScale);
        return converted <= double.Epsilon
            ? Complex.Zero
            : Complex.FromPolarCoordinates(converted, value.Phase);
    }

    private double ConvertMagnitude(double value, double fullScale)
    {
        var magnitude = Math.Abs(value);
        var clipped = magnitude;
        if (_profile.ClippingEnabled && magnitude > fullScale)
        {
            clipped = fullScale;
            _clippedValueCount++;
        }

        if (!_profile.QuantizationEnabled)
            return clipped;

        var denominator = (1L << (_profile.AdcBits - 1)) - 1d;
        var step = fullScale / denominator;
        var quantized = Math.Round(clipped / step, MidpointRounding.AwayFromZero) * step;
        quantized = Math.Min(quantized, fullScale);
        if (Math.Abs(quantized - clipped) > 1e-12)
            _quantizedValueCount++;
        return quantized;
    }

    private static MeasurementFrame Retimestamp(MeasurementFrame value, DateTimeOffset timestamp)
        => value with { Timestamp = timestamp };

    private static MeasurementFrame ZeroLike(MeasurementFrame source, DateTimeOffset timestamp)
    {
        if (source.Phasors is not { } phasors)
            return source with { Timestamp = timestamp, PhaseA = 0, PhaseB = 0, PhaseC = 0, Residual = 0 };

        var zero = new PhasorMeasurementSet(
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            phasors.FrequencyHz)
        {
            NeutralCurrentAvailable = phasors.NeutralCurrentAvailable,
            NeutralVoltageAvailable = phasors.NeutralVoltageAvailable
        };
        return new MeasurementFrame(timestamp, 0, 0, 0, 0, source.SmvTrust, zero);
    }

    private sealed record BufferedMeasurement(DateTimeOffset AvailableAt, MeasurementFrame Measurement);
}
