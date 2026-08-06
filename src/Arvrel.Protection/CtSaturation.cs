namespace Arvrel.Protection;

/// <summary>
/// Engineering-study parameters for a single-phase protective current transformer.
/// The model uses the CT equivalent circuit and a deterministic piecewise excitation
/// curve. It is intended for relay-algorithm studies, not IEC 61869 type testing.
/// </summary>
public sealed record CtSaturationSettings
{
    public bool Enabled { get; init; }
    public double RatedSecondaryCurrentA { get; init; } = 5;
    public double KneePointVoltageRms { get; init; } = 100;
    public double SecondaryWindingResistanceOhm { get; init; } = 0.5;
    public double BurdenResistanceOhm { get; init; } = 1.0;
    public double BurdenInductanceMilliHenries { get; init; } = 0.1;
    public double ExcitationCurrentAtKneeA { get; init; } = 0.03;
    public double ExcitationCurrentAtTwiceKneeA { get; init; } = 50;
    public double SaturationExponent { get; init; } = 2;
    public double RemanencePercent { get; init; }
    public double MaximumFluxPerUnit { get; init; } = 2.5;
    public double MaximumExcitationCurrentA { get; init; } = 200;

    public static CtSaturationSettings Disabled { get; } = new();

    public static CtSaturationSettings ProtectionClass5AStudy { get; } = new()
    {
        Enabled = true
    };

    public CtSaturationSettings Normalize()
    {
        Validate();
        return this;
    }

    public void Validate()
    {
        RequireRange(RatedSecondaryCurrentA, 0.1, 20, nameof(RatedSecondaryCurrentA));
        RequireRange(KneePointVoltageRms, 1, 5_000, nameof(KneePointVoltageRms));
        RequireRange(SecondaryWindingResistanceOhm, 0, 100, nameof(SecondaryWindingResistanceOhm));
        RequireRange(BurdenResistanceOhm, 0, 100, nameof(BurdenResistanceOhm));
        RequireRange(BurdenInductanceMilliHenries, 0, 1_000, nameof(BurdenInductanceMilliHenries));
        RequireRange(ExcitationCurrentAtKneeA, 0.000001, 100, nameof(ExcitationCurrentAtKneeA));
        RequireRange(ExcitationCurrentAtTwiceKneeA, 0.000001, 10_000, nameof(ExcitationCurrentAtTwiceKneeA));
        RequireRange(SaturationExponent, 1, 8, nameof(SaturationExponent));
        RequireRange(RemanencePercent, -95, 95, nameof(RemanencePercent));
        RequireRange(MaximumFluxPerUnit, 1.05, 10, nameof(MaximumFluxPerUnit));
        RequireRange(MaximumExcitationCurrentA, 0.001, 100_000, nameof(MaximumExcitationCurrentA));

        if (ExcitationCurrentAtTwiceKneeA <= ExcitationCurrentAtKneeA)
            throw new ArgumentOutOfRangeException(
                nameof(ExcitationCurrentAtTwiceKneeA),
                "Excitation current at twice knee flux must exceed the knee-point excitation current.");
        if (MaximumExcitationCurrentA < ExcitationCurrentAtTwiceKneeA)
            throw new ArgumentOutOfRangeException(
                nameof(MaximumExcitationCurrentA),
                "Maximum excitation current must be at least the excitation current at twice knee flux.");
    }

    private static void RequireRange(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(name, $"{name} must be finite and between {minimum} and {maximum}.");
    }
}

public sealed record CtSaturationChannelDiagnostics(
    bool Enabled,
    bool Saturated,
    int SaturatedSampleCount,
    int FirstSaturatedSample,
    double FirstSaturationMilliseconds,
    double MaximumAbsoluteFluxPerUnit,
    double MaximumExcitationCurrentA,
    double MaximumSecondaryVoltageV,
    double IdealRmsA,
    double SecondaryRmsA,
    double RmsMagnitudeErrorPercent,
    double WaveformErrorPercent,
    double MinimumMagnitudeRatio)
{
    public static CtSaturationChannelDiagnostics Disabled { get; } = new(
        false,
        false,
        0,
        -1,
        double.NaN,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        1);
}

public sealed record CtSaturationChannelResult(
    double[] SecondaryCurrentA,
    double[] FluxPerUnit,
    double[] ExcitationCurrentA,
    CtSaturationChannelDiagnostics Diagnostics)
{
    public static CtSaturationChannelResult PassThrough(IReadOnlyList<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        return new CtSaturationChannelResult(
            samples.ToArray(),
            new double[samples.Count],
            new double[samples.Count],
            CtSaturationChannelDiagnostics.Disabled);
    }
}

