namespace Arvrel.Protection;

public sealed class ProtectionEngine
{
    private ProtectionSettings _settings;
    private readonly FeederProtectionEngine _feederEngine = new();
    private readonly Dictionary<string, PickupEvidence> _pickupEvidence = new(StringComparer.Ordinal);
    private TimeSpan _phase50Elapsed;
    private TimeSpan _earth50Elapsed;
    private double _phase51Progress;
    private double _earth51Progress;
    private bool _phase50PickupPrevious;
    private bool _earth50PickupPrevious;
    private bool _phase51PickupPrevious;
    private bool _earth51PickupPrevious;
    private DateTimeOffset? _previousTimestamp;
    private bool _tripLatched;
    private ProtectionOperationEvidence? _latchedOperation;

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

        // A protection timer starts at the observed pickup edge. The interval between
        // the preceding non-pickup frame and the first pickup frame must not be counted
        // retroactively. This keeps definite/inverse timing causal and makes settings
        // that are exactly representable on the processing grid operate exactly from
        // the recorded pickup timestamp.
        var phase50TimingDelta = TimingDelta(delta, phase50Pickup, _phase50PickupPrevious);
        var earth50TimingDelta = TimingDelta(delta, earth50Pickup, _earth50PickupPrevious);
        var phase51TimingDelta = TimingDelta(delta, phase51Pickup, _phase51PickupPrevious);
        var earth51TimingDelta = TimingDelta(delta, earth51Pickup, _earth51PickupPrevious);

        _phase50Elapsed = AccumulateDefiniteTime(
            _phase50Elapsed,
            phase50Pickup,
            phase50TimingDelta,
            !_settings.PhaseInstantaneousEnabled ||
            maxPhase < _settings.PhaseInstantaneousPickupA * _settings.PhaseInstantaneousDropoutRatio);
        _earth50Elapsed = AccumulateDefiniteTime(
            _earth50Elapsed,
            earth50Pickup,
            earth50TimingDelta,
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
            phase51TimingDelta,
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
            earth51TimingDelta,
            earth51Pickup,
            !_settings.EarthTimeEnabled || residual < _settings.EarthTimePickupA * _settings.EarthTimeDropoutRatio);

        _phase50PickupPrevious = phase50Pickup;
        _earth50PickupPrevious = earth50Pickup;
        _phase51PickupPrevious = phase51Pickup;
        _earth51PickupPrevious = earth51Pickup;

        var phase50Operate = _settings.PhaseInstantaneousEnabled && _phase50Elapsed >= _settings.PhaseInstantaneousDelay;
        var earth50Operate = _settings.EarthInstantaneousEnabled && _earth50Elapsed >= _settings.EarthInstantaneousDelay;
        var phase51Operate = _settings.PhaseTimeEnabled && _phase51Progress >= 1;
        var earth51Operate = _settings.EarthTimeEnabled && _earth51Progress >= 1;

        var feeder = _feederEngine.Evaluate(frame, _settings.Feeder, delta);
        var legacyOperationRequested = phase50Operate || phase51Operate || earth50Operate || earth51Operate;
        var blocked = feeder.Blocked || (legacyOperationRequested && !frame.SmvTrust.AllowsTrip);
        var tripRequested = feeder.TripRequested || (legacyOperationRequested && frame.SmvTrust.AllowsTrip);

        var phase50 = BuildDefiniteSnapshot(
            ProtectionElementId.PhaseInstantaneous50P,
            _settings.PhaseInstantaneousEnabled,
            phase50Pickup,
            phase50Operate,
            blocked && phase50Operate,
            _phase50Elapsed,
            _settings.PhaseInstantaneousDelay,
            maxPhase,
            _settings.PhaseInstantaneousPickupA);
        var phase51 = BuildTimeSnapshot(
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
            _settings.PhaseTimeUserC);
        var earth50 = BuildDefiniteSnapshot(
            ProtectionElementId.EarthInstantaneous50N,
            _settings.EarthInstantaneousEnabled,
            earth50Pickup,
            earth50Operate,
            blocked && earth50Operate,
            _earth50Elapsed,
            _settings.EarthInstantaneousDelay,
            residual,
            _settings.EarthInstantaneousPickupA);
        var earth51 = BuildTimeSnapshot(
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
            _settings.EarthTimeUserC);

        var minimumPhasePickup = Math.Min(
            _settings.PhaseInstantaneousEnabled ? _settings.PhaseInstantaneousPickupA : double.PositiveInfinity,
            _settings.PhaseTimeEnabled ? _settings.PhaseTimePickupA : double.PositiveInfinity);
        var phaseA = double.IsFinite(minimumPhasePickup) && frame.PhaseA >= minimumPhasePickup;
        var phaseB = double.IsFinite(minimumPhasePickup) && frame.PhaseB >= minimumPhasePickup;
        var phaseC = double.IsFinite(minimumPhasePickup) && frame.PhaseC >= minimumPhasePickup;

