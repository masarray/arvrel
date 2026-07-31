namespace Arvrel.Protection;

public enum ProtectionElementId
{
    PhaseInstantaneous50P,
    PhaseTime51P,
    EarthInstantaneous50N,
    EarthTime51N
}

public enum ProtectionStageState
{
    Ready,
    Pickup,
    Timing,
    Operated,
    Blocked
}

public sealed record SmvTrustState(
    bool AllowsMeasurement,
    bool AllowsPickup,
    bool AllowsTrip,
    string Code,
    string Detail)
{
    public static SmvTrustState Healthy { get; } = new(
        true,
        true,
        true,
        "HEALTHY",
        "Stream continuity, freshness, mapping and quality are acceptable.");

    public static SmvTrustState TripBlocked(string code, string detail) => new(
        true,
        true,
        false,
        code,
        detail);
}

public sealed record ProtectionSettings
{
    public double PhaseInstantaneousPickupA { get; init; } = 4.00;
    public TimeSpan PhaseInstantaneousDelay { get; init; } = TimeSpan.FromMilliseconds(60);
    public double PhaseTimePickupA { get; init; } = 1.25;
    public double PhaseTimeMultiplier { get; init; } = 0.12;
    public double EarthInstantaneousPickupA { get; init; } = 0.80;
    public TimeSpan EarthInstantaneousDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public double EarthTimePickupA { get; init; } = 0.30;
    public double EarthTimeMultiplier { get; init; } = 0.15;
    public double DropoutRatio { get; init; } = 0.95;

    public void Validate()
    {
        ValidatePositive(PhaseInstantaneousPickupA, nameof(PhaseInstantaneousPickupA));
        ValidatePositive(PhaseTimePickupA, nameof(PhaseTimePickupA));
        ValidatePositive(EarthInstantaneousPickupA, nameof(EarthInstantaneousPickupA));
        ValidatePositive(EarthTimePickupA, nameof(EarthTimePickupA));
        ValidatePositive(PhaseTimeMultiplier, nameof(PhaseTimeMultiplier));
        ValidatePositive(EarthTimeMultiplier, nameof(EarthTimeMultiplier));

        if (PhaseInstantaneousDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(PhaseInstantaneousDelay));
        if (EarthInstantaneousDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(EarthInstantaneousDelay));
        if (DropoutRatio is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(DropoutRatio));
    }

    private static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }
}

public sealed record MeasurementFrame(
    DateTimeOffset Timestamp,
    double PhaseA,
    double PhaseB,
    double PhaseC,
    double Residual,
    SmvTrustState SmvTrust)
{
    public double MaximumPhase => Math.Max(PhaseA, Math.Max(PhaseB, PhaseC));
}

public sealed record ElementSnapshot(
    ProtectionElementId Element,
    ProtectionStageState State,
    bool Pickup,
    bool Operated,
    double Progress,
    double OperatingQuantity,
    double PickupSetting,
    string Reason);

public sealed record ProtectionSnapshot(
    DateTimeOffset Timestamp,
    ElementSnapshot Phase50,
    ElementSnapshot Phase51,
    ElementSnapshot Earth50,
    ElementSnapshot Earth51,
    bool PhaseAPickup,
    bool PhaseBPickup,
    bool PhaseCPickup,
    bool EarthPickup,
    bool TripRequested,
    bool TripLatched,
    bool Blocked,
    string ActiveElement,
    string DecisionReason,
    SmvTrustState SmvTrust)
{
    public static ProtectionSnapshot Ready(DateTimeOffset timestamp) => new(
        timestamp,
        ReadyElement(ProtectionElementId.PhaseInstantaneous50P),
        ReadyElement(ProtectionElementId.PhaseTime51P),
        ReadyElement(ProtectionElementId.EarthInstantaneous50N),
        ReadyElement(ProtectionElementId.EarthTime51N),
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        "READY",
        "Measurements stable · no pickup",
        SmvTrustState.Healthy);

    private static ElementSnapshot ReadyElement(ProtectionElementId element) => new(
        element,
        ProtectionStageState.Ready,
        false,
        false,
        0,
        0,
        0,
        "Ready");
}
