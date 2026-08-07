namespace Arvrel.Application.Ied;

public enum VirtualIedKind
{
    ProtectionRelay,
    AutomaticVoltageRegulator
}

public sealed record VirtualIedDescriptor(
    VirtualIedKind Kind,
    string Id,
    string DisplayName,
    string Function,
    string Description)
{
    public override string ToString() => DisplayName;
}

public static class VirtualIedCatalog
{
    public static IReadOnlyList<VirtualIedDescriptor> All { get; } =
    [
        new(
            VirtualIedKind.ProtectionRelay,
            "ARV-5051",
            "Protection Relay · OCR",
            "50/51 · 50N/51N",
            "Existing virtual overcurrent relay laboratory."),
        new(
            VirtualIedKind.AutomaticVoltageRegulator,
            "ARV-AVR",
            "AVR · OLTC Controller",
            "Automatic voltage regulation",
            "Vendor-neutral transformer voltage-regulator simulator with automatic and manual tap control.")
    ];
}

public enum AvrOperatingMode
{
    Automatic,
    Manual
}

public enum AvrControlAuthority
{
    Local,
    Remote
}

public enum AvrVoltageInputMode
{
    BenchInjection,
    ClosedLoopPlant
}

public enum AvrControlState
{
    InBand,
    TimingRaise,
    TimingLower,
    TapMoving,
    TapLimit,
    Blocked,
    Manual
}

public sealed record AvrSettings
{
    public string ProfileName { get; init; } = "ARV-AVR Bench";
    public double NominalVoltageV { get; init; } = 100.0;
    public double SetpointVoltageV { get; init; } = 100.0;
    public double TolerancePercent { get; init; } = 1.0;
    public double T1Seconds { get; init; } = 10.0;
    public bool FastT2Enabled { get; init; } = true;
    public double T2Seconds { get; init; } = 2.0;
    public int MinimumTap { get; init; } = -8;
    public int MaximumTap { get; init; } = 8;
    public int NeutralTap { get; init; }
    public double TapStepPercent { get; init; } = 1.25;
    public double CommandPulseSeconds { get; init; } = 0.5;
    public double MotorDriveTravelSeconds { get; init; } = 3.0;
    public double UndervoltageBlockPercent { get; init; } = 80.0;
    public double OvervoltageBlockPercent { get; init; } = 120.0;
    public double NominalCurrentA { get; init; } = 1.0;
    public bool UndercurrentBlockingEnabled { get; init; }
    public double UndercurrentBlockPercent { get; init; } = 10.0;
    public bool OvercurrentBlockingEnabled { get; init; }
    public double OvercurrentBlockPercent { get; init; } = 150.0;
    public bool LineDropCompensationEnabled { get; init; }
    public double LineDropCompensationPercent { get; init; }
    public AvrVoltageInputMode VoltageInputMode { get; init; } = AvrVoltageInputMode.BenchInjection;

