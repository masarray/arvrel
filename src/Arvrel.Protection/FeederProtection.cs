using System.Numerics;

namespace Arvrel.Protection;

public enum DirectionalSense
{
    Forward,
    Reverse
}

public enum VoltageSelectionLogic
{
    OneOfThree,
    TwoOfThree,
    ThreeOfThree
}

public enum VoltageMeasurementMode
{
    PhaseToNeutral,
    PhaseToPhase,
    PositiveSequence
}

public sealed record FeederProtectionSettings
{
    public bool Undervoltage27Enabled { get; init; }
    public double Undervoltage27PickupV { get; init; } = 90;
    public TimeSpan Undervoltage27Delay { get; init; } = TimeSpan.FromSeconds(1);
    public double Undervoltage27ResetRatio { get; init; } = 1.05;
    public VoltageMeasurementMode Undervoltage27Mode { get; init; } = VoltageMeasurementMode.PhaseToNeutral;
    public VoltageSelectionLogic Undervoltage27Logic { get; init; } = VoltageSelectionLogic.TwoOfThree;

    public bool Overvoltage59Enabled { get; init; }
    public double Overvoltage59PickupV { get; init; } = 110;
    public TimeSpan Overvoltage59Delay { get; init; } = TimeSpan.FromSeconds(1);
    public double Overvoltage59DropoutRatio { get; init; } = 0.95;
    public VoltageMeasurementMode Overvoltage59Mode { get; init; } = VoltageMeasurementMode.PhaseToNeutral;
    public VoltageSelectionLogic Overvoltage59Logic { get; init; } = VoltageSelectionLogic.OneOfThree;

    public bool ResidualOvervoltage59NEnabled { get; init; }
    public double ResidualOvervoltage59NPickupV { get; init; } = 10;
    public TimeSpan ResidualOvervoltage59NDelay { get; init; } = TimeSpan.FromMilliseconds(500);
    public double ResidualOvervoltage59NDropoutRatio { get; init; } = 0.95;

    public bool DirectionalPhase67Enabled { get; init; }
    public double DirectionalPhase67PickupA { get; init; } = 1.25;
    public TimeSpan DirectionalPhase67Delay { get; init; } = TimeSpan.FromMilliseconds(300);
    public double DirectionalPhase67DropoutRatio { get; init; } = 0.95;
    public double DirectionalPhase67CharacteristicAngleDeg { get; init; } = 45;
    public double DirectionalPhase67MinimumPolarizingVoltageV { get; init; } = 5;
    public DirectionalSense DirectionalPhase67Sense { get; init; } = DirectionalSense.Forward;

    public bool DirectionalEarth67NEnabled { get; init; }
    public double DirectionalEarth67NPickupA { get; init; } = 0.30;
    public TimeSpan DirectionalEarth67NDelay { get; init; } = TimeSpan.FromMilliseconds(300);
    public double DirectionalEarth67NDropoutRatio { get; init; } = 0.95;
    public double DirectionalEarth67NCharacteristicAngleDeg { get; init; } = -45;
    public double DirectionalEarth67NMinimumPolarizingVoltageV { get; init; } = 2;
    public DirectionalSense DirectionalEarth67NSense { get; init; } = DirectionalSense.Forward;

    public bool AnyEnabled =>
        Undervoltage27Enabled ||
        Overvoltage59Enabled ||
        ResidualOvervoltage59NEnabled ||
        DirectionalPhase67Enabled ||
        DirectionalEarth67NEnabled;

