namespace Arvrel.Protection;

public sealed record VirtualInjectionRuntimeSnapshot(
    VirtualInjectionFrame Frame,
    VirtualInjectionProfile ConfiguredProfile,
    long SampleCounter,
    DateTimeOffset Timestamp,
    DateTimeOffset AppliedAt,
    DateTimeOffset OutputStateChangedAt,
    string WindowStatus,
    bool IsRunning)
{
    public CtSaturationRuntimeState CurrentTransformerState { get; init; } = CtSaturationRuntimeState.Empty;
    public long SourceSampleIndex { get; init; }

    public bool IsCoherent => IsRunning && string.Equals(WindowStatus, "coherent", StringComparison.Ordinal);
    public string OutputState => IsRunning ? (IsCoherent ? "running" : "starting") : "stopped";
}

public sealed class VirtualInjectionRuntime
{
    private const int StateAdvanceChunkSamples = 4_096;

    private readonly int _samplesPerCycle;
    private readonly int _cycles;
    private readonly double _nominalFrequencyHz;
    private readonly int _counterWrap;
    private DateTimeOffset _timestamp;
    private long _sampleCounter;
    private TimeSpan _coherenceRemaining;
    private VirtualInjectionProfile _configuredProfile;
    private CtSaturationRuntimeState _ctRuntimeState = CtSaturationRuntimeState.Empty;
    private long _sourceSampleIndex;
    private bool _isRunning;

    public VirtualInjectionRuntime(
        VirtualInjectionProfile? initialProfile = null,
        int samplesPerCycle = 80,
        int cycles = 2,
        double nominalFrequencyHz = 50,
        int counterWrap = 4000,
        DateTimeOffset? initialTimestamp = null)
    {
        if (samplesPerCycle < 4)
            throw new ArgumentOutOfRangeException(nameof(samplesPerCycle));
        if (cycles < 1 || cycles > 20)
            throw new ArgumentOutOfRangeException(nameof(cycles));
        if (!double.IsFinite(nominalFrequencyHz) || nominalFrequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(nominalFrequencyHz));
        if (counterWrap < 2)
            throw new ArgumentOutOfRangeException(nameof(counterWrap));

        _samplesPerCycle = samplesPerCycle;
        _cycles = cycles;
        _nominalFrequencyHz = nominalFrequencyHz;
        _counterWrap = counterWrap;
        _timestamp = initialTimestamp ?? DateTimeOffset.UtcNow;
        _configuredProfile = (initialProfile ?? VirtualInjectionPresets.Create("Normal balanced", nominalFrequencyHz)).Normalize();
        AppliedAt = _timestamp;
        OutputStateChangedAt = _timestamp;
    }

    public VirtualInjectionProfile ActiveProfile => _configuredProfile;
    public VirtualInjectionProfile OutputProfile => _isRunning
        ? _configuredProfile
        : CreateZeroOutputProfile(_configuredProfile);
    public DateTimeOffset AppliedAt { get; private set; }
    public DateTimeOffset OutputStateChangedAt { get; private set; }
    public long SampleCounter => _sampleCounter;
    public long SourceSampleIndex => _sourceSampleIndex;
    public CtSaturationRuntimeState CurrentTransformerState => _ctRuntimeState;
    public int SamplesPerCycle => _samplesPerCycle;
    public int Cycles => _cycles;
    public double NominalFrequencyHz => _nominalFrequencyHz;
    public double SampleRateHz => _nominalFrequencyHz * _samplesPerCycle;
    public bool IsRunning => _isRunning;
    public string WindowStatus => !_isRunning
        ? "stopped"
        : _coherenceRemaining > TimeSpan.Zero
            ? "rebuilding"
            : "coherent";
    public string OutputState => !_isRunning
        ? "stopped"
        : _coherenceRemaining > TimeSpan.Zero
            ? "starting"
            : "running";
    public string InjectionFingerprint => _configuredProfile.Fingerprint();
    public string OutputFingerprint => OutputProfile.Fingerprint();

    public bool Apply(VirtualInjectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Validate and normalize the complete configured source before mutation.
        // When the output is stopped this only changes the armed values; generated
        // voltage and current remain zero until Start is explicitly requested.
        var normalized = profile.Normalize();
        if (string.Equals(normalized.Fingerprint(), _configuredProfile.Fingerprint(), StringComparison.Ordinal))
            return false;

        // A source-event change restarts phase/DC time at t=0. Magnetic state is
        // retained only when the physical CT model and signal frequency are unchanged.
        // Replacing CT parameters represents a new transformer and resets its state.
        var preserveCtState = _isRunning && HasSameCtRuntimeIdentity(_configuredProfile, normalized);
        _configuredProfile = normalized;
        _sourceSampleIndex = 0;
        if (!preserveCtState)
        {
            _ctRuntimeState = _isRunning
                ? CreateInitialCtRuntimeState(normalized)
                : CtSaturationRuntimeState.Empty;
        }

        AppliedAt = _timestamp;
        if (_isRunning)
            _coherenceRemaining = TimeSpan.FromSeconds(1 / _nominalFrequencyHz);
        return true;
    }

    public bool Start()
    {
        if (_isRunning)
            return false;

        _isRunning = true;
        _sourceSampleIndex = 0;
        _ctRuntimeState = CreateInitialCtRuntimeState(_configuredProfile);
        OutputStateChangedAt = _timestamp;
        _coherenceRemaining = TimeSpan.FromSeconds(1 / _nominalFrequencyHz);
        return true;
    }

