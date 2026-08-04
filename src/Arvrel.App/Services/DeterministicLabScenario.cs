using Arvrel.App.Controls;
using Arvrel.Protection;

namespace Arvrel.App.Services;

public sealed class DeterministicLabScenario
{
    public const double Frequency = 50;
    public const int SamplesPerCycle = 80;
    public const int WindowSamples = SamplesPerCycle * 2;
    public const double NominalPhaseVoltage = 63.5;
    public const double SampleRateHz = Frequency * SamplesPerCycle;

    private readonly VirtualInjectionRuntime _runtime = new(
        VirtualInjectionPresets.Create("Normal balanced"),
        SamplesPerCycle,
        cycles: 2,
        nominalFrequencyHz: Frequency,
        counterWrap: 4000);

    public bool FaultActive
    {
        get => string.Equals(ActiveProfile.Name, "A-G fault", StringComparison.Ordinal);
        set => ApplyProfile(VirtualInjectionPresets.Create(value ? "A-G fault" : "Normal balanced", ActiveProfile.FrequencyHz));
    }

    public bool SmvDegraded { get; set; }
    public long SampleCounter => _runtime.SampleCounter;
    public VirtualInjectionProfile ActiveProfile => _runtime.ActiveProfile;
    public DateTimeOffset AppliedAt => _runtime.AppliedAt;
    public string InjectionFingerprint => _runtime.InjectionFingerprint;
    public string WindowStatus => _runtime.WindowStatus;
    public string NeutralCurrentProvenance => ActiveProfile.ExplicitNeutralCurrent
        ? "explicit-virtual-channel"
        : "calculated-phase-sum";
    public string NeutralVoltageProvenance => ActiveProfile.ExplicitNeutralVoltage
        ? "explicit-virtual-channel"
        : "calculated-phase-sum";

    public bool ApplyProfile(VirtualInjectionProfile profile)
        => _runtime.Apply(profile);

    public bool ApplyPreset(string name)
        => ApplyProfile(VirtualInjectionPresets.Create(name, ActiveProfile.FrequencyHz));

    public ScenarioStep Advance(TimeSpan delta, double pickupPosition, double tripPosition)
    {
        var snapshot = _runtime.Advance(delta, SmvDegraded);
        var injection = snapshot.Frame;
        var waveform = new WaveformFrame(
            injection.PhaseACurrentSamples,
            injection.PhaseBCurrentSamples,
            injection.PhaseCCurrentSamples,
            injection.ResidualCurrentSamples,
            ActiveProfile.FrequencyHz,
            pickupPosition,
            tripPosition);
        return new ScenarioStep(injection.Measurement, waveform);
    }

    public void Restart(bool keepProfile)
    {
        SmvDegraded = false;
        _runtime.Reset(keepProfile
            ? ActiveProfile
            : VirtualInjectionPresets.Create("Normal balanced"));
    }

    public void Reset() => Restart(keepProfile: false);
}

public sealed record ScenarioStep(MeasurementFrame Measurement, WaveformFrame Waveform);
