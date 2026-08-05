using System.Globalization;

namespace Arvrel.Protection;

/// <summary>
/// Canonicalizes a phasor frame to the visual resolution of the small relay LCD.
/// Sub-pixel magnitude/angle changes therefore do not invalidate the instrument,
/// while meaningful engineering changes remain immediately visible.
/// </summary>
public static class RelayLcdPhasorStabilizer
{
    public const double MagnitudeQuantum = 0.01;
    public const double AngleQuantumDegrees = 0.5;
    public const double FrequencyQuantumHz = 0.01;

    public static PhasorDisplayFrame Canonicalize(PhasorDisplayFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!frame.IsAvailable)
            return frame;

        var vectors = frame.Vectors
            .Select(vector => vector with
            {
                Magnitude = Quantize(vector.Magnitude, MagnitudeQuantum),
                AngleDegrees = NormalizeAndQuantizeAngle(vector.AngleDegrees)
            })
            .ToArray();

        var maximumCurrent = vectors
            .Where(vector => vector.QuantityKind == "I")
            .Select(vector => vector.Magnitude)
            .DefaultIfEmpty(0)
            .Max();
        var maximumVoltage = vectors
            .Where(vector => vector.QuantityKind == "V")
            .Select(vector => vector.Magnitude)
            .DefaultIfEmpty(0)
            .Max();

        return frame with
        {
            Vectors = vectors,
            ReferenceAngleDegrees = NormalizeAndQuantizeAngle(frame.ReferenceAngleDegrees),
            MaximumCurrent = maximumCurrent,
            MaximumVoltage = maximumVoltage,
            PositiveSequenceAngleDifferenceDegrees = QuantizeFiniteAngle(frame.PositiveSequenceAngleDifferenceDegrees),
            ResidualAngleDifferenceDegrees = QuantizeFiniteAngle(frame.ResidualAngleDifferenceDegrees),
            FrequencyHz = Quantize(frame.FrequencyHz, FrequencyQuantumHz)
        };
    }

    public static string Signature(PhasorDisplayFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!frame.IsAvailable)
            return $"{frame.Mode}|UNAVAILABLE|{frame.Status}";

        var vectors = string.Join(
            ";",
            frame.Vectors.Select(vector => string.Create(
                CultureInfo.InvariantCulture,
                $"{vector.Key}:{vector.QuantityKind}:{vector.Magnitude:0.###}:{vector.AngleDegrees:0.###}")));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{frame.Mode}|{frame.ReferenceLabel}|{frame.ReferenceAngleDegrees:0.###}|" +
            $"{frame.MaximumCurrent:0.###}|{frame.MaximumVoltage:0.###}|" +
            $"{frame.FrequencyHz:0.###}|{vectors}");
    }

    private static double NormalizeAndQuantizeAngle(double value)
        => PhasorDisplayProjector.NormalizeDegrees(Quantize(value, AngleQuantumDegrees));

    private static double QuantizeFiniteAngle(double value)
        => double.IsFinite(value) ? NormalizeAndQuantizeAngle(value) : value;

    private static double Quantize(double value, double quantum)
    {
        if (!double.IsFinite(value) || quantum <= 0)
            return value;
        var quantized = Math.Round(value / quantum, MidpointRounding.AwayFromZero) * quantum;
        return Math.Abs(quantized) < quantum / 2 ? 0 : quantized;
    }
}