    public void Validate()
    {
        ValidatePositive(Undervoltage27PickupV, nameof(Undervoltage27PickupV));
        ValidateDelay(Undervoltage27Delay, nameof(Undervoltage27Delay));
        if (!double.IsFinite(Undervoltage27ResetRatio) || Undervoltage27ResetRatio < 1 || Undervoltage27ResetRatio > 2)
            throw new ArgumentOutOfRangeException(nameof(Undervoltage27ResetRatio));

        ValidatePositive(Overvoltage59PickupV, nameof(Overvoltage59PickupV));
        ValidateDelay(Overvoltage59Delay, nameof(Overvoltage59Delay));
        ValidateDropout(Overvoltage59DropoutRatio, nameof(Overvoltage59DropoutRatio));

        ValidatePositive(ResidualOvervoltage59NPickupV, nameof(ResidualOvervoltage59NPickupV));
        ValidateDelay(ResidualOvervoltage59NDelay, nameof(ResidualOvervoltage59NDelay));
        ValidateDropout(ResidualOvervoltage59NDropoutRatio, nameof(ResidualOvervoltage59NDropoutRatio));

        ValidatePositive(DirectionalPhase67PickupA, nameof(DirectionalPhase67PickupA));
        ValidateDelay(DirectionalPhase67Delay, nameof(DirectionalPhase67Delay));
        ValidateDropout(DirectionalPhase67DropoutRatio, nameof(DirectionalPhase67DropoutRatio));
        ValidateAngle(DirectionalPhase67CharacteristicAngleDeg, nameof(DirectionalPhase67CharacteristicAngleDeg));
        ValidatePositive(DirectionalPhase67MinimumPolarizingVoltageV, nameof(DirectionalPhase67MinimumPolarizingVoltageV));

        ValidatePositive(DirectionalEarth67NPickupA, nameof(DirectionalEarth67NPickupA));
        ValidateDelay(DirectionalEarth67NDelay, nameof(DirectionalEarth67NDelay));
        ValidateDropout(DirectionalEarth67NDropoutRatio, nameof(DirectionalEarth67NDropoutRatio));
        ValidateAngle(DirectionalEarth67NCharacteristicAngleDeg, nameof(DirectionalEarth67NCharacteristicAngleDeg));
        ValidatePositive(DirectionalEarth67NMinimumPolarizingVoltageV, nameof(DirectionalEarth67NMinimumPolarizingVoltageV));
    }

    public string CanonicalIdentity() => string.Join('|',
        Undervoltage27Enabled, Undervoltage27PickupV, Undervoltage27Delay.Ticks, Undervoltage27ResetRatio, Undervoltage27Mode, Undervoltage27Logic,
        Overvoltage59Enabled, Overvoltage59PickupV, Overvoltage59Delay.Ticks, Overvoltage59DropoutRatio, Overvoltage59Mode, Overvoltage59Logic,
        ResidualOvervoltage59NEnabled, ResidualOvervoltage59NPickupV, ResidualOvervoltage59NDelay.Ticks, ResidualOvervoltage59NDropoutRatio,
        DirectionalPhase67Enabled, DirectionalPhase67PickupA, DirectionalPhase67Delay.Ticks, DirectionalPhase67DropoutRatio,
        DirectionalPhase67CharacteristicAngleDeg, DirectionalPhase67MinimumPolarizingVoltageV, DirectionalPhase67Sense,
        DirectionalEarth67NEnabled, DirectionalEarth67NPickupA, DirectionalEarth67NDelay.Ticks, DirectionalEarth67NDropoutRatio,
        DirectionalEarth67NCharacteristicAngleDeg, DirectionalEarth67NMinimumPolarizingVoltageV, DirectionalEarth67NSense);

