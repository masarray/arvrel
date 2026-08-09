namespace Arvrel.Protection;

public sealed partial class VirtualInjectionRuntime
{
    /// <summary>
    /// Starts a direct virtual secondary-injection output without applying the
    /// process-bus one-cycle coherence interlock used by <see cref="Start"/>.
    ///
    /// A physical secondary injector applies its waveform at the relay terminals
    /// immediately; relay acquisition/filter delay belongs to the relay front end,
    /// not to the test-set source. CT state and source phase still begin from the
    /// configured event origin at t=0.
    /// </summary>
    public bool StartDirectSecondaryInjection()
    {
        if (_isRunning)
            return false;

        _isRunning = true;
        _sourceSampleIndex = 0;
        _ctRuntimeState = CreateInitialCtRuntimeState(_configuredProfile);
        OutputStateChangedAt = _timestamp;
        _coherenceRemaining = TimeSpan.Zero;
        return true;
    }
}
