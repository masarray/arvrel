namespace Arvrel.Protection;

public sealed class ProtectionEngine
{
    private ProtectionSettings _settings;
    private readonly FeederProtectionEngine _feederEngine = new();
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

    public ProtectionSettings Settings => _settings;

    public void UpdateSettings(ProtectionSettings settings, bool keepTripLatch = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _settings = settings;
        ResetDynamicState(keepTripLatch);
        _previousTimestamp = null;
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

        var phase50Pickup = _settings.PhaseInstantaneousEnabled &&
                            frame.SmvTrust.AllowsPickup &&
                            maxPhase >= _settings.PhaseInstantaneousPickupA;
        var phase51Pickup = _settings.PhaseTimeEnabled &&
                            frame.SmvTrust.AllowsPickup &&
                            maxPhase >= _settings.PhaseTimePickupA;
        var earth50Pickup = _settings.EarthInstantaneousEnabled &&
                            frame.SmvTrust.AllowsPickup &&
                            residual >= _settings.EarthInstantaneousPickupA;
        var earth51Pickup = _settings.EarthTimeEnabled &&
                            frame.SmvTrust.AllowsPickup &&
                            residual >= _settings.EarthTimePickupA;

        _phase50Elapsed = AccumulateDefiniteTime(
            _phase50Elapsed,
            phase50Pickup,
            delta,
            !_settings.PhaseInstantaneousEnabled ||
            maxPhase < _settings.PhaseInstantaneousPickupA * _settings.PhaseInstantaneousDropoutRatio);
        _earth50Elapsed = AccumulateDefiniteTime(
            _earth50Elapsed,
            earth50Pickup,
            delta,
            !_settings.EarthInstantaneousEnabled ||
            residual < _settings.EarthInstantaneousPickupA * _settings.EarthInstantaneousDropoutRatio);

        _phase51Progress = AccumulateTimeCharacteristic(
            _phase51Progress,
            maxPhase,
            _settings.PhaseTimePickupA,
            _settings.PhaseTimeCurve,
            _settings.PhaseTimeMultiplier,
            _settings.PhaseTimeDefiniteDelay,
            _settings.PhaseTimeMinimumOperateTime,
            _settings.PhaseTimeResetMode,
            _settings.PhaseTimeResetDelay,
            _settings.PhaseTimeUserK,
            _settings.PhaseTimeUserAlpha,
            _settings.PhaseTimeUserC,
            delta,
            phase51Pickup,
            !_settings.PhaseTimeEnabled || maxPhase < _settings.PhaseTimePickupA * _settings.PhaseTimeDropoutRatio);
        _earth51Progress = AccumulateTimeCharacteristic(
            _earth51Progress,
            residual,
            _settings.EarthTimePickupA,
            _settings.EarthTimeCurve,
            _settings.EarthTimeMultiplier,
            _settings.EarthTimeDefiniteDelay,
            _settings.EarthTimeMinimumOperateTime,
            _settings.EarthTimeResetMode,
            _settings.EarthTimeResetDelay,
            _settings.EarthTimeUserK,
            _settings.EarthTimeUserAlpha,
            _settings.EarthTimeUserC,
            delta,
            earth51Pickup,
            !_settings.EarthTimeEnabled || residual < _settings.EarthTimePickupA * _settings.EarthTimeDropoutRatio);

        var phase50Operate = _settings.PhaseInstantaneousEnabled && _phase50Elapsed >= _settings.PhaseInstantaneousDelay;
        var earth50Operate = _settings.EarthInstantaneousEnabled && _earth50Elapsed >= _settings.EarthInstantaneousDelay;
        var phase51Operate = _settings.PhaseTimeEnabled && _phase51Progress >= 1;
        var earth51Operate = _settings.EarthTimeEnabled && _earth51Progress >= 1;

        var feeder = _feederEngine.Evaluate(frame, _settings.Feeder, delta);
        var legacyOperationRequested = phase50Operate || phase51Operate || earth50Operate || earth51Operate;
        var blocked = feeder.Blocked || legacyOperationRequested && !frame.SmvTrust.AllowsTrip;
        var tripRequested = feeder.TripRequested || legacyOperationRequested && frame.SmvTrust.AllowsTrip;
        if (tripRequested)
            _tripLatched = true;

        var minimumPhasePickup = Math.Min(
            _settings.PhaseInstantaneousEnabled ? _settings.PhaseInstantaneousPickupA : double.PositiveInfinity,
            _settings.PhaseTimeEnabled ? _settings.PhaseTimePickupA : double.PositiveInfinity);
        var phaseA = double.IsFinite(minimumPhasePickup) && frame.PhaseA >= minimumPhasePickup;
        var phaseB = double.IsFinite(minimumPhasePickup) && frame.PhaseB >= minimumPhasePickup;
        var phaseC = double.IsFinite(minimumPhasePickup) && frame.PhaseC >= minimumPhasePickup;
        var earth = earth50Pickup || earth51Pickup || feeder.DirectionalEarth67N.Pickup || feeder.ResidualOvervoltage59N.Pickup;

        var legacyActiveElement = ResolveActiveElement(
            phase50Operate,
            earth50Operate,
            phase51Operate,
            earth51Operate,
            phase50Pickup,
            earth50Pickup,
            phase51Pickup,
            earth51Pickup);
        var activeElement = legacyActiveElement == "READY" ? feeder.ActiveElement : legacyActiveElement;

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
                _settings.PhaseInstantaneousEnabled,
                phase50Pickup,
                phase50Operate,
                blocked && phase50Operate,
                _phase50Elapsed,
                _settings.PhaseInstantaneousDelay,
                maxPhase,
                _settings.PhaseInstantaneousPickupA),
            BuildTimeSnapshot(
                ProtectionElementId.PhaseTime51P,
                _settings.PhaseTimeEnabled,
                phase51Pickup,
                phase51Operate,
                blocked && phase51Operate,
                _phase51Progress,
                maxPhase,
                _settings.PhaseTimePickupA,
                _settings.PhaseTimeCurve,
                _settings.PhaseTimeMultiplier,
                _settings.PhaseTimeDefiniteDelay,
                _settings.PhaseTimeMinimumOperateTime,
                _settings.PhaseTimeUserK,
                _settings.PhaseTimeUserAlpha,
                _settings.PhaseTimeUserC),
            BuildDefiniteSnapshot(
                ProtectionElementId.EarthInstantaneous50N,
                _settings.EarthInstantaneousEnabled,
                earth50Pickup,
                earth50Operate,
                blocked && earth50Operate,
                _earth50Elapsed,
                _settings.EarthInstantaneousDelay,
                residual,
                _settings.EarthInstantaneousPickupA),
            BuildTimeSnapshot(
                ProtectionElementId.EarthTime51N,
                _settings.EarthTimeEnabled,
                earth51Pickup,
                earth51Operate,
                blocked && earth51Operate,
                _earth51Progress,
                residual,
                _settings.EarthTimePickupA,
                _settings.EarthTimeCurve,
                _settings.EarthTimeMultiplier,
                _settings.EarthTimeDefiniteDelay,
                _settings.EarthTimeMinimumOperateTime,
                _settings.EarthTimeUserK,
                _settings.EarthTimeUserAlpha,
                _settings.EarthTimeUserC),
            phaseA,
            phaseB,
            phaseC,
            earth,
            tripRequested,
            _tripLatched,
            blocked,
            activeElement,
            decisionReason,
            frame.SmvTrust)
        {
            Feeder = feeder
        };
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
        _feederEngine.Reset();
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
        bool enabled,
        bool pickup,
        bool operated,
        bool blocked,
        TimeSpan elapsed,
        TimeSpan delay,
        double quantity,
        double setting)
    {
        if (!enabled)
            return DisabledElement(element, quantity, setting);
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

    private static ElementSnapshot BuildTimeSnapshot(
        ProtectionElementId element,
        bool enabled,
        bool pickup,
        bool operated,
        bool blocked,
        double progress,
        double quantity,
        double setting,
        IecCurveFamily curve,
        double timeMultiplier,
        TimeSpan definiteDelay,
        TimeSpan minimumOperate,
        double userK,
        double userAlpha,
        double userC)
    {
        if (!enabled)
            return DisabledElement(element, quantity, setting);

        var curveName = IecCurveCalculator.GetParameters(curve, userK, userAlpha, userC).DisplayName;
        var multiple = setting > 0 ? quantity / setting : 0;
        var time = IecCurveCalculator.GetOperateTimeSeconds(
            curve,
            multiple,
            timeMultiplier,
            definiteDelay,
            minimumOperate,
            userK,
            userAlpha,
            userC);
        var timingDetail = double.IsFinite(time)
            ? $"{curveName} integration active · target {time:0.###} s at {multiple:0.##} × pickup."
            : $"{curveName} ready.";

        return new ElementSnapshot(
            element,
            ResolveStageState(pickup, operated, blocked),
            pickup,
            operated,
            Math.Clamp(progress, 0, 1),
            quantity,
            setting,
            blocked ? "Time-overcurrent operation reached but trip permission is blocked." : pickup ? timingDetail : $"{curveName} ready.");
    }

    private static ElementSnapshot DisabledElement(ProtectionElementId element, double quantity, double setting)
        => new(element, ProtectionStageState.Disabled, false, false, 0, quantity, setting, "Element disabled by active setting group.");

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

    private static double AccumulateTimeCharacteristic(
        double current,
        double measured,
        double pickup,
        IecCurveFamily curve,
        double timeMultiplier,
        TimeSpan definiteDelay,
        TimeSpan minimumOperate,
        ProtectionResetMode resetMode,
        TimeSpan resetDelay,
        double userK,
        double userAlpha,
        double userC,
        TimeSpan delta,
        bool started,
        bool droppedOut)
    {
        if (started && pickup > 0 && measured > pickup)
        {
            var operatingSeconds = IecCurveCalculator.GetOperateTimeSeconds(
                curve,
                measured / pickup,
                timeMultiplier,
                definiteDelay,
                minimumOperate,
                userK,
                userAlpha,
                userC);
            if (double.IsFinite(operatingSeconds) && operatingSeconds > 0)
                return current + delta.TotalSeconds / operatingSeconds;
            return current;
        }

        if (!droppedOut)
            return current;

        return resetMode switch
        {
            ProtectionResetMode.Instantaneous => 0,
            ProtectionResetMode.DefiniteTime => Math.Max(0, current - delta.TotalSeconds / resetDelay.TotalSeconds),
            ProtectionResetMode.InverseMemory => Math.Max(0, current - delta.TotalSeconds * 2),
            _ => 0
        };
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
