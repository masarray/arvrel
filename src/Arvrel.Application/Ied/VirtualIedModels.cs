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
    string Description);

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
            "Automatic Voltage Regulator · AVR",
            "OLTC voltage regulation",
            "Vendor-neutral transformer voltage-regulator simulator with automatic and manual tap control.")
    ];
}

public enum AvrOperatingMode
{
    Automatic,
    Manual
}

public enum AvrControlState
{
    InBand,
    TimingRaise,
    TimingLower,
    TapLimit,
    Blocked,
    Manual
}

public sealed record AvrSettings
{
    public string ProfileName { get; init; } = "ARV-AVR Classic";
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
    public double UndervoltageBlockPercent { get; init; } = 80.0;
    public double OvervoltageBlockPercent { get; init; } = 120.0;
    public bool LineDropCompensationEnabled { get; init; }
    public double LineDropCompensationPercent { get; init; }

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
        if (!double.IsFinite(UndervoltageBlockPercent) || UndervoltageBlockPercent <= 0 || UndervoltageBlockPercent >= 100)
            throw new ArgumentOutOfRangeException(nameof(UndervoltageBlockPercent), "Undervoltage blocking must be between 0 and 100%.");
        if (!double.IsFinite(OvervoltageBlockPercent) || OvervoltageBlockPercent <= 100 || OvervoltageBlockPercent > 200)
            throw new ArgumentOutOfRangeException(nameof(OvervoltageBlockPercent), "Overvoltage blocking must be above 100% and no more than 200%.");
        if (!double.IsFinite(LineDropCompensationPercent) || Math.Abs(LineDropCompensationPercent) > 20)
            throw new ArgumentOutOfRangeException(nameof(LineDropCompensationPercent), "Line-drop compensation must be between -20% and +20%.");
    }
}

public sealed record AvrSnapshot(
    DateTimeOffset Timestamp,
    AvrOperatingMode Mode,
    AvrControlState State,
    double SourceVoltageV,
    double MeasuredVoltageV,
    double EffectiveSetpointVoltageV,
    double LowerBandVoltageV,
    double UpperBandVoltageV,
    double DeviationPercent,
    int TapPosition,
    int OperationCount,
    bool RaiseOutput,
    bool LowerOutput,
    bool Blocked,
    string Reason,
    double DelayElapsedSeconds,
    double DelayTargetSeconds)
{
    public static AvrSnapshot Ready(AvrSettings settings, int tapPosition) => new(
        DateTimeOffset.UtcNow,
        AvrOperatingMode.Automatic,
        AvrControlState.InBand,
        settings.SetpointVoltageV,
        settings.SetpointVoltageV,
        settings.SetpointVoltageV,
        settings.SetpointVoltageV * (1 - (settings.TolerancePercent / 100.0)),
        settings.SetpointVoltageV * (1 + (settings.TolerancePercent / 100.0)),
        0,
        tapPosition,
        0,
        false,
        false,
        false,
        "Ready",
        0,
        settings.T1Seconds);
}

public sealed class AvrSimulationEngine
{
    private AvrSettings _settings;
    private AvrOperatingMode _mode = AvrOperatingMode.Automatic;
    private int _tapPosition;
    private int _operationCount;
    private int _consecutiveOperations;
    private int _lastDirection;
    private double _delayElapsedSeconds;

    public AvrSimulationEngine(AvrSettings settings)
    {
        settings.Validate();
        _settings = settings;
        _tapPosition = settings.NeutralTap;
    }

    public AvrSettings Settings => _settings;
    public AvrOperatingMode Mode => _mode;
    public int TapPosition => _tapPosition;
    public int OperationCount => _operationCount;

    public void ApplySettings(AvrSettings settings, bool keepTapPosition = true)
    {
        settings.Validate();
        _settings = settings;
        _tapPosition = keepTapPosition
            ? Math.Clamp(_tapPosition, settings.MinimumTap, settings.MaximumTap)
            : settings.NeutralTap;
        ResetTiming();
    }

    public void SetMode(AvrOperatingMode mode)
    {
        if (_mode == mode)
            return;

        _mode = mode;
        ResetTiming();
    }

    public bool RaiseTap()
    {
        ResetTiming();
        return MoveTap(+1);
    }

    public bool LowerTap()
    {
        ResetTiming();
        return MoveTap(-1);
    }

    public void Reset()
    {
        _tapPosition = _settings.NeutralTap;
        _operationCount = 0;
        _mode = AvrOperatingMode.Automatic;
        ResetTiming();
    }

