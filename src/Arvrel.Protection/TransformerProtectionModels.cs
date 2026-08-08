using System.Numerics;

namespace Arvrel.Protection;

public enum TransformerPhase
{
    A,
    B,
    C
}

public enum TransformerHarmonicSecurityMode
{
    Disabled,
    Blocking,
    Restraint
}

public sealed record TransformerPhaseCurrents(Complex PhaseA, Complex PhaseB, Complex PhaseC)
{
    public Complex ZeroSequence => (PhaseA + PhaseB + PhaseC) / 3;
    public Complex Residual => PhaseA + PhaseB + PhaseC;

    public Complex For(TransformerPhase phase) => phase switch
    {
        TransformerPhase.A => PhaseA,
        TransformerPhase.B => PhaseB,
        TransformerPhase.C => PhaseC,
        _ => Complex.Zero
    };

    public TransformerPhaseCurrents RemoveZeroSequence()
    {
        var zero = ZeroSequence;
        return new(PhaseA - zero, PhaseB - zero, PhaseC - zero);
    }
}

public sealed record TransformerHarmonicRatios(double PhaseA, double PhaseB, double PhaseC)
{
    public static TransformerHarmonicRatios Zero { get; } = new(0, 0, 0);

    public double For(TransformerPhase phase) => phase switch
    {
        TransformerPhase.A => PhaseA,
        TransformerPhase.B => PhaseB,
        TransformerPhase.C => PhaseC,
        _ => 0
    };

    public void Validate(string name)
    {
        ValidateRatio(PhaseA, $"{name}.PhaseA");
        ValidateRatio(PhaseB, $"{name}.PhaseB");
        ValidateRatio(PhaseC, $"{name}.PhaseC");
    }

    private static void ValidateRatio(double value, string name)
    {
        // This is a measured quantity, not a setting. Very low fundamental current can
        // legitimately produce ratios above 500%, so do not impose an arbitrary ceiling.
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name);
    }
}

/// <summary>
/// Explicit current-domain compensation applied before transformer differential logic.
/// CurrentScaleToPu converts the source current into a common per-unit current base.
/// PhaseShiftDegrees and ReversePolarity align the two winding current references.
/// RemoveZeroSequence is intended for transformer differential compensation only;
/// REF always uses the local uncompensated residual current path.
/// </summary>
public sealed record TransformerWindingCompensation
{
    public double CurrentScaleToPu { get; init; } = 1;
    public double PhaseShiftDegrees { get; init; }
    public bool ReversePolarity { get; init; }
    public bool RemoveZeroSequence { get; init; }

    public TransformerPhaseCurrents NormalizeDifferential(TransformerPhaseCurrents currents)
        => Normalize(currents, RemoveZeroSequence);

    public TransformerPhaseCurrents NormalizeLocal(TransformerPhaseCurrents currents)
        => Normalize(currents, removeZeroSequence: false);

    public Complex NormalizeNeutral(Complex current)
        => current * BuildFactor();

    public void Validate(string name)
    {
        if (!double.IsFinite(CurrentScaleToPu) || CurrentScaleToPu <= 0 || CurrentScaleToPu > 1_000_000)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(CurrentScaleToPu)}");
        if (!double.IsFinite(PhaseShiftDegrees) || PhaseShiftDegrees is < -180 or > 180)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(PhaseShiftDegrees)}");
    }

    public string CanonicalIdentity() => ProtectionSettings.CanonicalJoin(
        CurrentScaleToPu,
        PhaseShiftDegrees,
        ReversePolarity,
        RemoveZeroSequence);

    private TransformerPhaseCurrents Normalize(TransformerPhaseCurrents currents, bool removeZeroSequence)
    {
        ArgumentNullException.ThrowIfNull(currents);
        var factor = BuildFactor();
        var normalized = new TransformerPhaseCurrents(
            currents.PhaseA * factor,
            currents.PhaseB * factor,
            currents.PhaseC * factor);
        return removeZeroSequence ? normalized.RemoveZeroSequence() : normalized;
    }

    private Complex BuildFactor()
    {
        var angleRadians = PhaseShiftDegrees * Math.PI / 180;
        var rotation = Complex.FromPolarCoordinates(CurrentScaleToPu, angleRadians);
        return ReversePolarity ? -rotation : rotation;
    }
}