    private static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateDelay(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero || value > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateDropout(double value, string name)
    {
        if (!double.IsFinite(value) || value is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateAngle(double value, string name)
    {
        if (!double.IsFinite(value) || value is < -180 or > 180)
            throw new ArgumentOutOfRangeException(name);
    }
}

public sealed record PhasorMeasurementSet(
    Complex PhaseACurrent,
    Complex PhaseBCurrent,
    Complex PhaseCCurrent,
    Complex NeutralCurrent,
    Complex PhaseAVoltage,
    Complex PhaseBVoltage,
    Complex PhaseCVoltage,
    Complex NeutralVoltage,
    double FrequencyHz)
{
    private static readonly Complex A = Complex.FromPolarCoordinates(1, 2 * Math.PI / 3);
    private static readonly Complex A2 = A * A;

    public Complex PositiveSequenceCurrent => (PhaseACurrent + A * PhaseBCurrent + A2 * PhaseCCurrent) / 3;
    public Complex NegativeSequenceCurrent => (PhaseACurrent + A2 * PhaseBCurrent + A * PhaseCCurrent) / 3;
    public Complex ZeroSequenceCurrent => (PhaseACurrent + PhaseBCurrent + PhaseCCurrent) / 3;

    public Complex PositiveSequenceVoltage => (PhaseAVoltage + A * PhaseBVoltage + A2 * PhaseCVoltage) / 3;
    public Complex NegativeSequenceVoltage => (PhaseAVoltage + A2 * PhaseBVoltage + A * PhaseCVoltage) / 3;
    public Complex ZeroSequenceVoltage => (PhaseAVoltage + PhaseBVoltage + PhaseCVoltage) / 3;

    public Complex ResidualCurrent => PhaseACurrent + PhaseBCurrent + PhaseCCurrent;
    public Complex ResidualVoltage => PhaseAVoltage + PhaseBVoltage + PhaseCVoltage;

    public double[] PhaseToNeutralVoltages =>
    [
        PhaseAVoltage.Magnitude,
        PhaseBVoltage.Magnitude,
        PhaseCVoltage.Magnitude
    ];

    public double[] PhaseToPhaseVoltages =>
    [
        (PhaseAVoltage - PhaseBVoltage).Magnitude,
        (PhaseBVoltage - PhaseCVoltage).Magnitude,
        (PhaseCVoltage - PhaseAVoltage).Magnitude
    ];

    public double MaximumPhaseCurrent => Math.Max(PhaseACurrent.Magnitude, Math.Max(PhaseBCurrent.Magnitude, PhaseCCurrent.Magnitude));
}

public sealed record DirectionalDecision(
    bool PolarizingAvailable,
    bool InSelectedDirection,
    double OperatingAngleDegrees,
    double Torque,
    double OperatingMagnitude,
    double PolarizingMagnitude,
    string Reason)
{
    public static DirectionalDecision Unavailable(string reason) => new(false, false, double.NaN, 0, 0, 0, reason);
}

public sealed record FeederProtectionSnapshot(
    ElementSnapshot Undervoltage27,
    ElementSnapshot Overvoltage59,
    ElementSnapshot ResidualOvervoltage59N,
    ElementSnapshot DirectionalPhase67,
    ElementSnapshot DirectionalEarth67N,
    DirectionalDecision PhaseDirectionalDecision,
    DirectionalDecision EarthDirectionalDecision,
    bool TripRequested,
    bool Blocked,
    string ActiveElement)
{
    public static FeederProtectionSnapshot Ready(DateTimeOffset timestamp) => new(
        ReadyElement(ProtectionElementId.Undervoltage27),
        ReadyElement(ProtectionElementId.Overvoltage59),
        ReadyElement(ProtectionElementId.ResidualOvervoltage59N),
        ReadyElement(ProtectionElementId.DirectionalPhase67),
        ReadyElement(ProtectionElementId.DirectionalEarth67N),
        DirectionalDecision.Unavailable("Phasor measurement not available."),
        DirectionalDecision.Unavailable("Phasor measurement not available."),
        false,
        false,
        "READY");

    private static ElementSnapshot ReadyElement(ProtectionElementId id) => new(
        id,
        ProtectionStageState.Ready,
        false,
        false,
        0,
        0,
        0,
        "Ready");
}

internal sealed class FeederProtectionEngine
{
    private TimeSpan _undervoltageElapsed;
    private TimeSpan _overvoltageElapsed;
    private TimeSpan _residualOvervoltageElapsed;
    private TimeSpan _directionalPhaseElapsed;
    private TimeSpan _directionalEarthElapsed;

    public FeederProtectionSnapshot Evaluate(
        MeasurementFrame frame,
        FeederProtectionSettings settings,
        TimeSpan delta)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        if (!settings.AnyEnabled)
            return DisabledSnapshot(frame.Timestamp);

        if (frame.Phasors is null)
        {
            Reset();
            return UnavailableSnapshot(frame.Timestamp, "Voltage and current phasors are not available.");
        }

        var phasors = frame.Phasors;
        var undervoltageQuantity = ResolveUndervoltageQuantity(phasors, settings.Undervoltage27Mode, settings.Undervoltage27Logic);
        var overvoltageQuantity = ResolveOvervoltageQuantity(phasors, settings.Overvoltage59Mode, settings.Overvoltage59Logic);
        var residualVoltage = phasors.ResidualVoltage.Magnitude;

        var undervoltagePickup = settings.Undervoltage27Enabled &&
                                 frame.SmvTrust.AllowsPickup &&
                                 IsVoltageLogicSatisfied(
                                     ResolveVoltageValues(phasors, settings.Undervoltage27Mode),
                                     settings.Undervoltage27PickupV,
                                     settings.Undervoltage27Logic,
                                     below: true);
        var overvoltagePickup = settings.Overvoltage59Enabled &&
                                frame.SmvTrust.AllowsPickup &&
                                IsVoltageLogicSatisfied(
                                    ResolveVoltageValues(phasors, settings.Overvoltage59Mode),
                                    settings.Overvoltage59PickupV,
                                    settings.Overvoltage59Logic,
                                    below: false);
        var residualOvervoltagePickup = settings.ResidualOvervoltage59NEnabled &&
                                        frame.SmvTrust.AllowsPickup &&
                                        residualVoltage >= settings.ResidualOvervoltage59NPickupV;

        var phaseDirection = EvaluateDirection(
            phasors.PositiveSequenceCurrent,
            phasors.PositiveSequenceVoltage,
            settings.DirectionalPhase67CharacteristicAngleDeg,
            settings.DirectionalPhase67Sense,
            settings.DirectionalPhase67MinimumPolarizingVoltageV,
            "positive-sequence V1/I1");
        var earthDirection = EvaluateDirection(
            phasors.ResidualCurrent,
            phasors.ResidualVoltage,
            settings.DirectionalEarth67NCharacteristicAngleDeg,
            settings.DirectionalEarth67NSense,
            settings.DirectionalEarth67NMinimumPolarizingVoltageV,
            "residual 3V0/3I0");

        var phaseDirectionalPickup = settings.DirectionalPhase67Enabled &&
                                     frame.SmvTrust.AllowsPickup &&
                                     phasors.MaximumPhaseCurrent >= settings.DirectionalPhase67PickupA &&
                                     phaseDirection.PolarizingAvailable &&
                                     phaseDirection.InSelectedDirection;
        var earthDirectionalPickup = settings.DirectionalEarth67NEnabled &&
                                     frame.SmvTrust.AllowsPickup &&
                                     phasors.ResidualCurrent.Magnitude >= settings.DirectionalEarth67NPickupA &&
                                     earthDirection.PolarizingAvailable &&
                                     earthDirection.InSelectedDirection;

        _undervoltageElapsed = Accumulate(
            _undervoltageElapsed,
            undervoltagePickup,
            delta,
            !settings.Undervoltage27Enabled ||
            undervoltageQuantity >= settings.Undervoltage27PickupV * settings.Undervoltage27ResetRatio);
        _overvoltageElapsed = Accumulate(
            _overvoltageElapsed,
            overvoltagePickup,
            delta,
            !settings.Overvoltage59Enabled ||
            overvoltageQuantity < settings.Overvoltage59PickupV * settings.Overvoltage59DropoutRatio);
        _residualOvervoltageElapsed = Accumulate(
            _residualOvervoltageElapsed,
            residualOvervoltagePickup,
            delta,
            !settings.ResidualOvervoltage59NEnabled ||
            residualVoltage < settings.ResidualOvervoltage59NPickupV * settings.ResidualOvervoltage59NDropoutRatio);
        _directionalPhaseElapsed = Accumulate(
            _directionalPhaseElapsed,
            phaseDirectionalPickup,
            delta,
            !settings.DirectionalPhase67Enabled ||
            phasors.MaximumPhaseCurrent < settings.DirectionalPhase67PickupA * settings.DirectionalPhase67DropoutRatio ||
            !phaseDirection.InSelectedDirection);
        _directionalEarthElapsed = Accumulate(
            _directionalEarthElapsed,
            earthDirectionalPickup,
            delta,
            !settings.DirectionalEarth67NEnabled ||
            phasors.ResidualCurrent.Magnitude < settings.DirectionalEarth67NPickupA * settings.DirectionalEarth67NDropoutRatio ||
            !earthDirection.InSelectedDirection);

        var undervoltageOperate = settings.Undervoltage27Enabled && _undervoltageElapsed >= settings.Undervoltage27Delay;
        var overvoltageOperate = settings.Overvoltage59Enabled && _overvoltageElapsed >= settings.Overvoltage59Delay;
        var residualOvervoltageOperate = settings.ResidualOvervoltage59NEnabled && _residualOvervoltageElapsed >= settings.ResidualOvervoltage59NDelay;
        var phaseDirectionalOperate = settings.DirectionalPhase67Enabled && _directionalPhaseElapsed >= settings.DirectionalPhase67Delay;
        var earthDirectionalOperate = settings.DirectionalEarth67NEnabled && _directionalEarthElapsed >= settings.DirectionalEarth67NDelay;

        var operationRequested = undervoltageOperate || overvoltageOperate || residualOvervoltageOperate || phaseDirectionalOperate || earthDirectionalOperate;
        var blocked = operationRequested && !frame.SmvTrust.AllowsTrip;
        var tripRequested = operationRequested && frame.SmvTrust.AllowsTrip;
        var activeElement = ResolveActiveElement(
            undervoltageOperate,
            overvoltageOperate,
            residualOvervoltageOperate,
            phaseDirectionalOperate,
            earthDirectionalOperate,
            undervoltagePickup,
            overvoltagePickup,
            residualOvervoltagePickup,
            phaseDirectionalPickup,
            earthDirectionalPickup);

        return new FeederProtectionSnapshot(
            BuildSnapshot(ProtectionElementId.Undervoltage27, settings.Undervoltage27Enabled, undervoltagePickup, undervoltageOperate, blocked && undervoltageOperate,
                _undervoltageElapsed, settings.Undervoltage27Delay, undervoltageQuantity, settings.Undervoltage27PickupV, "Undervoltage timer active."),
            BuildSnapshot(ProtectionElementId.Overvoltage59, settings.Overvoltage59Enabled, overvoltagePickup, overvoltageOperate, blocked && overvoltageOperate,
                _overvoltageElapsed, settings.Overvoltage59Delay, overvoltageQuantity, settings.Overvoltage59PickupV, "Overvoltage timer active."),
            BuildSnapshot(ProtectionElementId.ResidualOvervoltage59N, settings.ResidualOvervoltage59NEnabled, residualOvervoltagePickup, residualOvervoltageOperate, blocked && residualOvervoltageOperate,
                _residualOvervoltageElapsed, settings.ResidualOvervoltage59NDelay, residualVoltage, settings.ResidualOvervoltage59NPickupV, "Residual overvoltage timer active."),
            BuildSnapshot(ProtectionElementId.DirectionalPhase67, settings.DirectionalPhase67Enabled, phaseDirectionalPickup, phaseDirectionalOperate, blocked && phaseDirectionalOperate,
                _directionalPhaseElapsed, settings.DirectionalPhase67Delay, phasors.MaximumPhaseCurrent, settings.DirectionalPhase67PickupA, phaseDirection.Reason),
            BuildSnapshot(ProtectionElementId.DirectionalEarth67N, settings.DirectionalEarth67NEnabled, earthDirectionalPickup, earthDirectionalOperate, blocked && earthDirectionalOperate,
                _directionalEarthElapsed, settings.DirectionalEarth67NDelay, phasors.ResidualCurrent.Magnitude, settings.DirectionalEarth67NPickupA, earthDirection.Reason),
            phaseDirection,
            earthDirection,
            tripRequested,
            blocked,
            activeElement);
    }

    public void Reset()
    {
        _undervoltageElapsed = TimeSpan.Zero;
        _overvoltageElapsed = TimeSpan.Zero;
        _residualOvervoltageElapsed = TimeSpan.Zero;
        _directionalPhaseElapsed = TimeSpan.Zero;
        _directionalEarthElapsed = TimeSpan.Zero;
    }

    private static DirectionalDecision EvaluateDirection(
        Complex operating,
        Complex polarizing,
        double characteristicAngleDegrees,
        DirectionalSense sense,
        double minimumPolarizingVoltage,
        string mode)
    {
        var operatingMagnitude = operating.Magnitude;
        var polarizingMagnitude = polarizing.Magnitude;
        if (polarizingMagnitude < minimumPolarizingVoltage)
        {
            return new DirectionalDecision(
                false,
                false,
                double.NaN,
                0,
                operatingMagnitude,
                polarizingMagnitude,
                $"Directional supervision unavailable: {mode} polarizing voltage {polarizingMagnitude:0.###} V is below {minimumPolarizingVoltage:0.###} V.");
        }

        var operatingAngle = NormalizeDegrees(ToDegrees(operating.Phase - polarizing.Phase));
        var torque = Math.Cos(ToRadians(operatingAngle - characteristicAngleDegrees));
        var inForwardRegion = torque > 0;
        var selected = sense == DirectionalSense.Forward ? inForwardRegion : !inForwardRegion;
        var directionText = inForwardRegion ? "FORWARD" : "REVERSE";
        return new DirectionalDecision(
            true,
            selected,
            operatingAngle,
            torque,
            operatingMagnitude,
            polarizingMagnitude,
            $"{mode} · {directionText} · angle {operatingAngle:0.0}° · MTA {characteristicAngleDegrees:0.0}° · torque {torque:0.###}.");
    }

    private static double[] ResolveVoltageValues(PhasorMeasurementSet phasors, VoltageMeasurementMode mode) => mode switch
    {
        VoltageMeasurementMode.PhaseToNeutral => phasors.PhaseToNeutralVoltages,
        VoltageMeasurementMode.PhaseToPhase => phasors.PhaseToPhaseVoltages,
        VoltageMeasurementMode.PositiveSequence =>
        [
            phasors.PositiveSequenceVoltage.Magnitude,
            phasors.PositiveSequenceVoltage.Magnitude,
            phasors.PositiveSequenceVoltage.Magnitude
        ],
        _ => phasors.PhaseToNeutralVoltages
    };

    private static double ResolveUndervoltageQuantity(PhasorMeasurementSet phasors, VoltageMeasurementMode mode, VoltageSelectionLogic logic)
    {
        var values = ResolveVoltageValues(phasors, mode).OrderBy(value => value).ToArray();
        return logic switch
        {
            VoltageSelectionLogic.OneOfThree => values[0],
            VoltageSelectionLogic.TwoOfThree => values[1],
            VoltageSelectionLogic.ThreeOfThree => values[2],
            _ => values[1]
        };
    }

    private static double ResolveOvervoltageQuantity(PhasorMeasurementSet phasors, VoltageMeasurementMode mode, VoltageSelectionLogic logic)
    {
        var values = ResolveVoltageValues(phasors, mode).OrderByDescending(value => value).ToArray();
        return logic switch
        {
            VoltageSelectionLogic.OneOfThree => values[0],
            VoltageSelectionLogic.TwoOfThree => values[1],
            VoltageSelectionLogic.ThreeOfThree => values[2],
            _ => values[0]
        };
    }

    private static bool IsVoltageLogicSatisfied(
        IReadOnlyList<double> values,
        double setting,
        VoltageSelectionLogic logic,
        bool below)
    {
        var required = logic switch
        {
            VoltageSelectionLogic.OneOfThree => 1,
            VoltageSelectionLogic.TwoOfThree => 2,
            VoltageSelectionLogic.ThreeOfThree => 3,
            _ => 1
        };
        var count = values.Count(value => below ? value <= setting : value >= setting);
        return count >= required;
    }

    private static ElementSnapshot BuildSnapshot(
        ProtectionElementId id,
        bool enabled,
        bool pickup,
        bool operated,
        bool blocked,
        TimeSpan elapsed,
        TimeSpan delay,
        double quantity,
        double setting,
        string activeReason)
    {
        if (!enabled)
            return new ElementSnapshot(id, ProtectionStageState.Disabled, false, false, 0, quantity, setting, "Element disabled by active setting group.");

        var progress = delay <= TimeSpan.Zero ? (pickup ? 1 : 0) : elapsed.TotalMilliseconds / delay.TotalMilliseconds;
        var state = blocked
            ? ProtectionStageState.Blocked
            : operated
                ? ProtectionStageState.Operated
                : pickup
                    ? ProtectionStageState.Timing
                    : ProtectionStageState.Ready;
        var reason = blocked
            ? "Operation reached but trip permission is blocked by the SMV trust policy."
            : pickup
                ? activeReason
                : "Ready";
        return new ElementSnapshot(id, state, pickup, operated, Math.Clamp(progress, 0, 1), quantity, setting, reason);
    }

    private static TimeSpan Accumulate(TimeSpan current, bool pickup, TimeSpan delta, bool droppedOut)
    {
        if (pickup)
            return current + delta;
        return droppedOut ? TimeSpan.Zero : current;
    }

    private static string ResolveActiveElement(
        bool uvOperate,
        bool ovOperate,
        bool residualOvOperate,
        bool phaseDirectionalOperate,
        bool earthDirectionalOperate,
        bool uvPickup,
        bool ovPickup,
        bool residualOvPickup,
        bool phaseDirectionalPickup,
        bool earthDirectionalPickup)
    {
        if (phaseDirectionalOperate) return "67P";
        if (earthDirectionalOperate) return "67N";
        if (uvOperate) return "27";
        if (ovOperate) return "59";
        if (residualOvOperate) return "59N";
        if (phaseDirectionalPickup) return "67P START";
        if (earthDirectionalPickup) return "67N START";
        if (uvPickup) return "27 START";
        if (ovPickup) return "59 START";
        if (residualOvPickup) return "59N START";
        return "READY";
    }

    private static FeederProtectionSnapshot DisabledSnapshot(DateTimeOffset timestamp) => new(
        DisabledElement(ProtectionElementId.Undervoltage27),
        DisabledElement(ProtectionElementId.Overvoltage59),
        DisabledElement(ProtectionElementId.ResidualOvervoltage59N),
        DisabledElement(ProtectionElementId.DirectionalPhase67),
        DisabledElement(ProtectionElementId.DirectionalEarth67N),
        DirectionalDecision.Unavailable("67P disabled."),
        DirectionalDecision.Unavailable("67N disabled."),
        false,
        false,
        "READY");

    private static FeederProtectionSnapshot UnavailableSnapshot(DateTimeOffset timestamp, string reason) => new(
        ReadyUnavailable(ProtectionElementId.Undervoltage27, reason),
        ReadyUnavailable(ProtectionElementId.Overvoltage59, reason),
        ReadyUnavailable(ProtectionElementId.ResidualOvervoltage59N, reason),
        ReadyUnavailable(ProtectionElementId.DirectionalPhase67, reason),
        ReadyUnavailable(ProtectionElementId.DirectionalEarth67N, reason),
        DirectionalDecision.Unavailable(reason),
        DirectionalDecision.Unavailable(reason),
        false,
        false,
        "PHASOR UNAVAILABLE");

    private static ElementSnapshot DisabledElement(ProtectionElementId id) => new(
        id,
        ProtectionStageState.Disabled,
        false,
        false,
        0,
        0,
        0,
        "Element disabled by active setting group.");

    private static ElementSnapshot ReadyUnavailable(ProtectionElementId id, string reason) => new(
        id,
        ProtectionStageState.Ready,
        false,
        false,
        0,
        0,
        0,
        reason);

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double NormalizeDegrees(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle <= -180) angle += 360;
        return angle;
    }
}

public static class FundamentalPhasorEstimator
{
    public static Complex EstimateRms(IReadOnlyList<double> samples, int samplesPerCycle)
    {
        if (samplesPerCycle < 4)
            throw new ArgumentOutOfRangeException(nameof(samplesPerCycle));
        if (samples.Count < samplesPerCycle)
            throw new ArgumentException("A complete one-cycle sample window is required.", nameof(samples));

        var start = samples.Count - samplesPerCycle;
        var real = 0.0;
        var imaginary = 0.0;
        var mean = 0.0;
        for (var index = 0; index < samplesPerCycle; index++)
            mean += samples[start + index];
        mean /= samplesPerCycle;

        for (var index = 0; index < samplesPerCycle; index++)
        {
            var value = samples[start + index] - mean;
            var angle = -2 * Math.PI * index / samplesPerCycle;
            real += value * Math.Cos(angle);
            imaginary += value * Math.Sin(angle);
        }

        var scale = Math.Sqrt(2) / samplesPerCycle;
        return new Complex(real * scale, imaginary * scale);
    }

    public static PhasorMeasurementSet Build(
        IReadOnlyList<double> phaseACurrent,
        IReadOnlyList<double> phaseBCurrent,
        IReadOnlyList<double> phaseCCurrent,
        IReadOnlyList<double> neutralCurrent,
        IReadOnlyList<double> phaseAVoltage,
        IReadOnlyList<double> phaseBVoltage,
        IReadOnlyList<double> phaseCVoltage,
        IReadOnlyList<double> neutralVoltage,
        int samplesPerCycle,
        double frequencyHz)
    {
        return new PhasorMeasurementSet(
            EstimateRms(phaseACurrent, samplesPerCycle),
            EstimateRms(phaseBCurrent, samplesPerCycle),
            EstimateRms(phaseCCurrent, samplesPerCycle),
            EstimateRms(neutralCurrent, samplesPerCycle),
            EstimateRms(phaseAVoltage, samplesPerCycle),
            EstimateRms(phaseBVoltage, samplesPerCycle),
            EstimateRms(phaseCVoltage, samplesPerCycle),
            EstimateRms(neutralVoltage, samplesPerCycle),
            frequencyHz);
    }
}