    public AvrSnapshot Advance(TimeSpan elapsed, double sourceVoltageV)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!double.IsFinite(sourceVoltageV) || sourceVoltageV <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceVoltageV), "Source voltage must be greater than zero.");

        var effectiveSetpoint = _settings.SetpointVoltageV *
            (1 + (_settings.LineDropCompensationEnabled ? _settings.LineDropCompensationPercent / 100.0 : 0));
        var lowerBand = effectiveSetpoint * (1 - (_settings.TolerancePercent / 100.0));
        var upperBand = effectiveSetpoint * (1 + (_settings.TolerancePercent / 100.0));
        var tapFactor = 1 + ((_tapPosition - _settings.NeutralTap) * _settings.TapStepPercent / 100.0);
        var measuredVoltage = sourceVoltageV * tapFactor;
        var deviationPercent = ((measuredVoltage - effectiveSetpoint) / effectiveSetpoint) * 100.0;

        var underBlock = measuredVoltage < _settings.NominalVoltageV * (_settings.UndervoltageBlockPercent / 100.0);
        var overBlock = measuredVoltage > _settings.NominalVoltageV * (_settings.OvervoltageBlockPercent / 100.0);
        if (underBlock || overBlock)
        {
            ResetTiming();
            return Snapshot(
                sourceVoltageV,
                measuredVoltage,
                effectiveSetpoint,
                lowerBand,
                upperBand,
                deviationPercent,
                AvrControlState.Blocked,
                underBlock ? "Undervoltage blocking active" : "Overvoltage blocking active",
                blocked: true,
                delayTargetSeconds: 0);
        }

        if (_mode == AvrOperatingMode.Manual)
        {
            ResetTiming();
            return Snapshot(
                sourceVoltageV,
                measuredVoltage,
                effectiveSetpoint,
                lowerBand,
                upperBand,
                deviationPercent,
                AvrControlState.Manual,
                "Manual mode · automatic tap commands inhibited",
                blocked: false,
                delayTargetSeconds: 0);
        }

        var direction = measuredVoltage < lowerBand ? +1 : measuredVoltage > upperBand ? -1 : 0;
        if (direction == 0)
        {
            ResetTiming();
            return Snapshot(
                sourceVoltageV,
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

        var operated = false;
        if (_delayElapsedSeconds + 1e-9 >= delayTarget)
        {
            operated = MoveTap(direction);
            _delayElapsedSeconds = 0;
            if (operated)
                _consecutiveOperations++;
        }

        var state = direction > 0 ? AvrControlState.TimingRaise : AvrControlState.TimingLower;
        var reason = operated
            ? direction > 0 ? "Automatic RAISE tap command issued" : "Automatic LOWER tap command issued"
            : direction > 0 ? "Voltage low · timing RAISE" : "Voltage high · timing LOWER";

        return Snapshot(
            sourceVoltageV,
            measuredVoltage,
            effectiveSetpoint,
            lowerBand,
            upperBand,
            deviationPercent,
            state,
            reason,
            blocked: false,
            delayTargetSeconds: delayTarget,
            raiseOutput: operated && direction > 0,
            lowerOutput: operated && direction < 0);
    }

    private AvrSnapshot Snapshot(
        double sourceVoltageV,
        double measuredVoltageV,
        double effectiveSetpointVoltageV,
        double lowerBandVoltageV,
        double upperBandVoltageV,
        double deviationPercent,
        AvrControlState state,
        string reason,
        bool blocked,
        double delayTargetSeconds,
        bool raiseOutput = false,
        bool lowerOutput = false) => new(
            DateTimeOffset.UtcNow,
            _mode,
            state,
            sourceVoltageV,
            measuredVoltageV,
            effectiveSetpointVoltageV,
            lowerBandVoltageV,
            upperBandVoltageV,
            deviationPercent,
            _tapPosition,
            _operationCount,
            raiseOutput,
            lowerOutput,
            blocked,
            reason,
            _delayElapsedSeconds,
            delayTargetSeconds);

    private bool MoveTap(int direction)
    {
        var next = Math.Clamp(_tapPosition + Math.Sign(direction), _settings.MinimumTap, _settings.MaximumTap);
        if (next == _tapPosition)
            return false;

        _tapPosition = next;
        _operationCount++;
        return true;
    }

    private void ResetTiming()
    {
        _delayElapsedSeconds = 0;
        _consecutiveOperations = 0;
        _lastDirection = 0;
    }
}
