namespace Arvrel.Protection;

public enum TransformerIedFunctionStatus
{
    Implemented,
    Planned
}

public sealed record TransformerIedFunctionDescriptor(
    string AnsiCode,
    string Name,
    TransformerIedFunctionStatus Status,
    string MeasurementDomain,
    string Notes);

/// <summary>
/// Vendor-neutral capability profile for ARVREL's two-winding transformer
/// differential IED. This is intentionally metadata-only: protection execution is
/// owned by TransformerProtectionEngine and acquisition is owned by a future paired
/// process-bus adapter.
/// </summary>
public static class TransformerDifferentialIedProfile
{
    public const string ModelId = "transformer-differential-2w";
    public const string DisplayName = "Transformer Differential Protection Relay";
    public const string Iec61850DifferentialLogicalNode = "PDIF";

    public static IReadOnlyList<TransformerIedFunctionDescriptor> Functions { get; } =
    [
        new("87T", "Biased transformer differential", TransformerIedFunctionStatus.Implemented,
            "Paired HV/LV fundamental currents", "Dual-slope percentage restraint with harmonic security."),
        new("87T-HS", "Unrestrained transformer differential high-set", TransformerIedFunctionStatus.Implemented,
            "Paired HV/LV fundamental currents", "Fast high-set stage with configurable harmonic bypass."),
        new("87N-HV", "Restricted earth fault — HV winding", TransformerIedFunctionStatus.Implemented,
            "HV phase residual + independent neutral CT", "Low-impedance biased REF; neutral CT is mandatory."),
        new("87N-LV", "Restricted earth fault — LV winding", TransformerIedFunctionStatus.Implemented,
            "LV phase residual + independent neutral CT", "Low-impedance biased REF; neutral CT is mandatory."),
        new("24", "Overfluxing / V/Hz", TransformerIedFunctionStatus.Planned,
            "Voltage + frequency", "Natural next transformer-specific protection stage."),
        new("49", "Transformer thermal protection", TransformerIedFunctionStatus.Planned,
            "Load current + thermal model", "Requires transformer thermal constants and ambient/oil context."),
        new("50/51", "Winding backup overcurrent", TransformerIedFunctionStatus.Planned,
            "Per-winding current", "Reuse common overcurrent primitives through a winding adapter."),
        new("50BF", "Breaker failure", TransformerIedFunctionStatus.Planned,
            "Trip command + breaker current/status", "Requires explicit breaker topology and timers."),
        new("63", "Mechanical transformer protection input", TransformerIedFunctionStatus.Planned,
            "Binary input", "Buchholz / sudden-pressure style virtual binary interfaces."),
        new("86", "Transformer lockout logic", TransformerIedFunctionStatus.Planned,
            "Protection operation matrix", "Virtual-only lockout composition; no physical output path.")
    ];

    public static TransformerProtectionSettings CreateSafeDefaults() => new();
}
