using System.Security.Cryptography;
using System.Text;

namespace Arvrel.Protection;

public sealed record TransformerExternalFaultSecuritySettings
{
    public bool Enabled { get; init; }
    public double MinimumBiasPu { get; init; } = 2.0;
    public double MinimumBiasIncreasePu { get; init; } = 0.50;
    public double MaximumInitialDifferentialToBiasRatio { get; init; } = 0.20;
    public TimeSpan ArmingDuration { get; init; } = TimeSpan.FromMilliseconds(80);
    public TimeSpan SecurityHold { get; init; } = TimeSpan.FromMilliseconds(120);
    public double DistortionRatioThreshold { get; init; } = 0.12;
    public double PeakAsymmetryThreshold { get; init; } = 0.08;
    public double SevereDistortionRatioThreshold { get; init; } = 0.25;
    public bool SuperviseHighSet { get; init; } = true;
    public bool SuperviseRef { get; init; } = true;

    public void Validate()
    {
        ValidatePositive(MinimumBiasPu, nameof(MinimumBiasPu));
        ValidatePositive(MinimumBiasIncreasePu, nameof(MinimumBiasIncreasePu));
        ValidateRatio(MaximumInitialDifferentialToBiasRatio, nameof(MaximumInitialDifferentialToBiasRatio));
        ValidateDuration(ArmingDuration, nameof(ArmingDuration));
        ValidateDuration(SecurityHold, nameof(SecurityHold));
        ValidateRatio(DistortionRatioThreshold, nameof(DistortionRatioThreshold));
        if (!double.IsFinite(PeakAsymmetryThreshold) || PeakAsymmetryThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(PeakAsymmetryThreshold));
        ValidateRatio(SevereDistortionRatioThreshold, nameof(SevereDistortionRatioThreshold));
        if (SevereDistortionRatioThreshold < DistortionRatioThreshold)
            throw new ArgumentOutOfRangeException(
                nameof(SevereDistortionRatioThreshold),
                "Severe distortion threshold must be greater than or equal to the normal distortion threshold.");
    }

    public string CanonicalIdentity() => ProtectionSettings.CanonicalJoin(
        Enabled,
        MinimumBiasPu,
        MinimumBiasIncreasePu,
        MaximumInitialDifferentialToBiasRatio,
        ArmingDuration.Ticks,
        SecurityHold.Ticks,
        DistortionRatioThreshold,
        PeakAsymmetryThreshold,
        SevereDistortionRatioThreshold,
        SuperviseHighSet,
        SuperviseRef);

    private static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateRatio(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 5)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateDuration(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value > TimeSpan.FromSeconds(10))
            throw new ArgumentOutOfRangeException(name);
    }
}

public sealed record TransformerDifferentialSettings
{
    /// <summary>
    /// Minimum restrained differential pickup in per unit. This is the generic
    /// equivalent of the commonly used transformer-differential Is1 setting.
    /// </summary>
    public double MinimumPickupPu { get; init; } = 0.30;

    public bool Enabled { get; init; }

    /// <summary>Low-bias percentage slope as a ratio (K1; 0.25 = 25%).</summary>
    public double Slope1 { get; init; } = 0.25;

    /// <summary>High-bias percentage slope as a ratio (K2; 0.60 = 60%).</summary>
    public double Slope2 { get; init; } = 0.60;

    /// <summary>Bias-current breakpoint in per unit (generic Is2).</summary>
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

    public TransformerExternalFaultSecuritySettings ExternalFaultSecurity { get; init; } = new();
    public TransformerWindingCompensation HighVoltageCompensation { get; init; } = new();
    public TransformerWindingCompensation LowVoltageCompensation { get; init; } = new();

    public bool AnyEnabled => Enabled || HighSetEnabled;

    // Familiar aliases used in transformer differential application guides. They are
    // read-only aliases so the persisted model remains vendor-neutral and unambiguous.
    public double Is1Pu => MinimumPickupPu;
    public double K1 => Slope1;
    public double Is2Pu => SlopeBreakpointPu;
    public double K2 => Slope2;

    /// <summary>
    /// Continuous two-slope percentage-biased characteristic:
    /// Is1 + K1*Ibias below Is2, then a continuous K2 segment above Is2.
    /// </summary>
    public double StandardSlopeThresholdPu(double biasCurrentPu)
    {
        if (!double.IsFinite(biasCurrentPu) || biasCurrentPu < 0)
            throw new ArgumentOutOfRangeException(nameof(biasCurrentPu));

        var firstRegion = Math.Min(biasCurrentPu, SlopeBreakpointPu);
        var secondRegion = Math.Max(0, biasCurrentPu - SlopeBreakpointPu);
        return MinimumPickupPu + Slope1 * firstRegion + Slope2 * secondRegion;
    }

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
        ArgumentNullException.ThrowIfNull(ExternalFaultSecurity);
        ExternalFaultSecurity.Validate();
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
        ExternalFaultSecurity.CanonicalIdentity(),
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