    public bool Stop()
    {
        if (!_isRunning)
            return false;

        _isRunning = false;
        _sourceSampleIndex = 0;
        _ctRuntimeState = CtSaturationRuntimeState.Empty;
        OutputStateChangedAt = _timestamp;
        _coherenceRemaining = TimeSpan.Zero;
        return true;
    }

    public VirtualInjectionRuntimeSnapshot Advance(TimeSpan delta, bool trustDegraded)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta));

        _timestamp += delta;
        var elapsedSamples = checked((long)Math.Round(delta.TotalSeconds * SampleRateHz));
        _sampleCounter = (_sampleCounter + elapsedSamples) % _counterWrap;

        if (_isRunning && elapsedSamples > 0)
            AdvanceCurrentTransformerState(elapsedSamples);

        if (delta > TimeSpan.Zero && _coherenceRemaining > TimeSpan.Zero)
        {
            _coherenceRemaining -= delta;
            if (_coherenceRemaining < TimeSpan.Zero)
                _coherenceRemaining = TimeSpan.Zero;
        }

        var trust = trustDegraded
            ? SmvTrustState.TripBlocked(
                "SMPCNT_GAP",
                "Repeated sample-counter discontinuity exceeds the active trip policy.")
            : _isRunning && _coherenceRemaining > TimeSpan.Zero
                ? new SmvTrustState(
                    true,
                    false,
                    false,
                    "INJECTION_REBUILD",
                    "Virtual output has started or changed and is rebuilding one coherent nominal measurement cycle.")
                : SmvTrustState.Healthy;

        var frame = _isRunning
            ? StatefulVirtualInjectionGenerator.GeneratePreview(
                _configuredProfile,
                _timestamp,
                trust,
                _samplesPerCycle,
                _cycles,
                _nominalFrequencyHz,
                _sourceSampleIndex,
                _ctRuntimeState)
            : VirtualInjectionGenerator.Generate(
                OutputProfile,
                _timestamp,
                trust,
                _samplesPerCycle,
                _cycles,
                _nominalFrequencyHz);

        return new VirtualInjectionRuntimeSnapshot(
            frame,
            _configuredProfile,
            _sampleCounter,
            _timestamp,
            AppliedAt,
            OutputStateChangedAt,
            WindowStatus,
            _isRunning)
        {
            CurrentTransformerState = _ctRuntimeState,
            SourceSampleIndex = _sourceSampleIndex
        };
    }

    public void Reset(VirtualInjectionProfile? profile = null)
    {
        _timestamp = DateTimeOffset.UtcNow;
        _sampleCounter = 0;
        _sourceSampleIndex = 0;
        _ctRuntimeState = CtSaturationRuntimeState.Empty;
        _coherenceRemaining = TimeSpan.Zero;
        _isRunning = false;
        if (profile is not null)
            _configuredProfile = profile.Normalize();
        AppliedAt = _timestamp;
        OutputStateChangedAt = _timestamp;
    }

    private void AdvanceCurrentTransformerState(long elapsedSamples)
    {
        while (elapsedSamples > 0)
        {
            var chunk = (int)Math.Min(elapsedSamples, StateAdvanceChunkSamples);
            _ctRuntimeState = StatefulVirtualInjectionGenerator.AdvanceCurrentTransformer(
                _configuredProfile,
                _sourceSampleIndex,
                chunk,
                SampleRateHz,
                _ctRuntimeState);
            _sourceSampleIndex = checked(_sourceSampleIndex + chunk);
            elapsedSamples -= chunk;
        }
    }

    private static bool HasSameCtRuntimeIdentity(
        VirtualInjectionProfile previous,
        VirtualInjectionProfile next)
        => previous.FrequencyHz.Equals(next.FrequencyHz) &&
           Equals(previous.CurrentTransformer, next.CurrentTransformer);

    private static CtSaturationRuntimeState CreateInitialCtRuntimeState(
        VirtualInjectionProfile profile)
        => CtSaturationRuntimeState.CreateInitial(
            profile.CurrentTransformer,
            profile.FrequencyHz,
            profile.ExplicitNeutralCurrent);

    private static VirtualInjectionProfile CreateZeroOutputProfile(VirtualInjectionProfile configured)
        => new(
            $"{configured.Name} · output stopped",
            configured.FrequencyHz,
            Zero(configured.PhaseAVoltage),
            Zero(configured.PhaseBVoltage),
            Zero(configured.PhaseCVoltage),
            Zero(configured.NeutralVoltage),
            Zero(configured.PhaseACurrent),
            Zero(configured.PhaseBCurrent),
            Zero(configured.PhaseCCurrent),
            Zero(configured.NeutralCurrent))
        {
            // STOP is an absolute virtual-output interlock. Configured CT parameters
            // stay armed in ActiveProfile, while output and magnetic runtime state are
            // cleared. The next Start deterministically reapplies configured remanence.
            CurrentTransformer = configured.CurrentTransformer with
            {
                Enabled = false,
                RemanencePercent = 0
            }
        };

    private static VirtualInjectionChannel Zero(VirtualInjectionChannel channel)
        => channel with
        {
            Rms = 0,
            AngleDegrees = 0,
            DcOffsetPercent = 0
        };
}
