using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

/// <summary>
/// Platform-neutral deterministic IEC 61850 Sampled Values laboratory source.
/// This type deliberately exposes measurement and waveform data only; presentation
/// frameworks are responsible for adapting the data into their own controls.
/// </summary>
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
        set => ApplySourcePresetPreservingCt(value ? "A-G fault" : "Normal balanced");
    }

    public bool SmvDegraded { get; set; }
    public bool IsRunning => _runtime.IsRunning;
    public long SampleCounter => _runtime.SampleCounter;
    public long SourceSampleIndex => _runtime.SourceSampleIndex;
    public CtSaturationRuntimeState CurrentTransformerState => _runtime.CurrentTransformerState;
    public VirtualInjectionProfile ActiveProfile => _runtime.ActiveProfile;
    public VirtualInjectionProfile OutputProfile => _runtime.OutputProfile;
    public DateTimeOffset AppliedAt => _runtime.AppliedAt;
    public DateTimeOffset OutputStateChangedAt => _runtime.OutputStateChangedAt;
    public string InjectionFingerprint => _runtime.InjectionFingerprint;
    public string OutputFingerprint => _runtime.OutputFingerprint;
    public string WindowStatus => _runtime.WindowStatus;
    public string OutputState => _runtime.OutputState;
    public string NeutralCurrentProvenance => ActiveProfile.ExplicitNeutralCurrent
        ? "explicit-virtual-channel"
        : "calculated-phase-sum";
    public string NeutralVoltageProvenance => ActiveProfile.ExplicitNeutralVoltage
        ? "explicit-virtual-channel"
        : "calculated-phase-sum";

    public bool ApplyProfile(VirtualInjectionProfile profile)
        => _runtime.Apply(profile);

    public bool ApplyContinuousProfile(VirtualInjectionProfile profile)
        => _runtime.ApplyContinuous(profile);

    public bool ApplyPreset(string name)
        => VirtualInjectionCtStudyScenarios.Contains(name)
            ? ApplyProfile(VirtualInjectionCtStudyScenarios.Create(name, ActiveProfile.FrequencyHz))
            : ApplySourcePresetPreservingCt(name);

    public bool StartInjection() => _runtime.Start();

    public bool StopInjection() => _runtime.Stop();

    public bool RestartEvent() => _runtime.RestartEvent();

    public bool ResetCurrentTransformerState()
        => _runtime.ResetCurrentTransformerState(demagnetize: false);

    public bool DemagnetizeCurrentTransformer()
        => _runtime.ResetCurrentTransformerState(demagnetize: true);

    public ScenarioStep Advance(TimeSpan delta)
    {
        var snapshot = _runtime.Advance(delta, SmvDegraded);
        var injection = snapshot.Frame;

        // The one-cycle runtime coherence window is source-side presentation state,
        // not a permission signal from a physical secondary injector to the relay.
        // A CMC-style source applies waveform samples immediately at START; any
        // recognition/filter delay must therefore belong to the relay front end and
        // protection algorithm. Preserve genuine SMV degradation blocking, but do not
        // suppress pickup/trip merely because the virtual source UI says STARTING.
        var measurement = snapshot.IsRunning &&
                          !SmvDegraded &&
                          string.Equals(injection.Measurement.SmvTrust.Code, "INJECTION_REBUILD", StringComparison.Ordinal)
            ? injection.Measurement with { SmvTrust = SmvTrustState.Healthy }
            : injection.Measurement;

        var waveform = new ScenarioWaveform(
            injection.PhaseACurrentSamples,
            injection.PhaseBCurrentSamples,
            injection.PhaseCCurrentSamples,
            injection.ResidualCurrentSamples,
            ActiveProfile.FrequencyHz,
            injection.SamplesPerCycle,
            injection.SampleRateHz)
        {
            PhaseAVoltage = injection.PhaseAVoltageSamples,
            PhaseBVoltage = injection.PhaseBVoltageSamples,
            PhaseCVoltage = injection.PhaseCVoltageSamples,
            ResidualVoltage = injection.ResidualVoltageSamples
        };
        return new ScenarioStep(measurement, waveform)
        {
            CtSaturation = injection.CtSaturation,
            CurrentTransformerState = snapshot.CurrentTransformerState,
            SourceSampleIndex = snapshot.SourceSampleIndex,
            IsRunning = snapshot.IsRunning
        };
    }

    public void Restart(bool keepProfile)
    {
        SmvDegraded = false;
        _runtime.Reset(keepProfile
            ? ActiveProfile
            : VirtualInjectionPresets.Create("Normal balanced"));
    }

    public void Reset() => Restart(keepProfile: false);

    private bool ApplySourcePresetPreservingCt(string name)
    {
        var source = VirtualInjectionPresets.Create(name, ActiveProfile.FrequencyHz);
        return ApplyProfile((source with
        {
            CurrentTransformer = ActiveProfile.CurrentTransformer
        }).Normalize());
    }
}

public sealed record ScenarioWaveform(
    double[] PhaseA,
    double[] PhaseB,
    double[] PhaseC,
    double[] Residual,
    double FrequencyHz,
    int SamplesPerCycle,
    double SampleRateHz)
{
    public double[] PhaseAVoltage { get; init; } = Array.Empty<double>();
    public double[] PhaseBVoltage { get; init; } = Array.Empty<double>();
    public double[] PhaseCVoltage { get; init; } = Array.Empty<double>();
    public double[] ResidualVoltage { get; init; } = Array.Empty<double>();
}

public sealed record ScenarioStep(
    MeasurementFrame Measurement,
    ScenarioWaveform Waveform)
{
    public CtSaturationFrameDiagnostics CtSaturation { get; init; } = CtSaturationFrameDiagnostics.Disabled;
    public CtSaturationRuntimeState CurrentTransformerState { get; init; } = CtSaturationRuntimeState.Empty;
    public long SourceSampleIndex { get; init; }
    public bool IsRunning { get; init; }
}
