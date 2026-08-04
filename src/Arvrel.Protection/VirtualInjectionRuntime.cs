namespace Arvrel.Protection;

public sealed record VirtualInjectionRuntimeSnapshot(
    VirtualInjectionFrame Frame,
    long SampleCounter,
    DateTimeOffset Timestamp,
    DateTimeOffset AppliedAt,
    string WindowStatus)
{
    public bool IsCoherent => string.Equals(WindowStatus, "coherent", StringComparison.Ordinal);
}

public sealed class VirtualInjectionRuntime
{
    private readonly int _samplesPerCycle;
    private readonly int _cycles;
    private readonly double _nominalFrequencyHz;
    private readonly int _counterWrap;
    private DateTimeOffset _timestamp;
    private long _sampleCounter;
    private TimeSpan _coherenceRemaining;
    private VirtualInjectionProfile _activeProfile;

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
        _activeProfile = (initialProfile ?? VirtualInjectionPresets.Create("Normal balanced", nominalFrequencyHz)).Normalize();
        AppliedAt = _timestamp;
    }

    public VirtualInjectionProfile ActiveProfile => _activeProfile;
    public DateTimeOffset AppliedAt { get; private set; }
    public long SampleCounter => _sampleCounter;
    public int SamplesPerCycle => _samplesPerCycle;
    public int Cycles => _cycles;
    public double NominalFrequencyHz => _nominalFrequencyHz;
    public double SampleRateHz => _nominalFrequencyHz * _samplesPerCycle;
    public string WindowStatus => _coherenceRemaining > TimeSpan.Zero ? "rebuilding" : "coherent";
    public string InjectionFingerprint => _activeProfile.Fingerprint();

    public bool Apply(VirtualInjectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Complete validation and normalization before mutating the active state.
        // A rejected edit therefore cannot partially replace the last valid source.
        var normalized = profile.Normalize();
        if (string.Equals(normalized.Fingerprint(), _activeProfile.Fingerprint(), StringComparison.Ordinal))
            return false;

        _activeProfile = normalized;
        AppliedAt = _timestamp;
        _coherenceRemaining = TimeSpan.FromSeconds(1 / _nominalFrequencyHz);
        return true;
    }

    public VirtualInjectionRuntimeSnapshot Advance(TimeSpan delta, bool trustDegraded)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta));

        _timestamp += delta;
        _sampleCounter = (_sampleCounter + (long)Math.Round(delta.TotalSeconds * SampleRateHz)) % _counterWrap;

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
            : _coherenceRemaining > TimeSpan.Zero
                ? new SmvTrustState(
                    true,
                    false,
                    false,
                    "INJECTION_REBUILD",
                    "A new virtual injection profile is rebuilding a complete coherent nominal one-cycle measurement window.")
                : SmvTrustState.Healthy;

        var frame = VirtualInjectionGenerator.Generate(
            _activeProfile,
            _timestamp,
            trust,
            _samplesPerCycle,
            _cycles,
            _nominalFrequencyHz);
        return new VirtualInjectionRuntimeSnapshot(
            frame,
            _sampleCounter,
            _timestamp,
            AppliedAt,
            WindowStatus);
    }

    public void Reset(VirtualInjectionProfile? profile = null)
    {
        _timestamp = DateTimeOffset.UtcNow;
        _sampleCounter = 0;
        _coherenceRemaining = TimeSpan.Zero;
        if (profile is not null)
            _activeProfile = profile.Normalize();
        AppliedAt = _timestamp;
    }
}