        if (frame.Phasors is { } phasors &&
            (feeder.DirectionalPhase67.Pickup || feeder.DirectionalPhase67.Operated))
        {
            var pickup = _settings.Feeder.DirectionalPhase67PickupA;
            var directionalA = phasors.PhaseACurrent.Magnitude >= pickup;
            var directionalB = phasors.PhaseBCurrent.Magnitude >= pickup;
            var directionalC = phasors.PhaseCCurrent.Magnitude >= pickup;

            if (!directionalA && !directionalB && !directionalC)
            {
                var maximum = phasors.MaximumPhaseCurrent;
                directionalA = Math.Abs(phasors.PhaseACurrent.Magnitude - maximum) < 1e-9;
                directionalB = Math.Abs(phasors.PhaseBCurrent.Magnitude - maximum) < 1e-9;
                directionalC = Math.Abs(phasors.PhaseCCurrent.Magnitude - maximum) < 1e-9;
            }

            phaseA |= directionalA;
            phaseB |= directionalB;
            phaseC |= directionalC;
        }

        var earth = earth50Pickup || earth51Pickup || feeder.DirectionalEarth67N.Pickup || feeder.ResidualOvervoltage59N.Pickup;

        // Preserve established numerical-relay precedence: instantaneous phase,
        // instantaneous earth, time phase, time earth, then feeder functions.
        // Operated elements are still resolved before any pickup-only state.
        var elements = new[]
        {
            phase50,
            earth50,
            phase51,
            earth51,
            feeder.DirectionalPhase67,
            feeder.DirectionalEarth67N,
            feeder.Undervoltage27,
            feeder.Overvoltage59,
            feeder.ResidualOvervoltage59N
        };

        UpdatePickupEvidence(frame.Timestamp, elements);
        var operatedElement = ResolveFirstElement(elements, operated: true);
        var pickupElement = ResolveFirstElement(elements, operated: false);
        var activeElement = operatedElement ?? FormatPickupElement(pickupElement) ?? "READY";

        if (tripRequested && !_tripLatched)
        {
            var tripElement = operatedElement ?? NormalizeElement(feeder.ActiveElement) ?? pickupElement ?? "UNKNOWN";
            _latchedOperation = CaptureOperationEvidence(
                tripElement,
                frame.Timestamp,
                elements,
                phaseA,
                phaseB,
                phaseC,
                earth);
        }

        if (tripRequested)
            _tripLatched = true;

        var reportedElement = _tripLatched && _latchedOperation is not null
            ? _latchedOperation.Element
            : activeElement;
        var decisionReason = blocked
            ? $"TRIP BLOCKED · {frame.SmvTrust.Code} · {frame.SmvTrust.Detail}"
            : _tripLatched
                ? $"TRIP LATCHED · {reportedElement}"
                : activeElement == "READY"
                    ? "Measurements stable · no pickup"
                    : $"OPERATING · {activeElement}";

