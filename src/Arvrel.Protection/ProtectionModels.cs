using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Arvrel.Protection.Algorithms;

namespace Arvrel.Protection;

public enum ProtectionElementId
{
    PhaseInstantaneous50P,
    PhaseTime51P,
    EarthInstantaneous50N,
    EarthTime51N,
    DirectionalPhase67,
    DirectionalEarth67N,
    Undervoltage27,
    Overvoltage59,
    ResidualOvervoltage59N
}

public enum ProtectionStageState
{
    Ready,
    Pickup,
    Timing,
    Operated,
    Blocked,
    Disabled
}

public enum IecCurveFamily
{
    DefiniteTime,
    StandardInverse,
    VeryInverse,
    ExtremelyInverse,
    LongTimeInverse,
    UserDefined
}

public enum ProtectionResetMode
{
    Instantaneous,
    DefiniteTime,
    InverseMemory
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
    public string GroupName { get; init; } = "GROUP A";
    public int Revision { get; init; } = 1;

    public bool PhaseInstantaneousEnabled { get; init; } = true;
    public double PhaseInstantaneousPickupA { get; init; } = 4.00;
    public TimeSpan PhaseInstantaneousDelay { get; init; } = TimeSpan.FromMilliseconds(60);
    public double PhaseInstantaneousDropoutRatio { get; init; } = 0.95;

    public bool PhaseTimeEnabled { get; init; } = true;
    public double PhaseTimePickupA { get; init; } = 1.25;
    public IecCurveFamily PhaseTimeCurve { get; init; } = IecCurveFamily.StandardInverse;
    public double PhaseTimeMultiplier { get; init; } = 0.12;
    public TimeSpan PhaseTimeDefiniteDelay { get; init; } = TimeSpan.FromMilliseconds(600);
    public TimeSpan PhaseTimeMinimumOperateTime { get; init; } = TimeSpan.FromMilliseconds(20);
    public double PhaseTimeDropoutRatio { get; init; } = 0.95;
    public ProtectionResetMode PhaseTimeResetMode { get; init; } = ProtectionResetMode.InverseMemory;
    public TimeSpan PhaseTimeResetDelay { get; init; } = TimeSpan.FromSeconds(1);
    public double PhaseTimeUserK { get; init; } = 0.14;
    public double PhaseTimeUserAlpha { get; init; } = 0.02;
    public double PhaseTimeUserC { get; init; }

    public bool EarthInstantaneousEnabled { get; init; } = true;
    public double EarthInstantaneousPickupA { get; init; } = 0.80;
    public TimeSpan EarthInstantaneousDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public double EarthInstantaneousDropoutRatio { get; init; } = 0.95;

    public bool EarthTimeEnabled { get; init; } = true;
    public double EarthTimePickupA { get; init; } = 0.30;
    public IecCurveFamily EarthTimeCurve { get; init; } = IecCurveFamily.StandardInverse;
    public double EarthTimeMultiplier { get; init; } = 0.15;
    public TimeSpan EarthTimeDefiniteDelay { get; init; } = TimeSpan.FromMilliseconds(800);
    public TimeSpan EarthTimeMinimumOperateTime { get; init; } = TimeSpan.FromMilliseconds(20);
    public double EarthTimeDropoutRatio { get; init; } = 0.95;
    public ProtectionResetMode EarthTimeResetMode { get; init; } = ProtectionResetMode.InverseMemory;
    public TimeSpan EarthTimeResetDelay { get; init; } = TimeSpan.FromSeconds(1);
    public double EarthTimeUserK { get; init; } = 0.14;
    public double EarthTimeUserAlpha { get; init; } = 0.02;
    public double EarthTimeUserC { get; init; }

