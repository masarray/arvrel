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
    public bool IsRunning { get; private set; }

    public bool ToggleRunning()
    {
        IsRunning = !IsRunning;
        return IsRunning;
    }

    public bool SetRunning(bool running)
    {
        if (IsRunning == running)
            return false;

        IsRunning = running;
        return true;
    }

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
        {
            var scenarioStep = Scenario.Advance(substep);
            Snapshot = _engine.Evaluate(scenarioStep.Measurement);
            ticks[index] = new InternalLabTick(scenarioStep, Snapshot);
        }

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
        IsRunning = false;
    }

    public void Reset(bool keepProfile = false)
    {
        _engine.Reset();
        Scenario.Restart(keepProfile);
        Snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
        IsRunning = false;
    }
}

public sealed record InternalLabTick(
    ScenarioStep Scenario,
    ProtectionSnapshot Protection);