public sealed record CtSaturationFrameDiagnostics(
    CtSaturationChannelDiagnostics PhaseA,
    CtSaturationChannelDiagnostics PhaseB,
    CtSaturationChannelDiagnostics PhaseC,
    CtSaturationChannelDiagnostics Neutral)
{
    public static CtSaturationFrameDiagnostics Disabled { get; } = new(
        CtSaturationChannelDiagnostics.Disabled,
        CtSaturationChannelDiagnostics.Disabled,
        CtSaturationChannelDiagnostics.Disabled,
        CtSaturationChannelDiagnostics.Disabled);

    public bool Enabled => PhaseA.Enabled || PhaseB.Enabled || PhaseC.Enabled || Neutral.Enabled;
    public bool Saturated => PhaseA.Saturated || PhaseB.Saturated || PhaseC.Saturated || Neutral.Saturated;
    public int SaturatedChannelCount =>
        (PhaseA.Saturated ? 1 : 0) +
        (PhaseB.Saturated ? 1 : 0) +
        (PhaseC.Saturated ? 1 : 0) +
        (Neutral.Saturated ? 1 : 0);

    public string Summary => !Enabled
        ? "CT ideal"
        : Saturated
            ? $"CT SAT · {SaturatedChannelCount} channel(s)"
            : "CT nonlinear · unsaturated";
}

/// <summary>
/// Deterministic CT equivalent-circuit model:
/// ideal referred secondary current = relay current + excitation current;
/// burden voltage integrates core flux; a piecewise excitation curve produces
/// ratio error, waveform distortion, remanence effects, and saturation onset.
/// </summary>
public static class CtSaturationModel
{
    private const int SolverIterations = 6;
    private const double Relaxation = 0.45;