    public FeederProtectionSettings Feeder { get; init; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GroupName))
            throw new ArgumentException("Setting group name is required.", nameof(GroupName));
        if (Revision < 1)
            throw new ArgumentOutOfRangeException(nameof(Revision));

        ValidateDefinedEnum(PhaseTimeCurve, nameof(PhaseTimeCurve));
        ValidateDefinedEnum(PhaseTimeResetMode, nameof(PhaseTimeResetMode));
        ValidateDefinedEnum(EarthTimeCurve, nameof(EarthTimeCurve));
        ValidateDefinedEnum(EarthTimeResetMode, nameof(EarthTimeResetMode));

        ValidateElement(PhaseInstantaneousPickupA, PhaseInstantaneousDelay, PhaseInstantaneousDropoutRatio,
            nameof(PhaseInstantaneousPickupA), nameof(PhaseInstantaneousDelay), nameof(PhaseInstantaneousDropoutRatio));
        ValidateElement(EarthInstantaneousPickupA, EarthInstantaneousDelay, EarthInstantaneousDropoutRatio,
            nameof(EarthInstantaneousPickupA), nameof(EarthInstantaneousDelay), nameof(EarthInstantaneousDropoutRatio));

        ValidateTimeElement(
            PhaseTimePickupA,
            PhaseTimeMultiplier,
            PhaseTimeDefiniteDelay,
            PhaseTimeMinimumOperateTime,
            PhaseTimeDropoutRatio,
            PhaseTimeResetDelay,
            PhaseTimeUserK,
            PhaseTimeUserAlpha,
            PhaseTimeUserC,
            nameof(PhaseTimePickupA));
        ValidateTimeElement(
            EarthTimePickupA,
            EarthTimeMultiplier,
            EarthTimeDefiniteDelay,
            EarthTimeMinimumOperateTime,
            EarthTimeDropoutRatio,
            EarthTimeResetDelay,
            EarthTimeUserK,
            EarthTimeUserAlpha,
            EarthTimeUserC,
            nameof(EarthTimePickupA));
        Feeder.Validate();
    }

    public string Fingerprint()
    {
        var canonical = CanonicalJoin(
            GroupName.Trim(), Revision,
            PhaseInstantaneousEnabled, PhaseInstantaneousPickupA, PhaseInstantaneousDelay.Ticks, PhaseInstantaneousDropoutRatio,
            PhaseTimeEnabled, PhaseTimePickupA, PhaseTimeCurve, PhaseTimeMultiplier, PhaseTimeDefiniteDelay.Ticks,
            PhaseTimeMinimumOperateTime.Ticks, PhaseTimeDropoutRatio, PhaseTimeResetMode, PhaseTimeResetDelay.Ticks,
            PhaseTimeUserK, PhaseTimeUserAlpha, PhaseTimeUserC,
            EarthInstantaneousEnabled, EarthInstantaneousPickupA, EarthInstantaneousDelay.Ticks, EarthInstantaneousDropoutRatio,
            EarthTimeEnabled, EarthTimePickupA, EarthTimeCurve, EarthTimeMultiplier, EarthTimeDefiniteDelay.Ticks,
            EarthTimeMinimumOperateTime.Ticks, EarthTimeDropoutRatio, EarthTimeResetMode, EarthTimeResetDelay.Ticks,
            EarthTimeUserK, EarthTimeUserAlpha, EarthTimeUserC,
            Feeder.CanonicalIdentity());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    internal static string CanonicalJoin(params object?[] values)
        => string.Join('|', values.Select(FormatCanonicalValue));

    private static string FormatCanonicalValue(object? value) => value switch
    {
        null => string.Empty,
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static void ValidateElement(
        double pickup,
        TimeSpan delay,
        double dropout,
        string pickupName,
        string delayName,
        string dropoutName)
    {
        ValidatePositive(pickup, pickupName);
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(delayName);
        ValidateDropout(dropout, dropoutName);
    }

    private static void ValidateTimeElement(
        double pickup,
        double timeMultiplier,
        TimeSpan definiteDelay,
        TimeSpan minimumOperate,
        double dropout,
        TimeSpan resetDelay,
        double userK,
        double userAlpha,
        double userC,
        string prefix)
    {
        ValidatePositive(pickup, prefix);
        ValidatePositive(timeMultiplier, $"{prefix}.TMS");
        if (definiteDelay <= TimeSpan.Zero || definiteDelay > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException($"{prefix}.DefiniteDelay");
        if (minimumOperate < TimeSpan.Zero || minimumOperate > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException($"{prefix}.MinimumOperateTime");
        if (resetDelay <= TimeSpan.Zero || resetDelay > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException($"{prefix}.ResetDelay");
        ValidateDropout(dropout, $"{prefix}.DropoutRatio");
        ValidatePositive(userK, $"{prefix}.UserK");
        ValidatePositive(userAlpha, $"{prefix}.UserAlpha");
        if (!double.IsFinite(userC) || userC < 0)
            throw new ArgumentOutOfRangeException($"{prefix}.UserC");
    }

    private static void ValidateDropout(double value, string name)
    {
        if (!double.IsFinite(value) || value is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateDefinedEnum<TEnum>(TEnum value, string name) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(name, value, "Undefined enum value.");
    }
}

public sealed record MeasurementFrame(
    DateTimeOffset Timestamp,
    double PhaseA,
    double PhaseB,
    double PhaseC,
    double Residual,
    SmvTrustState SmvTrust,
    PhasorMeasurementSet? Phasors = null)
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

public sealed record ProtectionOperationEvidence(
    string Element,
    DateTimeOffset PickupTimestamp,
    DateTimeOffset TripTimestamp,
    double PickupQuantity,
    double TripQuantity,
    string QuantitySymbol,
    string QuantityUnit,
    bool PhaseA,
    bool PhaseB,
    bool PhaseC,
    bool Earth);

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
    public FeederProtectionSnapshot Feeder { get; init; } = FeederProtectionSnapshot.Ready(Timestamp);
    public ProtectionOperationEvidence? LatchedOperation { get; init; }
    public IReadOnlyList<AlgorithmAbComparisonSnapshot> AlgorithmComparisons { get; init; } = Array.Empty<AlgorithmAbComparisonSnapshot>();
    public AlgorithmRuntimeRegistrySnapshot AlgorithmRuntime { get; init; } = AlgorithmRuntimeRegistry.Snapshot();

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
        SmvTrustState.Healthy)
    {
        Feeder = FeederProtectionSnapshot.Ready(timestamp),
        AlgorithmRuntime = AlgorithmRuntimeRegistry.Snapshot()
    };

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
