using System.Numerics;
using Arvrel.App.Controls;
using Arvrel.Protection;

namespace Arvrel.App.Services;

public sealed class DeterministicLabScenario
{
    public const double Frequency = 50;
    public const int SamplesPerCycle = 80;
    public const int WindowSamples = SamplesPerCycle * 2;
    public const double NominalPhaseVoltage = 63.5;

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
        var phaseAVoltage = NominalPhaseVoltage - 48.5 * ramp;
        var trust = SmvDegraded
            ? SmvTrustState.TripBlocked("SMPCNT_GAP", "Repeated sample-counter discontinuity exceeds the active trip policy.")
            : SmvTrustState.Healthy;

        var phasors = BuildPhasors(phaseA, phaseB, phaseC, residual, phaseAVoltage);
        var frame = new MeasurementFrame(_timestamp, phaseA, phaseB, phaseC, residual, trust, phasors);
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

    private static PhasorMeasurementSet BuildPhasors(
        double phaseA,
        double phaseB,
        double phaseC,
        double residual,
        double phaseAVoltage)
    {
        var ia = Polar(phaseA, FaultAngle(phaseA));
        var ib = Polar(phaseB, -120);
        var ic = Polar(phaseC, 120);
        var va = Polar(phaseAVoltage, 0);
        var vb = Polar(NominalPhaseVoltage, -120);
        var vc = Polar(NominalPhaseVoltage, 120);
        var residualVoltage = va + vb + vc;
        var residualCurrentAngle = residualVoltage.Magnitude > 0.001
            ? residualVoltage.Phase * 180 / Math.PI - 45
            : 0;

        return new PhasorMeasurementSet(
            ia,
            ib,
            ic,
            Polar(residual, residualCurrentAngle),
            va,
            vb,
            vc,
            residualVoltage,
            Frequency);
    }

    private static double FaultAngle(double phaseA)
        => phaseA > 1.2 ? 45 : 0;

    private static Complex Polar(double magnitude, double angleDegrees)
        => Complex.FromPolarCoordinates(magnitude, angleDegrees * Math.PI / 180);

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
