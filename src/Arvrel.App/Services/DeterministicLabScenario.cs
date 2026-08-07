using Arvrel.App.Controls;
using Arvrel.Protection;
using CoreScenario = Arvrel.Application.Laboratory.DeterministicLabScenario;

namespace Arvrel.App.Services;

/// <summary>
/// WPF presentation adapter for the platform-neutral deterministic laboratory.
/// The simulation and measurement lifecycle lives in Arvrel.Application; this
/// adapter only converts waveform data into the existing WPF control model.
/// </summary>
public sealed class DeterministicLabScenario
{
    public const double Frequency = CoreScenario.Frequency;
    public const int SamplesPerCycle = CoreScenario.SamplesPerCycle;
    public const int WindowSamples = CoreScenario.WindowSamples;
    public const double NominalPhaseVoltage = CoreScenario.NominalPhaseVoltage;
    public const double SampleRateHz = CoreScenario.SampleRateHz;

    private readonly CoreScenario _inner = new();

    public bool FaultActive
    {
        get => _inner.FaultActive;
        set => _inner.FaultActive = value;
    }

    public bool SmvDegraded
    {
        get => _inner.SmvDegraded;
        set => _inner.SmvDegraded = value;
    }

    public bool IsRunning => _inner.IsRunning;
    public long SampleCounter => _inner.SampleCounter;
    public long SourceSampleIndex => _inner.SourceSampleIndex;
    public CtSaturationRuntimeState CurrentTransformerState => _inner.CurrentTransformerState;
    public VirtualInjectionProfile ActiveProfile => _inner.ActiveProfile;
    public VirtualInjectionProfile OutputProfile => _inner.OutputProfile;
    public DateTimeOffset AppliedAt => _inner.AppliedAt;
    public DateTimeOffset OutputStateChangedAt => _inner.OutputStateChangedAt;
    public string InjectionFingerprint => _inner.InjectionFingerprint;
    public string OutputFingerprint => _inner.OutputFingerprint;
    public string WindowStatus => _inner.WindowStatus;
    public string OutputState => _inner.OutputState;
    public string NeutralCurrentProvenance => _inner.NeutralCurrentProvenance;
    public string NeutralVoltageProvenance => _inner.NeutralVoltageProvenance;

    public bool ApplyProfile(VirtualInjectionProfile profile)
        => _inner.ApplyProfile(profile);

    public bool ApplyPreset(string name)
        => _inner.ApplyPreset(name);

    public bool StartInjection() => _inner.StartInjection();

    public bool StopInjection() => _inner.StopInjection();

    public bool ResetCurrentTransformerState()
        => _inner.ResetCurrentTransformerState();

    public bool DemagnetizeCurrentTransformer()
        => _inner.DemagnetizeCurrentTransformer();

    public ScenarioStep Advance(TimeSpan delta, double pickupPosition, double tripPosition)
    {
        var step = _inner.Advance(delta);
        var waveform = new WaveformFrame(
            step.Waveform.PhaseA,
            step.Waveform.PhaseB,
            step.Waveform.PhaseC,
            step.Waveform.Residual,
            step.Waveform.FrequencyHz,
            pickupPosition,
            tripPosition)
        {
            SampleRateHz = step.Waveform.SampleRateHz,
            NominalSamplesPerCycle = step.Waveform.SamplesPerCycle
        };
        var referenceProfile = step.IsRunning ? _inner.ActiveProfile : _inner.OutputProfile;
        var idealReference = VirtualInjectionReferenceGenerator.GenerateCurrents(
            referenceProfile,
            step.SourceSampleIndex,
            step.Waveform.PhaseA.Length,
            step.Waveform.SampleRateHz);

        return new ScenarioStep(step.Measurement, waveform)
        {
            CtSaturation = step.CtSaturation,
            CurrentTransformerState = step.CurrentTransformerState,
            SourceSampleIndex = step.SourceSampleIndex,
            IsRunning = step.IsRunning,
            IdealCurrentReference = idealReference
        };
    }

    public void Restart(bool keepProfile)
        => _inner.Restart(keepProfile);

    public void Reset()
        => _inner.Reset();
}

public sealed record ScenarioStep(MeasurementFrame Measurement, WaveformFrame Waveform)
{
    public CtSaturationFrameDiagnostics CtSaturation { get; init; } = CtSaturationFrameDiagnostics.Disabled;
    public CtSaturationRuntimeState CurrentTransformerState { get; init; } = CtSaturationRuntimeState.Empty;
    public long SourceSampleIndex { get; init; }
    public bool IsRunning { get; init; }
    public VirtualInjectionCurrentReference IdealCurrentReference { get; init; } = new(
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        0,
        DeterministicLabScenario.SampleRateHz,
        DeterministicLabScenario.Frequency);
}
