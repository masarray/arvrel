using Arvrel.App.Controls;
using Arvrel.Protection;

namespace Arvrel.App.Services;

public sealed class DeterministicLabScenario
{
    public const double Frequency = 50;
    public const int SamplesPerCycle = 80;
    public const int WindowSamples = SamplesPerCycle * 2;

    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private long _sampleCounter;
    private double _faultAgeSeconds;

    public bool FaultActive { get; set; }
    public bool SmvDegraded { get; set; }
    public long SampleCounter => _sampleCounter;

    public ScenarioStep Advance(TimeSpan delta, double pickupPosition, double tripPosition)
    {
        _timestamp += delta;
        _sampleCounter = (_sampleCounter + (long)Math.Round(delta.TotalSeconds * Frequency * SamplesPerCycle)) % 4000;
        _faultAgeSeconds = FaultActive ? _faultAgeSeconds + delta.TotalSeconds : 0;

        var ramp = FaultActive ? Math.Clamp(_faultAgeSeconds / 0.12, 0, 1) : 0;
        var phaseA = 1 + 7.4 * ramp;
        var phaseB = 1.02;
        var phaseC = 0.98;
        var residual = FaultActive ? 0.02 + 7.32 * ramp : 0.02;
        var trust = SmvDegraded
            ? SmvTrustState.TripBlocked("SMPCNT_GAP", "Repeated sample-counter discontinuity exceeds the active trip policy.")
            : SmvTrustState.Healthy;

        var frame = new MeasurementFrame(_timestamp, phaseA, phaseB, phaseC, residual, trust);
        return new ScenarioStep(frame, BuildWaveform(phaseA, phaseB, phaseC, residual, pickupPosition, tripPosition));
    }

    public void Reset()
    {
        _timestamp = DateTimeOffset.UtcNow;
        _sampleCounter = 0;
        _faultAgeSeconds = 0;
        FaultActive = false;
        SmvDegraded = false;
    }

    private static WaveformFrame BuildWaveform(
        double phaseARms,
        double phaseBRms,
        double phaseCRms,
        double residualRms,
        double pickupPosition,
        double tripPosition)
    {
        var phaseA = new double[WindowSamples];
        var phaseB = new double[WindowSamples];
        var phaseC = new double[WindowSamples];
        var residual = new double[WindowSamples];
        var rootTwo = Math.Sqrt(2);

        for (var index = 0; index < WindowSamples; index++)
        {
            var angle = index / (double)SamplesPerCycle * Math.PI * 2;
            phaseA[index] = phaseARms * rootTwo * Math.Sin(angle);
            phaseB[index] = phaseBRms * rootTwo * Math.Sin(angle - 2 * Math.PI / 3);
            phaseC[index] = phaseCRms * rootTwo * Math.Sin(angle + 2 * Math.PI / 3);
            residual[index] = residualRms * rootTwo * Math.Sin(angle);
        }

        return new WaveformFrame(phaseA, phaseB, phaseC, residual, Frequency, pickupPosition, tripPosition);
    }
}

public sealed record ScenarioStep(MeasurementFrame Measurement, WaveformFrame Waveform);
