using System.Numerics;
using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

public sealed record CausalRelayFrontEndSnapshot(
    DateTimeOffset? LastAdcSampleAt,
    DateTimeOffset? MeasurementValidAt,
    int WindowSamples,
    int WindowFill,
    long AdcSampleCount,
    long ClippedSampleCount,
    long QuantizedSampleCount,
    string ProfileFingerprint)
{
    public bool MeasurementValid => WindowSamples > 0 && WindowFill >= WindowSamples;
}

internal sealed record VirtualRelayTerminalSample(
    DateTimeOffset Timestamp,
    long MetrologyMicroseconds,
    long SourceSampleIndex,
    double SourceSampleRateHz,
    double FrequencyHz,
    double PhaseACurrent,
    double PhaseBCurrent,
    double PhaseCCurrent,
    double NeutralCurrent,
    double PhaseAVoltage,
    double PhaseBVoltage,
    double PhaseCVoltage,
    double NeutralVoltage,
    bool NeutralCurrentAvailable,
    bool NeutralVoltageAvailable,
    SmvTrustState SmvTrust);

/// <summary>
/// Causal numerical-relay acquisition path. Unlike the legacy front end, this runtime
/// never receives a pre-computed source phasor. It consumes one instantaneous terminal
/// sample at a time, applies signed ADC clipping/quantization, delays samples through
/// the configured input/filter latency, and builds a one-cycle rolling DFT only from
/// samples that have already arrived.
///
/// A powered relay does not begin a fault test with an empty measurement window. Reset
/// therefore primes one cycle of zero/pre-fault history. At T0 the newly injected samples
/// causally replace that history, reproducing a filter step response instead of imposing
/// an artificial one-cycle measurement blackout.
/// </summary>
internal sealed class CausalRelayFrontEndRuntime
{
    private const double NominalMeasurementFrequencyHz = 50d;

    private readonly VirtualRelayFrontEndProfile _profile;
    private readonly Queue<DelayedSample> _pending = new();
    private readonly Queue<QuantizedSample> _window = new();
    private readonly int _windowSamples;
    private readonly long _capturePeriodUs;
    private readonly long _groupDelayUs;
    private long? _nextCaptureUs;
    private DateTimeOffset? _lastAdcSampleAt;
    private DateTimeOffset? _measurementValidAt;
    private long _adcSampleCount;
    private long _clippedSampleCount;
    private long _quantizedSampleCount;

    public CausalRelayFrontEndRuntime(VirtualRelayFrontEndProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _profile.Validate();
        _windowSamples = checked((int)Math.Round(_profile.AdcSampleRateHz / NominalMeasurementFrequencyHz));
        if (_windowSamples < 4)
            throw new ArgumentOutOfRangeException(nameof(profile), "ADC rate must provide at least four samples per nominal cycle.");
        if (1_000_000 % _profile.AdcSampleRateHz != 0)
            throw new ArgumentOutOfRangeException(nameof(profile), "Metrology-grade ADC rate must divide one second exactly.");
        _capturePeriodUs = 1_000_000 / _profile.AdcSampleRateHz;
        if (_profile.MeasurementGroupDelay.Ticks % 10 != 0)
            throw new ArgumentOutOfRangeException(nameof(profile), "Front-end group delay must resolve to whole microseconds.");
        _groupDelayUs = _profile.MeasurementGroupDelay.Ticks / 10;
        Reset();
    }

    public CausalRelayFrontEndSnapshot Snapshot => new(
        _lastAdcSampleAt,
        _measurementValidAt,
        _windowSamples,
        _window.Count,
        _adcSampleCount,
        _clippedSampleCount,
        _quantizedSampleCount,
        _profile.Fingerprint());

    public MeasurementFrame Process(VirtualRelayTerminalSample terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        CaptureIfDue(terminal);
        ReleaseAvailable(terminal.MetrologyMicroseconds);

        if (_window.Count < _windowSamples)
            return ZeroLike(terminal);

        _measurementValidAt ??= terminal.Timestamp;
        var samples = _window.ToArray();
        var firstIndex = samples[0].SourceSampleIndex;
        var sourceSamplesPerCycle = terminal.SourceSampleRateHz / NominalMeasurementFrequencyHz;

        var ia = Estimate(samples.Select(item => item.PhaseACurrent).ToArray(), firstIndex, sourceSamplesPerCycle);
        var ib = Estimate(samples.Select(item => item.PhaseBCurrent).ToArray(), firstIndex, sourceSamplesPerCycle);
        var ic = Estimate(samples.Select(item => item.PhaseCCurrent).ToArray(), firstIndex, sourceSamplesPerCycle);
        var explicitIn = Estimate(samples.Select(item => item.NeutralCurrent).ToArray(), firstIndex, sourceSamplesPerCycle);
        var va = Estimate(samples.Select(item => item.PhaseAVoltage).ToArray(), firstIndex, sourceSamplesPerCycle);
        var vb = Estimate(samples.Select(item => item.PhaseBVoltage).ToArray(), firstIndex, sourceSamplesPerCycle);
        var vc = Estimate(samples.Select(item => item.PhaseCVoltage).ToArray(), firstIndex, sourceSamplesPerCycle);
        var explicitVn = Estimate(samples.Select(item => item.NeutralVoltage).ToArray(), firstIndex, sourceSamplesPerCycle);

        var neutralCurrentAvailable = samples[^1].NeutralCurrentAvailable;
        var neutralVoltageAvailable = samples[^1].NeutralVoltageAvailable;
        var phasors = new PhasorMeasurementSet(
            ia,
            ib,
            ic,
            neutralCurrentAvailable ? explicitIn : Complex.Zero,
            va,
            vb,
            vc,
            neutralVoltageAvailable ? explicitVn : Complex.Zero,
            terminal.FrequencyHz)
        {
            NeutralCurrentAvailable = neutralCurrentAvailable,
            NeutralVoltageAvailable = neutralVoltageAvailable
        };

        return new MeasurementFrame(
            terminal.Timestamp,
            ia.Magnitude,
            ib.Magnitude,
            ic.Magnitude,
            phasors.ResidualCurrent.Magnitude,
            terminal.SmvTrust,
            phasors);
    }