    public void Validate()
    {
        if (!double.IsFinite(NominalVoltageV) || NominalVoltageV <= 0)
            throw new ArgumentOutOfRangeException(nameof(NominalVoltageV), "Nominal voltage must be greater than zero.");
        if (!double.IsFinite(SetpointVoltageV) || SetpointVoltageV <= 0)
            throw new ArgumentOutOfRangeException(nameof(SetpointVoltageV), "Setpoint voltage must be greater than zero.");
        if (!double.IsFinite(TolerancePercent) || TolerancePercent <= 0 || TolerancePercent > 20)
            throw new ArgumentOutOfRangeException(nameof(TolerancePercent), "Tolerance must be greater than zero and no more than 20%.");
        if (!double.IsFinite(T1Seconds) || T1Seconds < 0 || T1Seconds > 600)
            throw new ArgumentOutOfRangeException(nameof(T1Seconds), "T1 must be between 0 and 600 seconds.");
        if (!double.IsFinite(T2Seconds) || T2Seconds < 0 || T2Seconds > 600)
            throw new ArgumentOutOfRangeException(nameof(T2Seconds), "T2 must be between 0 and 600 seconds.");
        if (MinimumTap >= MaximumTap)
            throw new ArgumentException("Minimum tap must be lower than maximum tap.");
        if (NeutralTap < MinimumTap || NeutralTap > MaximumTap)
            throw new ArgumentOutOfRangeException(nameof(NeutralTap), "Neutral tap must be inside the configured tap range.");
        if (!double.IsFinite(TapStepPercent) || TapStepPercent <= 0 || TapStepPercent > 10)
            throw new ArgumentOutOfRangeException(nameof(TapStepPercent), "Tap step must be greater than zero and no more than 10%.");
        if (!double.IsFinite(CommandPulseSeconds) || CommandPulseSeconds < 0.05 || CommandPulseSeconds > 10)
            throw new ArgumentOutOfRangeException(nameof(CommandPulseSeconds), "Tap command pulse must be between 0.05 and 10 seconds.");
        if (!double.IsFinite(MotorDriveTravelSeconds) || MotorDriveTravelSeconds < 0 || MotorDriveTravelSeconds > 60)
            throw new ArgumentOutOfRangeException(nameof(MotorDriveTravelSeconds), "Motor-drive travel time must be between 0 and 60 seconds.");
        if (!double.IsFinite(UndervoltageBlockPercent) || UndervoltageBlockPercent <= 0 || UndervoltageBlockPercent >= 100)
            throw new ArgumentOutOfRangeException(nameof(UndervoltageBlockPercent), "Undervoltage blocking must be between 0 and 100%.");
        if (!double.IsFinite(OvervoltageBlockPercent) || OvervoltageBlockPercent <= 100 || OvervoltageBlockPercent > 200)
            throw new ArgumentOutOfRangeException(nameof(OvervoltageBlockPercent), "Overvoltage blocking must be above 100% and no more than 200%.");
        if (!double.IsFinite(NominalCurrentA) || NominalCurrentA <= 0 || NominalCurrentA > 20)
            throw new ArgumentOutOfRangeException(nameof(NominalCurrentA), "Nominal secondary current must be greater than zero and no more than 20 A.");
        if (!double.IsFinite(UndercurrentBlockPercent) || UndercurrentBlockPercent < 0 || UndercurrentBlockPercent >= 100)
            throw new ArgumentOutOfRangeException(nameof(UndercurrentBlockPercent), "Undercurrent blocking must be between 0 and less than 100% In.");
        if (!double.IsFinite(OvercurrentBlockPercent) || OvercurrentBlockPercent <= 100 || OvercurrentBlockPercent > 1000)
            throw new ArgumentOutOfRangeException(nameof(OvercurrentBlockPercent), "Overcurrent blocking must be above 100% and no more than 1000% In.");
        if (!double.IsFinite(LineDropCompensationPercent) || Math.Abs(LineDropCompensationPercent) > 20)
            throw new ArgumentOutOfRangeException(nameof(LineDropCompensationPercent), "Line-drop compensation must be between -20% and +20%.");
    }
}

public sealed record AvrSnapshot(
    DateTimeOffset Timestamp,
    AvrOperatingMode Mode,
    AvrControlAuthority Authority,
    AvrControlState State,
    double SourceVoltageV,
    double SourceCurrentA,
    double CurrentAngleDegrees,
    double PowerFactor,
    double MeasuredVoltageV,
    double EffectiveSetpointVoltageV,
    double LowerBandVoltageV,
    double UpperBandVoltageV,
    double DeviationPercent,
    int TapPosition,
    int? PendingTapPosition,
    int OperationCount,
    bool RaiseOutput,
    bool LowerOutput,
    bool TapMoving,
    bool Blocked,
    string Reason,
    double DelayElapsedSeconds,
    double DelayTargetSeconds,
    double MotorTravelRemainingSeconds,
    double CommandPulseRemainingSeconds)
{
    public static AvrSnapshot Ready(AvrSettings settings, int tapPosition) => new(
        DateTimeOffset.UtcNow,
        AvrOperatingMode.Automatic,
        AvrControlAuthority.Local,
        AvrControlState.InBand,
        settings.SetpointVoltageV,
        settings.NominalCurrentA,
        0,
        1,
        settings.SetpointVoltageV,
        settings.SetpointVoltageV,
        settings.SetpointVoltageV * (1 - (settings.TolerancePercent / 100.0)),
        settings.SetpointVoltageV * (1 + (settings.TolerancePercent / 100.0)),
        0,
        tapPosition,
        null,
        0,
        false,
        false,
        false,
        false,
        "Ready",
        0,
        settings.T1Seconds,
        0,
        0);
}