        return new ProtectionSnapshot(
            frame.Timestamp,
            phase50,
            phase51,
            earth50,
            earth51,
            phaseA,
            phaseB,
            phaseC,
            earth,
            tripRequested,
            _tripLatched,
            blocked,
            reportedElement,
            decisionReason,
            frame.SmvTrust)
        {
            Feeder = feeder,
            LatchedOperation = _latchedOperation
        };
    }

    public void Reset()
    {
        ResetDynamicState(keepTripLatch: false);
        _previousTimestamp = null;
    }

    /// <summary>
    /// Advances only the engine's time anchor for an Ethernet frame rejected before
    /// measurement ingestion. No protection accumulator, pickup state, or trip state
    /// is evaluated from the stale previous measurement.
    /// </summary>
    public void ObserveRejectedFrameTimestamp(DateTimeOffset timestamp)
    {
        if (!_previousTimestamp.HasValue || timestamp > _previousTimestamp.Value)
            _previousTimestamp = timestamp;
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
        _phase50PickupPrevious = false;
        _earth50PickupPrevious = false;
        _phase51PickupPrevious = false;
        _earth51PickupPrevious = false;
        _feederEngine.Reset();
        _pickupEvidence.Clear();
        if (!keepTripLatch)
        {
            _tripLatched = false;
            _latchedOperation = null;
        }
    }

    private ProtectionSnapshot CreateUnavailableSnapshot(MeasurementFrame frame, string reason)
    {
        var ready = ProtectionSnapshot.Ready(frame.Timestamp);
        var activeElement = _tripLatched && _latchedOperation is not null
            ? _latchedOperation.Element
            : "SMV BLOCK";
        return ready with
        {
            DecisionReason = $"MEASUREMENT BLOCKED · {frame.SmvTrust.Code} · {reason}",
            Blocked = true,
            ActiveElement = activeElement,
            SmvTrust = frame.SmvTrust,
            TripLatched = _tripLatched,
            LatchedOperation = _latchedOperation
        };
    }

    private void UpdatePickupEvidence(DateTimeOffset timestamp, IReadOnlyList<ElementSnapshot> elements)
    {
        foreach (var element in elements)
        {
            var code = ElementCode(element.Element);
            if (element.Pickup || element.Operated)
            {
                if (!_pickupEvidence.ContainsKey(code))
                {
                    var quantity = QuantityFor(element);
                    _pickupEvidence[code] = new PickupEvidence(timestamp, quantity.Value, quantity.Symbol, quantity.Unit);
                }
            }
            else
            {
                _pickupEvidence.Remove(code);
            }
        }
    }

    private ProtectionOperationEvidence CaptureOperationEvidence(
        string element,
        DateTimeOffset tripTimestamp,
        IReadOnlyList<ElementSnapshot> elements,
        bool phaseA,
        bool phaseB,
        bool phaseC,
        bool earth)
    {
        var normalized = NormalizeElement(element) ?? "UNKNOWN";
        var snapshot = elements.FirstOrDefault(candidate => ElementCode(candidate.Element) == normalized);
        var tripQuantity = snapshot is null
            ? new OperatingQuantity(0, QuantitySymbol(normalized), QuantityUnit(normalized))
            : QuantityFor(snapshot);
        var pickup = _pickupEvidence.TryGetValue(normalized, out var evidence)
            ? evidence
            : new PickupEvidence(tripTimestamp, tripQuantity.Value, tripQuantity.Symbol, tripQuantity.Unit);

        return new ProtectionOperationEvidence(
            normalized,
            pickup.Timestamp,
            tripTimestamp,
            pickup.Quantity,
            tripQuantity.Value,
            tripQuantity.Symbol,
            tripQuantity.Unit,
            phaseA,
            phaseB,
            phaseC,
            earth);
    }

    private static string? ResolveFirstElement(IReadOnlyList<ElementSnapshot> elements, bool operated)
    {
        foreach (var element in elements)
        {
            if (operated ? element.Operated : element.Pickup)
                return ElementCode(element.Element);
        }
        return null;
    }

    private static string? FormatPickupElement(string? element) => element switch
    {
        null => null,
        "50P-1" or "50N" => $"{element} PICKUP",
        _ => $"{element} START"
    };

    private static string? NormalizeElement(string? element)
    {
        if (string.IsNullOrWhiteSpace(element) ||
            string.Equals(element, "READY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(element, "PHASOR UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return element.Replace(" PICKUP", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" START", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string ElementCode(ProtectionElementId element) => element switch
    {
        ProtectionElementId.PhaseInstantaneous50P => "50P-1",
        ProtectionElementId.PhaseTime51P => "51P",
        ProtectionElementId.EarthInstantaneous50N => "50N",
        ProtectionElementId.EarthTime51N => "51N",
        ProtectionElementId.DirectionalPhase67 => "67P",
        ProtectionElementId.DirectionalEarth67N => "67N",
        ProtectionElementId.Undervoltage27 => "27",
        ProtectionElementId.Overvoltage59 => "59",
        ProtectionElementId.ResidualOvervoltage59N => "59N",
        _ => "UNKNOWN"
    };

    private static OperatingQuantity QuantityFor(ElementSnapshot snapshot)
    {
        var code = ElementCode(snapshot.Element);
        return new OperatingQuantity(
            snapshot.OperatingQuantity,
            QuantitySymbol(code),
            QuantityUnit(code));
    }

    private static string QuantitySymbol(string element) => element switch
    {
        "50N" or "51N" or "67N" => "3I0",
        "27" or "59" => "V OP",
        "59N" => "3V0",
        _ => "I OP"
    };

    private static string QuantityUnit(string element)
        => element is "27" or "59" or "59N" ? "V" : "A";

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

    private static TimeSpan TimingDelta(TimeSpan delta, bool pickup, bool previousPickup)
        => pickup && !previousPickup ? TimeSpan.Zero : delta;

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

    private readonly record struct PickupEvidence(
        DateTimeOffset Timestamp,
        double Quantity,
        string Symbol,
        string Unit);

    private readonly record struct OperatingQuantity(double Value, string Symbol, string Unit);
}