    public void Reset()
    {
        _pending.Clear();
        _window.Clear();
        _nextCaptureUs = null;
        _lastAdcSampleAt = null;
        _measurementValidAt = null;
        _adcSampleCount = 0;
        _clippedSampleCount = 0;
        _quantizedSampleCount = 0;
        PrimeSettledZeroHistory();
    }

    private void PrimeSettledZeroHistory()
    {
        var firstIndex = -(_windowSamples - 1L);
        for (var index = 0; index < _windowSamples; index++)
        {
            _window.Enqueue(new QuantizedSample(
                firstIndex + index,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                false));
        }
    }

    private void CaptureIfDue(VirtualRelayTerminalSample terminal)
    {
        _nextCaptureUs ??= terminal.MetrologyMicroseconds;
        if (terminal.MetrologyMicroseconds < _nextCaptureUs.Value)
            return;

        var sample = new QuantizedSample(
            terminal.SourceSampleIndex,
            ConvertInstantaneous(terminal.PhaseACurrent, _profile.CurrentFullScaleRms),
            ConvertInstantaneous(terminal.PhaseBCurrent, _profile.CurrentFullScaleRms),
            ConvertInstantaneous(terminal.PhaseCCurrent, _profile.CurrentFullScaleRms),
            ConvertInstantaneous(terminal.NeutralCurrent, _profile.CurrentFullScaleRms),
            ConvertInstantaneous(terminal.PhaseAVoltage, _profile.VoltageFullScaleRms),
            ConvertInstantaneous(terminal.PhaseBVoltage, _profile.VoltageFullScaleRms),
            ConvertInstantaneous(terminal.PhaseCVoltage, _profile.VoltageFullScaleRms),
            ConvertInstantaneous(terminal.NeutralVoltage, _profile.VoltageFullScaleRms),
            terminal.NeutralCurrentAvailable,
            terminal.NeutralVoltageAvailable);

        _pending.Enqueue(new DelayedSample(checked(terminal.MetrologyMicroseconds + _groupDelayUs), sample));
        _lastAdcSampleAt = terminal.Timestamp;
        _adcSampleCount++;
        do
        {
            _nextCaptureUs = checked(_nextCaptureUs.Value + _capturePeriodUs);
        }
        while (_nextCaptureUs <= terminal.MetrologyMicroseconds);
    }

    private void ReleaseAvailable(long nowUs)
    {
        while (_pending.Count > 0 && _pending.Peek().AvailableAtMicroseconds <= nowUs)
        {
            var released = _pending.Dequeue().Sample;
            _window.Enqueue(released);
            while (_window.Count > _windowSamples)
                _window.Dequeue();
        }
    }

    private double ConvertInstantaneous(double value, double fullScaleRms)
    {
        var peakFullScale = fullScaleRms * Math.Sqrt(2d);
        var converted = value;
        if (_profile.ClippingEnabled && Math.Abs(converted) > peakFullScale)
        {
            converted = Math.CopySign(peakFullScale, converted);
            _clippedSampleCount++;
        }

        if (!_profile.QuantizationEnabled)
            return converted;

        var maximumCode = (1L << (_profile.AdcBits - 1)) - 1d;
        var step = peakFullScale / maximumCode;
        var quantized = Math.Round(converted / step, MidpointRounding.AwayFromZero) * step;
        quantized = Math.Clamp(quantized, -peakFullScale, peakFullScale);
        if (Math.Abs(quantized - converted) > 1e-12)
            _quantizedSampleCount++;
        return quantized;
    }

    private static Complex Estimate(double[] values, long firstSourceSampleIndex, double sourceSamplesPerCycle)
    {
        if (values.Length == 0)
            return Complex.Zero;

        var sum = Complex.Zero;
        for (var index = 0; index < values.Length; index++)
        {
            var angle = -2d * Math.PI * index / values.Length;
            sum += values[index] * Complex.FromPolarCoordinates(1d, angle);
        }

        var rms = sum * (Math.Sqrt(2d) / values.Length);
        var absoluteStartPhase = 2d * Math.PI * firstSourceSampleIndex / sourceSamplesPerCycle;
        return rms * Complex.FromPolarCoordinates(1d, -absoluteStartPhase);
    }

    private static MeasurementFrame ZeroLike(VirtualRelayTerminalSample terminal)
    {
        var zero = new PhasorMeasurementSet(
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            Complex.Zero,
            terminal.FrequencyHz)
        {
            NeutralCurrentAvailable = terminal.NeutralCurrentAvailable,
            NeutralVoltageAvailable = terminal.NeutralVoltageAvailable
        };
        return new MeasurementFrame(
            terminal.Timestamp,
            0,
            0,
            0,
            0,
            terminal.SmvTrust,
            zero);
    }

    private sealed record DelayedSample(long AvailableAtMicroseconds, QuantizedSample Sample);

    private sealed record QuantizedSample(
        long SourceSampleIndex,
        double PhaseACurrent,
        double PhaseBCurrent,
        double PhaseCCurrent,
        double NeutralCurrent,
        double PhaseAVoltage,
        double PhaseBVoltage,
        double PhaseCVoltage,
        double NeutralVoltage,
        bool NeutralCurrentAvailable,
        bool NeutralVoltageAvailable);
}
