using System.Security.Cryptography;
using System.Text;

namespace Arvrel.Protection;

public sealed record TransformerDifferentialSettings
{
    public bool Enabled { get; init; }
    public double MinimumPickupPu { get; init; } = 0.30;
    public double Slope1 { get; init; } = 0.25;
    public double Slope2 { get; init; } = 0.60;
    public double SlopeBreakpointPu { get; init; } = 2.0;
    public TimeSpan OperateDelay { get; init; } = TimeSpan.FromMilliseconds(20);
    public double DropoutRatio { get; init; } = 0.90;

    public bool HighSetEnabled { get; init; }
    public double HighSetPickupPu { get; init; } = 8.0;
    public TimeSpan HighSetDelay { get; init; } = TimeSpan.Zero;
    public double HighSetDropoutRatio { get; init; } = 0.90;
    public bool HighSetBypassesHarmonicSecurity { get; init; } = true;

    public TransformerHarmonicSecurityMode HarmonicSecurityMode { get; init; } = TransformerHarmonicSecurityMode.Blocking;
    public double SecondHarmonicThreshold { get; init; } = 0.15;
    public double FifthHarmonicThreshold { get; init; } = 0.35;
    public double SecondHarmonicRestraintGain { get; init; } = 2.0;
    public double FifthHarmonicRestraintGain { get; init; } = 1.0;

    public TransformerWindingCompensation HighVoltageCompensation { get; init; } = new();
    public TransformerWindingCompensation LowVoltageCompensation { get; init; } = new();

    public bool AnyEnabled => Enabled || HighSetEnabled;

    public void Validate()
    {
        ValidatePositive(MinimumPickupPu, nameof(MinimumPickupPu));
        ValidateSlope(Slope1, nameof(Slope1));
        ValidateSlope(Slope2, nameof(Slope2));
        if (Slope2 < Slope1)
            throw new ArgumentOutOfRangeException(nameof(Slope2), "Slope2 must be greater than or equal to Slope1.");
        ValidatePositive(SlopeBreakpointPu, nameof(SlopeBreakpointPu));
        ValidateDelay(OperateDelay, nameof(OperateDelay));
        ValidateDropout(DropoutRatio, nameof(DropoutRatio));

        ValidatePositive(HighSetPickupPu, nameof(HighSetPickupPu));
        ValidateDelay(HighSetDelay, nameof(HighSetDelay));
        ValidateDropout(HighSetDropoutRatio, nameof(HighSetDropoutRatio));

        if (!Enum.IsDefined(HarmonicSecurityMode))
            throw new ArgumentOutOfRangeException(nameof(HarmonicSecurityMode));
        ValidateRatio(SecondHarmonicThreshold, nameof(SecondHarmonicThreshold));
        ValidateRatio(FifthHarmonicThreshold, nameof(FifthHarmonicThreshold));
        ValidateNonNegative(SecondHarmonicRestraintGain, nameof(SecondHarmonicRestraintGain));
        ValidateNonNegative(FifthHarmonicRestraintGain, nameof(FifthHarmonicRestraintGain));
        HighVoltageCompensation.Validate(nameof(HighVoltageCompensation));
        LowVoltageCompensation.Validate(nameof(LowVoltageCompensation));
    }

    public string CanonicalIdentity() => ProtectionSettings.CanonicalJoin(
        Enabled,
        MinimumPickupPu,
        Slope1,
        Slope2,
        SlopeBreakpointPu,
        OperateDelay.Ticks,
        DropoutRatio,
        HighSetEnabled,
        HighSetPickupPu,
        HighSetDelay.Ticks,
        HighSetDropoutRatio,
        HighSetBypassesHarmonicSecurity,
        HarmonicSecurityMode,
        SecondHarmonicThreshold,
        FifthHarmonicThreshold,
        SecondHarmonicRestraintGain,
        FifthHarmonicRestraintGain,
        HighVoltageCompensation.CanonicalIdentity(),
        LowVoltageCompensation.CanonicalIdentity());

    private static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateSlope(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 5)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateRatio(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 5)
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
}

public sealed record RestrictedEarthFaultSettings
{
    public bool Enabled { get; init; }
    public double MinimumPickupPu { get; init; } = 0.15;
    public double BiasSlope { get; init; } = 0.20;
    public TimeSpan OperateDelay { get; init; } = TimeSpan.FromMilliseconds(20);
    public double DropoutRatio { get; init; } = 0.90;
    public double NeutralCurrentScale { get; init; } = 1.0;
    public bool ReverseNeutralPolarity { get; init; }

    public void Validate(string name)
    {
        if (!double.IsFinite(MinimumPickupPu) || MinimumPickupPu <= 0)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(MinimumPickupPu)}");
        if (!double.IsFinite(BiasSlope) || BiasSlope < 0 || BiasSlope > 5)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(BiasSlope)}");
        if (OperateDelay < TimeSpan.Zero || OperateDelay > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException($"{name}.{nameof(OperateDelay)}");
        if (!double.IsFinite(DropoutRatio) || DropoutRatio is <= 0 or > 1)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(DropoutRatio)}");
        if (!double.IsFinite(NeutralCurrentScale) || NeutralCurrentScale <= 0 || NeutralCurrentScale > 1_000_000)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(NeutralCurrentScale)}");
    }

    public string CanonicalIdentity() => ProtectionSettings.CanonicalJoin(
        Enabled,
        MinimumPickupPu,
        BiasSlope,
        OperateDelay.Ticks,
        DropoutRatio,
        NeutralCurrentScale,
        ReverseNeutralPolarity);
}

public sealed record TransformerProtectionSettings
{
    public TransformerDifferentialSettings Differential87T { get; init; } = new();
    public RestrictedEarthFaultSettings RefHighVoltage { get; init; } = new();
    public RestrictedEarthFaultSettings RefLowVoltage { get; init; } = new();

    public bool AnyEnabled => Differential87T.AnyEnabled || RefHighVoltage.Enabled || RefLowVoltage.Enabled;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Differential87T);
        ArgumentNullException.ThrowIfNull(RefHighVoltage);
        ArgumentNullException.ThrowIfNull(RefLowVoltage);
        Differential87T.Validate();
        RefHighVoltage.Validate(nameof(RefHighVoltage));
        RefLowVoltage.Validate(nameof(RefLowVoltage));
    }

    public string Fingerprint()
    {
        var canonical = ProtectionSettings.CanonicalJoin(
            Differential87T.CanonicalIdentity(),
            RefHighVoltage.CanonicalIdentity(),
            RefLowVoltage.CanonicalIdentity());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
