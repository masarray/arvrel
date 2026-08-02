using System.Numerics;

namespace Arvrel.Protection;

public enum PhasorDisplayMode
{
    Current,
    Voltage,
    Sequence
}

public sealed record PhasorDisplayVector(
    string Key,
    string Label,
    string QuantityKind,
    double Magnitude,
    double AngleDegrees,
    bool IsResidual = false,
    bool IsNegativeSequence = false);

public sealed record PhasorDisplayFrame(
    PhasorDisplayMode Mode,
    IReadOnlyList<PhasorDisplayVector> Vectors,
    string ReferenceLabel,
    double ReferenceAngleDegrees,
    double MaximumCurrent,
    double MaximumVoltage,
    double PositiveSequenceAngleDifferenceDegrees,
    double ResidualAngleDifferenceDegrees,
    double FrequencyHz,
    bool IsAvailable,
    string Status)
{
    public static PhasorDisplayFrame Unavailable(PhasorDisplayMode mode, string status) => new(
        mode,
        Array.Empty<PhasorDisplayVector>(),
        "NO REFERENCE",
        0,
        0,
        0,
        double.NaN,
        double.NaN,
        0,
        false,
        status);
}

/// <summary>
/// Converts the deterministic relay phasor set into a stable operator display frame.
/// The display convention follows the ArSubsv subscriber: all vectors are rotated to a
/// common VA reference when voltage is available, while current and voltage magnitudes
/// retain independent scales. Common rotation never changes directional V/I relations.
/// </summary>
public static class PhasorDisplayProjector
{
    private const double MinimumMagnitude = 1e-9;

    public static PhasorDisplayFrame Project(PhasorMeasurementSet? source, PhasorDisplayMode mode)
    {
        if (source is null)
            return PhasorDisplayFrame.Unavailable(mode, "Waiting for a complete 4I+4V one-cycle phasor window.");

        var reference = ResolveReference(source, out var referenceLabel);
        var referenceAngle = ToDegrees(reference.Phase);
        var vectors = mode switch
        {
            PhasorDisplayMode.Current => CurrentVectors(source, referenceAngle),
            PhasorDisplayMode.Voltage => VoltageVectors(source, referenceAngle),
            PhasorDisplayMode.Sequence => SequenceVectors(source, referenceAngle),
            _ => Array.Empty<PhasorDisplayVector>()
        };

        var maxCurrent = vectors
            .Where(vector => vector.QuantityKind == "I")
            .Select(vector => vector.Magnitude)
            .DefaultIfEmpty(0)
            .Max();
        var maxVoltage = vectors
            .Where(vector => vector.QuantityKind == "V")
            .Select(vector => vector.Magnitude)
            .DefaultIfEmpty(0)
            .Max();

        var positiveAngle = RelativeAngle(source.PositiveSequenceVoltage, source.PositiveSequenceCurrent);
        var residualAngle = RelativeAngle(source.ResidualVoltage, source.ResidualCurrent);
        var status = mode switch
        {
            PhasorDisplayMode.Current => "IA · IB · IC · 3I0",
            PhasorDisplayMode.Voltage => "VA · VB · VC · 3V0",
            _ => "I1/I2/3I0 · V1/V2/3V0"
        };

        return new PhasorDisplayFrame(
            mode,
            vectors,
            referenceLabel,
            referenceAngle,
            maxCurrent,
            maxVoltage,
            positiveAngle,
            residualAngle,
            source.FrequencyHz,
            true,
            status);
    }

    private static IReadOnlyList<PhasorDisplayVector> CurrentVectors(PhasorMeasurementSet source, double referenceAngle) =>
    [
        Vector("IA", "R / IA", "I", source.PhaseACurrent, referenceAngle),
        Vector("IB", "S / IB", "I", source.PhaseBCurrent, referenceAngle),
        Vector("IC", "T / IC", "I", source.PhaseCCurrent, referenceAngle),
        Vector("3I0", "3I0", "I", source.ResidualCurrent, referenceAngle, residual: true)
    ];

    private static IReadOnlyList<PhasorDisplayVector> VoltageVectors(PhasorMeasurementSet source, double referenceAngle) =>
    [
        Vector("VA", "R / VA", "V", source.PhaseAVoltage, referenceAngle),
        Vector("VB", "S / VB", "V", source.PhaseBVoltage, referenceAngle),
        Vector("VC", "T / VC", "V", source.PhaseCVoltage, referenceAngle),
        Vector("3V0", "3V0", "V", source.ResidualVoltage, referenceAngle, residual: true)
    ];

    private static IReadOnlyList<PhasorDisplayVector> SequenceVectors(PhasorMeasurementSet source, double referenceAngle) =>
    [
        Vector("I1", "I1", "I", source.PositiveSequenceCurrent, referenceAngle),
        Vector("I2", "I2", "I", source.NegativeSequenceCurrent, referenceAngle, negative: true),
        Vector("3I0", "3I0", "I", source.ResidualCurrent, referenceAngle, residual: true),
        Vector("V1", "V1", "V", source.PositiveSequenceVoltage, referenceAngle),
        Vector("V2", "V2", "V", source.NegativeSequenceVoltage, referenceAngle, negative: true),
        Vector("3V0", "3V0", "V", source.ResidualVoltage, referenceAngle, residual: true)
    ];

    private static PhasorDisplayVector Vector(
        string key,
        string label,
        string quantityKind,
        Complex value,
        double referenceAngle,
        bool residual = false,
        bool negative = false)
        => new(
            key,
            label,
            quantityKind,
            value.Magnitude,
            NormalizeDegrees(ToDegrees(value.Phase) - referenceAngle),
            residual,
            negative);

    private static Complex ResolveReference(PhasorMeasurementSet source, out string label)
    {
        if (source.PhaseAVoltage.Magnitude > MinimumMagnitude)
        {
            label = "VA = 0°";
            return source.PhaseAVoltage;
        }

        if (source.PositiveSequenceVoltage.Magnitude > MinimumMagnitude)
        {
            label = "V1 = 0°";
            return source.PositiveSequenceVoltage;
        }

        if (source.PhaseACurrent.Magnitude > MinimumMagnitude)
        {
            label = "IA = 0°";
            return source.PhaseACurrent;
        }

        label = "ABSOLUTE";
        return Complex.One;
    }

    private static double RelativeAngle(Complex voltage, Complex current)
    {
        if (voltage.Magnitude <= MinimumMagnitude || current.Magnitude <= MinimumMagnitude)
            return double.NaN;
        return NormalizeDegrees(ToDegrees(current.Phase - voltage.Phase));
    }

    public static double NormalizeDegrees(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle <= -180) angle += 360;
        return angle;
    }

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