public sealed class AvrSimulationEngine
{
    private AvrSettings _settings;
    private AvrOperatingMode _mode = AvrOperatingMode.Automatic;
    private AvrControlAuthority _authority = AvrControlAuthority.Local;
    private int _tapPosition;
    private int _operationCount;
    private int _consecutiveOperations;
    private int _lastDirection;
    private int _lastCommandDirection;
    private int? _pendingTapPosition;
    private double _delayElapsedSeconds;
    private double _motorTravelRemainingSeconds;
    private double _commandPulseRemainingSeconds;

    public AvrSimulationEngine(AvrSettings settings)
    {
        settings.Validate();
        _settings = settings;
        _tapPosition = settings.NeutralTap;
    }

    public AvrSettings Settings => _settings;
    public AvrOperatingMode Mode => _mode;
    public AvrControlAuthority Authority => _authority;
    public int TapPosition => _tapPosition;
    public int OperationCount => _operationCount;
    public bool TapMoving => _pendingTapPosition.HasValue;
    public bool CanAcceptLocalManualCommand =>
        _authority == AvrControlAuthority.Local && _mode == AvrOperatingMode.Manual && !_pendingTapPosition.HasValue;

    public void ApplySettings(AvrSettings settings, bool keepTapPosition = true)
    {
        settings.Validate();
        _settings = settings;
        _tapPosition = keepTapPosition
            ? Math.Clamp(_tapPosition, settings.MinimumTap, settings.MaximumTap)
            : settings.NeutralTap;
        ClearActuator();
        ResetRegulationTiming();
    }

    public void SetMode(AvrOperatingMode mode)
    {
        if (_mode == mode)
            return;

        _mode = mode;
        ResetRegulationTiming();
    }

    public void SetAuthority(AvrControlAuthority authority)
    {
        if (_authority == authority)
            return;

        _authority = authority;
        ResetRegulationTiming();
    }

    public bool RaiseTap()
        => IssueLocalManualTap(+1);

    public bool LowerTap()
        => IssueLocalManualTap(-1);

    public void Reset()
    {
        _tapPosition = _settings.NeutralTap;
        _operationCount = 0;
        _mode = AvrOperatingMode.Automatic;
        _authority = AvrControlAuthority.Local;
        ClearActuator();
        ResetRegulationTiming();
    }

