namespace Arvrel.Protection;

public sealed class ProtectionEngine
{
    private readonly ProtectionSettings _settings;
    private TimeSpan _phase50Elapsed;
    private TimeSpan _earth50Elapsed;
    private double _phase51Progress;
    private double _earth51Progress;
    private DateTimeOffset? _previousTimestamp;
    private bool _tripLatched;

    public ProtectionEngine(ProtectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public ProtectionSnapshot Evaluate(MeasurementFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame.SmvTrust);

        var delta = ResolveDelta(frame.Timestamp);
        if (!frame.SmvTrust.AllowsMeasurement)
        {
            ResetDynamicState(keepTripLatch: true);
            return CreateUnavailableSnapshot(frame, "Measurement blocked by SMV trust policy.");
        }

        var maxPhase = Math.Max(0, frame.MaximumPhase);
        var residual = Math.Max(0, frame.Residual);

        var phase50Pickup = frame.SmvTrust.AllowsPickup && maxPhase >= _settings.PhaseInstantaneousPickupA;
        var phase51Pickup = frame.SmvTrust.AllowsPickup && maxPhase >= _settings.PhaseTimePickupA;
        var earth50Pickup = frame.SmvTrust.AllowsPickup && residual >= _settings.EarthInstantaneousPickupA;
        var earth51Pickup = frame.SmvTrust.AllowsPickup && residual >= _settings.EarthTimePickupA;

        _phase50Elapsed = AccumulateDefiniteTime(
            _phase50Elapsed,
            phase50Pickup,
            delta,
            maxPhase < _settings.PhaseInstantaneousPickupA * _settings.DropoutRatio);
        _earth50Elapsed = AccumulateDefiniteTime(
            _earth50Elapsed,
            earth50Pickup,
            delta,
            residual < _settings.EarthInstantaneousPickupA * _settings.DropoutRatio);

        _phase51Progress = AccumulateIecStandardInverse(
            _phase51Progress,
            maxPhase,
            _settings.PhaseTimePickupA,
            _settings.PhaseTimeMultiplier,
            delta,
            phase51Pickup);
        _earth51Progress = AccumulateIecStandardInverse(
            _earth51Progress,
            residual,
            _settings.EarthTimePickupA,
            _settings.EarthTimeMultiplier,
            delta,
            earth51Pickup);

        var phase50Operate = _phase50Elapsed >= _settings.PhaseInstantaneousDelay;
        var earth50Operate = _earth50Elapsed >= _settings.EarthInstantaneousDelay;
        var phase51Operate = _phase51Progress >= 1;
        var earth51Operate = _earth51Progress >= 1;

        var operationRequested = phase50Operate || phase51Operate || earth50Operate || earth51Operate;
        var blocked = operationRequested && !frame.SmvTrust.AllowsTrip;
        var tripRequested = operationRequested && frame.SmvTrust.AllowsTrip;
        if (tripRequested)
            _tripLatched = true;

        var phaseA = frame.PhaseA >= Math.Min(_settings.PhaseInstantaneousPickupA, _settings.PhaseTimePickupA);
        var phaseB = frame.PhaseB >= Math.Min(_settings.PhaseInstantaneousPickupA, _settings.PhaseTimePickupA);
        var phaseC = frame.PhaseC >= Math.Min(_settings.PhaseInstantaneousPickupA, _settings.PhaseTimePickupA);
        var earth = earth50Pickup || earth51Pickup;

        var activeElement = ResolveActiveElement(
            phase50Operate,
            earth50Operate,
            phase51Operate,
            earth51Operate,
            phase50Pickup,
            earth50Pickup,
            phase51Pickup,
            earth51Pickup);

        var decisionReason = blocked
            ? $"TRIP BLOCKED · {frame.SmvTrust.Code} · {frame.SmvTrust.Detail}"
            : _tripLatched
                ? $"TRIP LATCHED · {activeElement}"
                : activeElement == "READY"
                    ? "Measurements stable · no pickup"
                    : $"OPERATING · {activeElement}";

        return new ProtectionSnapshot(
            frame.Timestamp,
            BuildDefiniteSnapshot(
                ProtectionElementId.PhaseInstantaneous50P,
                phase50Pickup,
                phase50Operate,
                blocked && phase50Operate,
                _phase50Elapsed,
                _settings.PhaseInstantaneousDelay,
                maxPhase,
                _settings.PhaseInstantaneousPickupA),
            BuildInverseSnapshot(
                ProtectionElementId.PhaseTime51P,
                phase51Pickup,
                phase51Operate,
                blocked && phase51Operate,
                _phase51Progress,
                maxPhase,
                _settings.PhaseTimePickupA),
            BuildDefiniteSnapshot(
                ProtectionElementId.EarthInstantaneous50N,
                earth50Pickup,
                earth50Operate,
                blocked && earth50Operate,
                _earth50Elapsed,
                _settings.EarthInstantaneousDelay,
                residual,
                _settings.EarthInstantaneousPickupA),
            BuildInverseSnapshot(
                ProtectionElementId.EarthTime51N,
                earth51Pickup,
                earth51Operate,
                blocked && earth51Operate,
                _earth51Progress,
                residual,
                _settings.EarthTimePickupA),
            phaseA,
            phaseB,
            phaseC,
            earth,
            tripRequested,
            _tripLatched,
            blocked,
            activeElement,
            decisionReason,
            frame.SmvTrust);
    }