public sealed record TransformerWindingMeasurement(
    TransformerPhaseCurrents FundamentalCurrentA,
    SmvTrustState Trust)
{
    public Complex NeutralCurrentA { get; init; }
    public bool NeutralCurrentAvailable { get; init; }
    public TransformerHarmonicRatios SecondHarmonicRatio { get; init; } = TransformerHarmonicRatios.Zero;
    public TransformerHarmonicRatios FifthHarmonicRatio { get; init; } = TransformerHarmonicRatios.Zero;
    public TransformerCtSaturationEvidence CtSaturationEvidence { get; init; } = TransformerCtSaturationEvidence.None;

    public void Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(FundamentalCurrentA);
        ArgumentNullException.ThrowIfNull(Trust);
        ArgumentNullException.ThrowIfNull(SecondHarmonicRatio);
        ArgumentNullException.ThrowIfNull(FifthHarmonicRatio);
        ArgumentNullException.ThrowIfNull(CtSaturationEvidence);
        SecondHarmonicRatio.Validate($"{name}.{nameof(SecondHarmonicRatio)}");
        FifthHarmonicRatio.Validate($"{name}.{nameof(FifthHarmonicRatio)}");
        CtSaturationEvidence.Validate($"{name}.{nameof(CtSaturationEvidence)}");
    }
}

public sealed record TransformerMeasurementFrame(
    DateTimeOffset Timestamp,
    TransformerWindingMeasurement HighVoltage,
    TransformerWindingMeasurement LowVoltage)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(HighVoltage);
        ArgumentNullException.ThrowIfNull(LowVoltage);
        HighVoltage.Validate(nameof(HighVoltage));
        LowVoltage.Validate(nameof(LowVoltage));
    }
}

public sealed record TransformerElementSnapshot(
    string Element,
    ProtectionStageState State,
    bool Pickup,
    bool Operated,
    double Progress,
    double OperatingQuantityPu,
    double PickupThresholdPu,
    string Reason);

public sealed record TransformerDifferentialPhaseSnapshot(
    TransformerPhase Phase,
    double OperatingCurrentPu,
    double RestraintCurrentPu,
    double ThresholdPu,
    double SecondHarmonicRatio,
    double FifthHarmonicRatio,
    bool HarmonicBlocked,
    bool RestrainedPickup,
    bool RestrainedOperated,
    bool HighSetPickup,
    bool HighSetOperated,
    string Reason)
{
    public bool ExternalFaultArmed { get; init; }
    public bool HighVoltageCtSaturationSuspected { get; init; }
    public bool LowVoltageCtSaturationSuspected { get; init; }
    public bool HighVoltageExternalFaultBlocked { get; init; }
    public bool LowVoltageExternalFaultBlocked { get; init; }
    public bool ExternalFaultSecurityBlocked => HighVoltageExternalFaultBlocked || LowVoltageExternalFaultBlocked;
    public double HighVoltageCtDistortionRatio { get; init; }
    public double LowVoltageCtDistortionRatio { get; init; }
    public double HighVoltageCtPeakAsymmetry { get; init; }
    public double LowVoltageCtPeakAsymmetry { get; init; }
}

public sealed record TransformerDifferentialSnapshot(
    TransformerElementSnapshot Restrained87T,
    TransformerElementSnapshot HighSet87T,
    IReadOnlyList<TransformerDifferentialPhaseSnapshot> Phases);

public sealed record RestrictedEarthFaultSnapshot(
    TransformerElementSnapshot Element,
    double PhaseResidualPu,
    double NeutralCurrentPu,
    double OperatingCurrentPu,
    double RestraintCurrentPu,
    double ThresholdPu,
    bool NeutralCurrentAvailable)
{
    public bool ExternalFaultSecurityBlocked { get; init; }
}

public sealed record TransformerExternalFaultSecurityPhaseSnapshot(
    TransformerPhase Phase,
    bool Armed,
    bool HighVoltageSaturationSuspected,
    bool LowVoltageSaturationSuspected,
    bool HighVoltageBlocked,
    bool LowVoltageBlocked,
    double HighVoltageDistortionRatio,
    double LowVoltageDistortionRatio,
    double HighVoltagePeakAsymmetry,
    double LowVoltagePeakAsymmetry,
    string Reason)
{
    public bool Blocked => HighVoltageBlocked || LowVoltageBlocked;
}

public sealed record TransformerExternalFaultSecuritySnapshot(
    bool Enabled,
    bool AnyArmed,
    bool AnyBlocked,
    bool RefHighVoltageBlocked,
    bool RefLowVoltageBlocked,
    IReadOnlyList<TransformerExternalFaultSecurityPhaseSnapshot> Phases,
    string Reason)
{
    public static TransformerExternalFaultSecuritySnapshot Disabled { get; } = new(
        false,
        false,
        false,
        false,
        false,
        Array.Empty<TransformerExternalFaultSecurityPhaseSnapshot>(),
        "External-fault CT saturation security disabled.");
}

public sealed record TransformerProtectionSnapshot(
    DateTimeOffset Timestamp,
    TransformerDifferentialSnapshot Differential,
    RestrictedEarthFaultSnapshot RefHighVoltage,
    RestrictedEarthFaultSnapshot RefLowVoltage,
    bool TripRequested,
    bool TripLatched,
    bool Blocked,
    string ActiveElement,
    string DecisionReason)
{
    public TransformerExternalFaultSecuritySnapshot ExternalFaultSecurity { get; init; } = TransformerExternalFaultSecuritySnapshot.Disabled;
}
