using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

/// <summary>
/// Owns the deterministic source and protection-engine lifecycle independently
/// from any desktop UI framework.
/// </summary>
public sealed class InternalLabSession
{
    private readonly ProtectionEngine _engine;

    public InternalLabSession(
        ProtectionSettings settings,
        DeterministicLabScenario? scenario = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        _engine = new ProtectionEngine(settings);
        Scenario = scenario ?? new DeterministicLabScenario();
        Snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    public DeterministicLabScenario Scenario { get; }
    public ProtectionSettings Settings => _engine.Settings;
    public ProtectionSnapshot Snapshot { get; private set; }
    public bool IsRunning => Scenario.IsRunning;

    public bool ToggleRunning()
        => SetRunning(!IsRunning);

    public bool SetRunning(bool running)
        => running
            ? Scenario.StartInjection()
            : Scenario.StopInjection();

    /// <summary>
    /// Advances one deterministic source frame and evaluates the protection engine.
    /// This method is also used to render the forced-zero output after STOP.
    /// </summary>
    public InternalLabTick Step(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta));

        var scenarioStep = Scenario.Advance(delta);
        Snapshot = _engine.Evaluate(scenarioStep.Measurement);
        return new InternalLabTick(scenarioStep, Snapshot);
    }

    /// <summary>
    /// Advances a scheduled batch only while the source is running.
    /// </summary>
    public IReadOnlyList<InternalLabTick> Advance(TimeSpan substep, int iterations)
    {
        if (substep < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(substep));
        if (iterations < 1 || iterations > 10_000)
            throw new ArgumentOutOfRangeException(nameof(iterations));
        if (!IsRunning)
            return Array.Empty<InternalLabTick>();

        var ticks = new InternalLabTick[iterations];
        for (var index = 0; index < iterations; index++)
            ticks[index] = Step(substep);

        return ticks;
    }

    public InternalLabTick CaptureFrame()
    {
        var scenarioStep = Scenario.Advance(TimeSpan.Zero);
        return new InternalLabTick(scenarioStep, Snapshot);
    }

    public void ApplySettings(ProtectionSettings settings, bool keepTripLatch = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        _engine.UpdateSettings(settings, keepTripLatch);
        Scenario.Reset();
        Snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Clears protection timers and the trip latch without changing the configured
    /// profile or source run state.
    /// </summary>
    public void ResetProtection()
    {
        _engine.Reset();
        Snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    public void Reset(bool keepProfile = false)
    {
        _engine.Reset();
        Scenario.Restart(keepProfile);
        Snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }
}

public sealed record InternalLabTick(
    ScenarioStep Scenario,
    ProtectionSnapshot Protection);