    public void Reset()
    {
        ResetDynamicState(keepTripLatch: false);
        _previousTimestamp = null;
    }

    private TimeSpan ResolveDelta(DateTimeOffset timestamp)
    {
        var delta = _previousTimestamp is null ? TimeSpan.Zero : timestamp - _previousTimestamp.Value;
        _previousTimestamp = timestamp;
        return delta < TimeSpan.Zero || delta > TimeSpan.FromSeconds(1) ? TimeSpan.Zero : delta;
    }

    private void ResetDynamicState(bool keepTripLatch)
    {
        _phase50Elapsed = TimeSpan.Zero;
        _earth50Elapsed = TimeSpan.Zero;
        _phase51Progress = 0;
        _earth51Progress = 0;
        if (!keepTripLatch)
            _tripLatched = false;
    }

    private ProtectionSnapshot CreateUnavailableSnapshot(MeasurementFrame frame, string reason)
    {
        var ready = ProtectionSnapshot.Ready(frame.Timestamp);
        return ready with
        {
            DecisionReason = $"MEASUREMENT BLOCKED · {frame.SmvTrust.Code} · {reason}",
            Blocked = true,
            ActiveElement = "SMV BLOCK",
            SmvTrust = frame.SmvTrust,
            TripLatched = _tripLatched
        };
    }

    private static ElementSnapshot BuildDefiniteSnapshot(
        ProtectionElementId element,
        bool pickup,
        bool operated,
        bool blocked,
        TimeSpan elapsed,
        TimeSpan delay,
        double quantity,
        double setting)
    {
        var progress = delay <= TimeSpan.Zero ? (pickup ? 1 : 0) : elapsed.TotalMilliseconds / delay.TotalMilliseconds;
        return new ElementSnapshot(
            element,
            ResolveStageState(pickup, operated, blocked),
            pickup,
            operated,
            Math.Clamp(progress, 0, 1),
            quantity,
            setting,
            blocked ? "Operation reached but trip permission is blocked." : pickup ? "Definite-time accumulator active." : "Ready");
    }

    private static ElementSnapshot BuildInverseSnapshot(
        ProtectionElementId element,
        bool pickup,
        bool operated,
        bool blocked,
        double progress,
        double quantity,
        double setting)
    {
        return new ElementSnapshot(
            element,
            ResolveStageState(pickup, operated, blocked),
            pickup,
            operated,
            Math.Clamp(progress, 0, 1),
            quantity,
            setting,
            blocked ? "Inverse operation reached but trip permission is blocked." : pickup ? "IEC standard-inverse integration active." : "Ready");
    }

    private static ProtectionStageState ResolveStageState(bool pickup, bool operated, bool blocked)
    {
        if (blocked)
            return ProtectionStageState.Blocked;
        if (operated)
            return ProtectionStageState.Operated;
        if (pickup)
            return ProtectionStageState.Timing;
        return ProtectionStageState.Ready;
    }

    private static TimeSpan AccumulateDefiniteTime(TimeSpan current, bool pickup, TimeSpan delta, bool droppedOut)
    {
        if (pickup)
            return current + delta;
        return droppedOut ? TimeSpan.Zero : current;
    }

    private static double AccumulateIecStandardInverse(
        double current,
        double measured,
        double pickup,
        double timeMultiplier,
        TimeSpan delta,
        bool started)
    {
        if (!started || pickup <= 0 || measured <= pickup)
            return Math.Max(0, current - delta.TotalSeconds * 2);

        var multiple = measured / pickup;
        var denominator = Math.Pow(multiple, 0.02) - 1;
        if (denominator <= 0.000001)
            return current;

        var operatingSeconds = timeMultiplier * 0.14 / denominator;
        operatingSeconds = Math.Clamp(operatingSeconds, 0.02, 120);
        return current + delta.TotalSeconds / operatingSeconds;
    }

    private static string ResolveActiveElement(
        bool phase50Operate,
        bool earth50Operate,
        bool phase51Operate,
        bool earth51Operate,
        bool phase50Pickup,
        bool earth50Pickup,
        bool phase51Pickup,
        bool earth51Pickup)
    {
        if (phase50Operate) return "50P-1";
        if (earth50Operate) return "50N";
        if (phase51Operate) return "51P";
        if (earth51Operate) return "51N";
        if (phase50Pickup) return "50P-1 PICKUP";
        if (earth50Pickup) return "50N PICKUP";
        if (phase51Pickup) return "51P START";
        if (earth51Pickup) return "51N START";
        return "READY";
    }
}