    public static CtSaturationChannelResult Apply(
        IReadOnlyList<double> idealSecondaryCurrentA,
        double sampleRateHz,
        double frequencyHz,
        CtSaturationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(idealSecondaryCurrentA);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        if (!double.IsFinite(sampleRateHz) || sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));
        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(frequencyHz));

        for (var index = 0; index < idealSecondaryCurrentA.Count; index++)
        {
            if (!double.IsFinite(idealSecondaryCurrentA[index]))
                throw new ArgumentOutOfRangeException(
                    nameof(idealSecondaryCurrentA),
                    $"Current sample {index} is not finite.");
        }

        if (!settings.Enabled || idealSecondaryCurrentA.Count == 0)
            return CtSaturationChannelResult.PassThrough(idealSecondaryCurrentA);

        var count = idealSecondaryCurrentA.Count;
        var secondary = new double[count];
        var fluxPerUnit = new double[count];
        var excitation = new double[count];
        var sampleInterval = 1d / sampleRateHz;
        var angularFrequency = 2 * Math.PI * frequencyHz;
        var kneeFluxLinkage = Math.Sqrt(2) * settings.KneePointVoltageRms / angularFrequency;
        var totalResistance = settings.SecondaryWindingResistanceOhm + settings.BurdenResistanceOhm;
        var burdenInductance = settings.BurdenInductanceMilliHenries / 1_000d;
        var maximumFlux = settings.MaximumFluxPerUnit * kneeFluxLinkage;

        var previousFlux = settings.RemanencePercent / 100d * kneeFluxLinkage;
        var previousSecondary = 0d;
        var previousVoltage = 0d;
        var saturatedSamples = 0;
        var firstSaturatedSample = -1;
        var maximumAbsoluteFluxPerUnit = Math.Abs(previousFlux / kneeFluxLinkage);
        var maximumExcitation = 0d;
        var maximumVoltage = 0d;
        var minimumMagnitudeRatio = 1d;
        var ratioWasMeasured = false;

        var idealSquareSum = 0d;
        var secondarySquareSum = 0d;
        var errorSquareSum = 0d;

        for (var index = 0; index < count; index++)
        {
            var ideal = idealSecondaryCurrentA[index];
            var candidateSecondary = ideal - ExcitationCurrent(previousFlux, kneeFluxLinkage, settings);
            var candidateFlux = previousFlux;
            var candidateVoltage = previousVoltage;
            var candidateExcitation = 0d;

            for (var iteration = 0; iteration < SolverIterations; iteration++)
            {
                var derivative = (candidateSecondary - previousSecondary) / sampleInterval;
                candidateVoltage = totalResistance * candidateSecondary + burdenInductance * derivative;
                candidateFlux = previousFlux + 0.5 * (previousVoltage + candidateVoltage) * sampleInterval;
                candidateFlux = Math.Clamp(candidateFlux, -maximumFlux, maximumFlux);
                candidateExcitation = ExcitationCurrent(candidateFlux, kneeFluxLinkage, settings);
                var targetSecondary = ideal - candidateExcitation;
                candidateSecondary += Relaxation * (targetSecondary - candidateSecondary);
            }

            candidateExcitation = ExcitationCurrent(candidateFlux, kneeFluxLinkage, settings);
            candidateSecondary = ideal - candidateExcitation;
            candidateVoltage = totalResistance * candidateSecondary +
                burdenInductance * (candidateSecondary - previousSecondary) / sampleInterval;

            secondary[index] = candidateSecondary;
            fluxPerUnit[index] = candidateFlux / kneeFluxLinkage;
            excitation[index] = candidateExcitation;

            var absoluteFluxPerUnit = Math.Abs(fluxPerUnit[index]);
            maximumAbsoluteFluxPerUnit = Math.Max(maximumAbsoluteFluxPerUnit, absoluteFluxPerUnit);
            maximumExcitation = Math.Max(maximumExcitation, Math.Abs(candidateExcitation));
            maximumVoltage = Math.Max(maximumVoltage, Math.Abs(candidateVoltage));
            if (absoluteFluxPerUnit >= 1)
            {
                saturatedSamples++;
                if (firstSaturatedSample < 0)
                    firstSaturatedSample = index;
            }

            if (Math.Abs(ideal) >= settings.RatedSecondaryCurrentA * 0.1)
            {
                var ratio = Math.Abs(candidateSecondary) / Math.Abs(ideal);
                minimumMagnitudeRatio = ratioWasMeasured
                    ? Math.Min(minimumMagnitudeRatio, ratio)
                    : ratio;
                ratioWasMeasured = true;
            }

            idealSquareSum += ideal * ideal;
            secondarySquareSum += candidateSecondary * candidateSecondary;
            var error = candidateSecondary - ideal;
            errorSquareSum += error * error;

            previousSecondary = candidateSecondary;
            previousVoltage = candidateVoltage;
            previousFlux = candidateFlux;
        }

        var idealRms = Math.Sqrt(idealSquareSum / count);
        var secondaryRms = Math.Sqrt(secondarySquareSum / count);
        var rmsMagnitudeErrorPercent = idealRms > 1e-12
            ? 100 * (secondaryRms - idealRms) / idealRms
            : 0;
        var waveformErrorPercent = idealRms > 1e-12
            ? 100 * Math.Sqrt(errorSquareSum / count) / idealRms
            : 0;
        var firstSaturationMilliseconds = firstSaturatedSample >= 0
            ? firstSaturatedSample * 1_000d / sampleRateHz
            : double.NaN;

        var diagnostics = new CtSaturationChannelDiagnostics(
            true,
            saturatedSamples > 0,
            saturatedSamples,
            firstSaturatedSample,
            firstSaturationMilliseconds,
            maximumAbsoluteFluxPerUnit,
            maximumExcitation,
            maximumVoltage,
            idealRms,
            secondaryRms,
            rmsMagnitudeErrorPercent,
            waveformErrorPercent,
            ratioWasMeasured ? minimumMagnitudeRatio : 1);

        return new CtSaturationChannelResult(
            secondary,
            fluxPerUnit,
            excitation,
            diagnostics);
    }

    private static double ExcitationCurrent(
        double fluxLinkage,
        double kneeFluxLinkage,
        CtSaturationSettings settings)
    {
        var normalizedFlux = Math.Abs(fluxLinkage) / kneeFluxLinkage;
        var magnitude = normalizedFlux <= 1
            ? settings.ExcitationCurrentAtKneeA * Math.Pow(normalizedFlux, 3)
            : settings.ExcitationCurrentAtKneeA +
              (settings.ExcitationCurrentAtTwiceKneeA - settings.ExcitationCurrentAtKneeA) *
              Math.Pow(normalizedFlux - 1, settings.SaturationExponent);

        magnitude = Math.Min(magnitude, settings.MaximumExcitationCurrentA);
        return Math.CopySign(magnitude, fluxLinkage);
    }
}