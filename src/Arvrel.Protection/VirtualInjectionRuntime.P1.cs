namespace Arvrel.Protection;

public sealed partial class VirtualInjectionRuntime
{
    /// <summary>
    /// Applies a live source-state change without rebuilding the measurement authority.
    /// This is intended for deterministic test-set workflows such as ramps and state
    /// sequences where amplitude/angle changes are part of the injected waveform, not
    /// a configuration lifecycle event. Phase/sample continuity and CT numerical state
    /// are preserved when frequency and CT model identity are unchanged.
    /// </summary>
    public bool ApplyContinuous(VirtualInjectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var normalized = profile.Normalize();
        if (string.Equals(normalized.Fingerprint(), _configuredProfile.Fingerprint(), StringComparison.Ordinal))
            return false;

        if (!_isRunning || !HasSameCtRuntimeIdentity(_configuredProfile, normalized))
            return Apply(normalized);

        _configuredProfile = normalized;
        AppliedAt = _timestamp;
        return true;
    }
}
