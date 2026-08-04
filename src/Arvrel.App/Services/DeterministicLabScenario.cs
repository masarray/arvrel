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

    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private long _sampleCounter;
    private TimeSpan _coherenceRemaining;
    private VirtualInjectionProfile _activeProfile = VirtualInjectionPresets.Create("Normal balanced");

    public bool FaultActive
    {
        get => string.Equals(_activeProfile.Name, "A-G fault", StringComparison.Ordinal);
        set => ApplyProfile(VirtualInjectionPresets.Create(value ? "A-G fault" : "Normal balanced", _activeProfile.FrequencyHz));
    }

    public bool SmvDegraded { get; set; }
    public long SampleCounter => _sampleCounter;
    public VirtualInjectionProfile ActiveProfile => _activeProfile;
    public DateTimeOffset AppliedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string InjectionFingerprint => _activeProfile.Fingerprint();
    public string WindowStatus => _coherenceRemaining > TimeSpan.Zero ? "rebuilding" : "coherent";
    public string NeutralCurrentProvenance => _activeProfile.ExplicitNeutralCurrent
        ? "explicit-virtual-channel"
        : "calculated-phase-sum";
    public string NeutralVoltageProvenance => _activeProfile.ExplicitNeutralVoltage
        ? "explicit-virtual-channel"
        : "calculated-phase-sum";

    public bool ApplyProfile(VirtualInjectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalized = profile.Normalize();
        if (string.Equals(normalized.Fingerprint(), _activeProfile.Fingerprint(), StringComparison.Ordinal))
            return false;

        _activeProfile = normalized;
        AppliedAt = _timestamp;
        _coherenceRemaining = TimeSpan.FromSeconds(1 / Frequency);
        return true;
    }

    public bool ApplyPreset(string name)
        => ApplyProfile(VirtualInjectionPresets.Create(name, _activeProfile.FrequencyHz));

    public ScenarioStep Advance(TimeSpan delta, double pickupPosition, double tripPosition)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta));

        _timestamp += delta;
        _sampleCounter = (_sampleCounter + (long)Math.Round(delta.TotalSeconds * SampleRateHz)) % 4000;

        if (delta > TimeSpan.Zero && _coherenceRemaining > TimeSpan.Zero)
        {
            _coherenceRemaining -= delta;
            if (_coherenceRemaining < TimeSpan.Zero)
                _coherenceRemaining = TimeSpan.Zero;
        }

        var trust = SmvDegraded
            ? SmvTrustState.TripBlocked(
                "SMPCNT_GAP",
                "Repeated sample-counter discontinuity exceeds the active trip policy.")
            : _coherenceRemaining > TimeSpan.Zero
                ? new SmvTrustState(
                    true,
                    false,
                    false,
                    "INJECTION_REBUILD",
                    "A new virtual injection profile is rebuilding a complete coherent nominal one-cycle measurement window.")
                : SmvTrustState.Healthy;

        var injection = VirtualInjectionGenerator.Generate(
            _activeProfile,
            _timestamp,
            trust,
            SamplesPerCycle,
            cycles: 2,
            nominalFrequencyHz: Frequency);
        var waveform = new WaveformFrame(
            injection.PhaseACurrentSamples,
            injection.PhaseBCurrentSamples,
            injection.PhaseCCurrentSamples,
            injection.ResidualCurrentSamples,
            _activeProfile.FrequencyHz,
            pickupPosition,
            tripPosition);
        return new ScenarioStep(injection.Measurement, waveform);
    }

    public void Restart(bool keepProfile)
    {
        _timestamp = DateTimeOffset.UtcNow;
        _sampleCounter = 0;
        SmvDegraded = false;
        if (!keepProfile)
            _activeProfile = VirtualInjectionPresets.Create("Normal balanced");
        AppliedAt = _timestamp;
        _coherenceRemaining = TimeSpan.Zero;
    }

    public void Reset() => Restart(keepProfile: false);
}

public sealed record ScenarioStep(MeasurementFrame Measurement, WaveformFrame Waveform);