    public AvrSnapshot Advance(
        TimeSpan elapsed,
        double sourceVoltageV,
        double sourceCurrentA = 1.0,
        double currentAngleDegrees = 0)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!double.IsFinite(sourceVoltageV) || sourceVoltageV <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceVoltageV), "Source voltage must be greater than zero.");
        if (!double.IsFinite(sourceCurrentA) || sourceCurrentA < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceCurrentA), "Source current must be zero or greater.");
        if (!double.IsFinite(currentAngleDegrees))
            throw new ArgumentOutOfRangeException(nameof(currentAngleDegrees), "Current angle must be finite.");

        AdvanceActuator(elapsed.TotalSeconds);

        var effectiveSetpoint = _settings.SetpointVoltageV *
            (1 + (_settings.LineDropCompensationEnabled ? _settings.LineDropCompensationPercent / 100.0 : 0));
        var lowerBand = effectiveSetpoint * (1 - (_settings.TolerancePercent / 100.0));
        var upperBand = effectiveSetpoint * (1 + (_settings.TolerancePercent / 100.0));
        var tapFactor = 1 + ((_tapPosition - _settings.NeutralTap) * _settings.TapStepPercent / 100.0);
        var measuredVoltage = _settings.VoltageInputMode == AvrVoltageInputMode.ClosedLoopPlant
            ? sourceVoltageV * tapFactor
            : sourceVoltageV;
        var deviationPercent = ((measuredVoltage - effectiveSetpoint) / effectiveSetpoint) * 100.0;
        var powerFactor = Math.Cos(currentAngleDegrees * Math.PI / 180.0);

        if (_pendingTapPosition.HasValue)
        {
            return Snapshot(
                sourceVoltageV,
                sourceCurrentA,
                currentAngleDegrees,
                powerFactor,
                measuredVoltage,
                effectiveSetpoint,
                lowerBand,
                upperBand,
                deviationPercent,
                AvrControlState.TapMoving,
                $"Tap changer moving · {_tapPosition:+0;-0;0} → {_pendingTapPosition.Value:+0;-0;0}",
                blocked: false,
                delayTargetSeconds: 0);
        }

        var underVoltageBlock = measuredVoltage < _settings.NominalVoltageV * (_settings.UndervoltageBlockPercent / 100.0);
        var overVoltageBlock = measuredVoltage > _settings.NominalVoltageV * (_settings.OvervoltageBlockPercent / 100.0);
        var underCurrentBlock = _settings.UndercurrentBlockingEnabled &&
            sourceCurrentA < _settings.NominalCurrentA * (_settings.UndercurrentBlockPercent / 100.0);
        var overCurrentBlock = _settings.OvercurrentBlockingEnabled &&
            sourceCurrentA > _settings.NominalCurrentA * (_settings.OvercurrentBlockPercent / 100.0);

        if (underVoltageBlock || overVoltageBlock || underCurrentBlock || overCurrentBlock)
        {
            ResetRegulationTiming();
            var reason = underVoltageBlock ? "Undervoltage blocking active"
                : overVoltageBlock ? "Overvoltage blocking active"
                : underCurrentBlock ? "Undercurrent blocking active"
                : "Overcurrent blocking active";
            return Snapshot(
                sourceVoltageV,
                sourceCurrentA,
                currentAngleDegrees,
                powerFactor,
                measuredVoltage,
                effectiveSetpoint,
                lowerBand,
                upperBand,
                deviationPercent,
                AvrControlState.Blocked,
                reason,
                blocked: true,
                delayTargetSeconds: 0);
        }

        if (_mode == AvrOperatingMode.Manual)
        {
            ResetRegulationTiming();
            var reason = _authority == AvrControlAuthority.Local
                ? "Manual local mode · use RAISE / LOWER front-panel commands"
                : "Manual remote mode · local tap commands inhibited";
            return Snapshot(
                sourceVoltageV,
                sourceCurrentA,
                currentAngleDegrees,
                powerFactor,
                measuredVoltage,
                effectiveSetpoint,
                lowerBand,
                upperBand,
                deviationPercent,
                AvrControlState.Manual,
                reason,
                blocked: false,
                delayTargetSeconds: 0);
        }

        var direction = measuredVoltage < lowerBand ? +1 : measuredVoltage > upperBand ? -1 : 0;
        if (direction == 0)
        {
            ResetRegulationTiming();
            return Snapshot(
                sourceVoltageV,
                sourceCurrentA,
                currentAngleDegrees,
                powerFactor,
                measuredVoltage,
                effectiveSetpoint,
                lowerBand,
                upperBand,
                deviationPercent,
                AvrControlState.InBand,
                "Voltage inside tolerance band",
                blocked: false,
                delayTargetSeconds: _settings.T1Seconds);
        }

        if (direction != _lastDirection)
        {
            _delayElapsedSeconds = 0;
            _consecutiveOperations = 0;
            _lastDirection = direction;
        }

        var delayTarget = _settings.FastT2Enabled && _consecutiveOperations > 0
            ? _settings.T2Seconds
            : _settings.T1Seconds;
        _delayElapsedSeconds += elapsed.TotalSeconds;

        var atLimit = direction > 0
            ? _tapPosition >= _settings.MaximumTap
            : _tapPosition <= _settings.MinimumTap;
        if (atLimit)
        {
            return Snapshot(
                sourceVoltageV,
                sourceCurrentA,
                currentAngleDegrees,
                powerFactor,
                measuredVoltage,
                effectiveSetpoint,
                lowerBand,
                upperBand,
                deviationPercent,
                AvrControlState.TapLimit,
                direction > 0 ? "Raise requested · maximum tap reached" : "Lower requested · minimum tap reached",
                blocked: false,
                delayTargetSeconds: delayTarget);
        }

        if (_delayElapsedSeconds + 1e-9 >= delayTarget)
        {
            IssueTapCommand(direction);
            _delayElapsedSeconds = 0;
            _consecutiveOperations++;
            return Snapshot(
                sourceVoltageV,
                sourceCurrentA,
                currentAngleDegrees,
                powerFactor,
                measuredVoltage,
                effectiveSetpoint,
                lowerBand,
                upperBand,
                deviationPercent,
                AvrControlState.TapMoving,
                direction > 0 ? "Automatic RAISE command issued · awaiting tap feedback" : "Automatic LOWER command issued · awaiting tap feedback",
                blocked: false,
                delayTargetSeconds: 0);
        }

        return Snapshot(
            sourceVoltageV,
            sourceCurrentA,
            currentAngleDegrees,
            powerFactor,
            measuredVoltage,
            effectiveSetpoint,
            lowerBand,
            upperBand,
            deviationPercent,
            direction > 0 ? AvrControlState.TimingRaise : AvrControlState.TimingLower,
            direction > 0 ? "Voltage low · timing RAISE" : "Voltage high · timing LOWER",
            blocked: false,
            delayTargetSeconds: delayTarget);
    }

    private bool IssueLocalManualTap(int direction)
    {
        if (!CanAcceptLocalManualCommand)
            return false;

        ResetRegulationTiming();
        return IssueTapCommand(direction);
    }

    private bool IssueTapCommand(int direction)
    {
        if (_pendingTapPosition.HasValue)
            return false;

        var next = Math.Clamp(_tapPosition + Math.Sign(direction), _settings.MinimumTap, _settings.MaximumTap);
        if (next == _tapPosition)
            return false;

        _pendingTapPosition = next;
        _lastCommandDirection = Math.Sign(direction);
        _commandPulseRemainingSeconds = _settings.CommandPulseSeconds;
        _motorTravelRemainingSeconds = _settings.MotorDriveTravelSeconds;
        _operationCount++;

        if (_settings.MotorDriveTravelSeconds <= 1e-9)
            CompletePendingTap();

        return true;
    }

    private void AdvanceActuator(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0)
            return;

        _commandPulseRemainingSeconds = Math.Max(0, _commandPulseRemainingSeconds - elapsedSeconds);
        if (!_pendingTapPosition.HasValue)
            return;

        _motorTravelRemainingSeconds = Math.Max(0, _motorTravelRemainingSeconds - elapsedSeconds);
        if (_motorTravelRemainingSeconds <= 1e-9)
            CompletePendingTap();
    }

    private void CompletePendingTap()
    {
        if (_pendingTapPosition is not int target)
            return;

        _tapPosition = target;
        _pendingTapPosition = null;
        _motorTravelRemainingSeconds = 0;
    }

    private AvrSnapshot Snapshot(
        double sourceVoltageV,
        double sourceCurrentA,
        double currentAngleDegrees,
        double powerFactor,
        double measuredVoltageV,
        double effectiveSetpointVoltageV,
        double lowerBandVoltageV,
        double upperBandVoltageV,
        double deviationPercent,
        AvrControlState state,
        string reason,
        bool blocked,
        double delayTargetSeconds) => new(
            DateTimeOffset.UtcNow,
            _mode,
            _authority,
            state,
            sourceVoltageV,
            sourceCurrentA,
            currentAngleDegrees,
            powerFactor,
            measuredVoltageV,
            effectiveSetpointVoltageV,
            lowerBandVoltageV,
            upperBandVoltageV,
            deviationPercent,
            _tapPosition,
            _pendingTapPosition,
            _operationCount,
            _commandPulseRemainingSeconds > 0 && _lastCommandDirection > 0,
            _commandPulseRemainingSeconds > 0 && _lastCommandDirection < 0,
            _pendingTapPosition.HasValue,
            blocked,
            reason,
            _delayElapsedSeconds,
            delayTargetSeconds,
            _motorTravelRemainingSeconds,
            _commandPulseRemainingSeconds);

    private void ClearActuator()
    {
        _pendingTapPosition = null;
        _motorTravelRemainingSeconds = 0;
        _commandPulseRemainingSeconds = 0;
        _lastCommandDirection = 0;
    }

    private void ResetRegulationTiming()
    {
        _delayElapsedSeconds = 0;
        _consecutiveOperations = 0;
        _lastDirection = 0;
    }
}