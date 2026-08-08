namespace Arvrel.Protection;

/// <summary>
/// Vendor-neutral waveform-quality policy used to derive CT saturation evidence.
/// The estimator intentionally reports measurable waveform distortion only; the
/// protection engine decides whether that evidence is relevant to an external fault.
/// </summary>
public sealed record TransformerCtSaturationEstimatorSettings
{
    public int WindowCycles { get; init; } = 1;
    public int MinimumSamplesPerCycle { get; init; } = 16;
    public double MinimumFundamentalRms { get; init; } = 1e-6;

    public void Validate()
    {
        if (WindowCycles is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(WindowCycles));
        if (MinimumSamplesPerCycle is < 8 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(MinimumSamplesPerCycle));
        if (!double.IsFinite(MinimumFundamentalRms) || MinimumFundamentalRms <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumFundamentalRms));
    }
}

public sealed record TransformerCtSaturationPhaseEvidence(
    double FundamentalRms,
    double ResidualDistortionRms,
    double DistortionRatio,
    double PeakAsymmetry,
    bool Reliable,
    string Reason)
{
    public static TransformerCtSaturationPhaseEvidence None { get; } = new(
        0,
        0,
        0,
        0,
        false,
        "CT saturation waveform evidence not available.");

    public void Validate(string name)
    {
        ValidateNonNegative(FundamentalRms, $"{name}.{nameof(FundamentalRms)}");
        ValidateNonNegative(ResidualDistortionRms, $"{name}.{nameof(ResidualDistortionRms)}");
        ValidateNonNegative(DistortionRatio, $"{name}.{nameof(DistortionRatio)}");
        if (!double.IsFinite(PeakAsymmetry) || PeakAsymmetry is < 0 or > 1)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(PeakAsymmetry)}");
        ArgumentNullException.ThrowIfNull(Reason);
    }

    private static void ValidateNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name);
    }
}

public sealed record TransformerCtSaturationEvidence(
    TransformerCtSaturationPhaseEvidence PhaseA,
    TransformerCtSaturationPhaseEvidence PhaseB,
    TransformerCtSaturationPhaseEvidence PhaseC)
{
    public static TransformerCtSaturationEvidence None { get; } = new(
        TransformerCtSaturationPhaseEvidence.None,
        TransformerCtSaturationPhaseEvidence.None,
        TransformerCtSaturationPhaseEvidence.None);

    public TransformerCtSaturationPhaseEvidence For(TransformerPhase phase) => phase switch
    {
        TransformerPhase.A => PhaseA,
        TransformerPhase.B => PhaseB,
        TransformerPhase.C => PhaseC,
        _ => TransformerCtSaturationPhaseEvidence.None
    };

    public int ReliablePhaseCount =>
        (PhaseA.Reliable ? 1 : 0) +
        (PhaseB.Reliable ? 1 : 0) +
        (PhaseC.Reliable ? 1 : 0);

    public void Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(PhaseA);
        ArgumentNullException.ThrowIfNull(PhaseB);
        ArgumentNullException.ThrowIfNull(PhaseC);
        PhaseA.Validate($"{name}.{nameof(PhaseA)}");
        PhaseB.Validate($"{name}.{nameof(PhaseB)}");
        PhaseC.Validate($"{name}.{nameof(PhaseC)}");
    }
}

public static class TransformerCtSaturationEstimator
{
    public static TransformerCtSaturationPhaseEvidence EstimatePhase(
        IReadOnlyList<double> samples,
        int samplesPerCycle,
        TransformerCtSaturationEstimatorSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var policy = settings ?? new TransformerCtSaturationEstimatorSettings();
        policy.Validate();
        ValidateSampling(samples, samplesPerCycle, policy);

        var count = checked(samplesPerCycle * policy.WindowCycles);
        var start = samples.Count - count;
        var mean = 0.0;
        for (var index = start; index < samples.Count; index++)
        {
            var sample = samples[index];
            if (!double.IsFinite(sample))
                throw new ArgumentException("CT saturation estimation requires finite waveform samples.", nameof(samples));
            mean += sample;
        }
        mean /= count;

        var cosine = 0.0;
        var sine = 0.0;
        var positivePeak = 0.0;
        var negativePeak = 0.0;
        for (var offset = 0; offset < count; offset++)
        {
            var centered = samples[start + offset] - mean;
            var angle = 2 * Math.PI * offset / samplesPerCycle;
            cosine += centered * Math.Cos(angle);
            sine += centered * Math.Sin(angle);
            positivePeak = Math.Max(positivePeak, centered);
            negativePeak = Math.Max(negativePeak, -centered);
        }

        var cosineAmplitude = 2 * cosine / count;
        var sineAmplitude = 2 * sine / count;
        var fundamentalRms = Math.Sqrt(cosineAmplitude * cosineAmplitude + sineAmplitude * sineAmplitude) / Math.Sqrt(2);

        var residualSquareSum = 0.0;
        for (var offset = 0; offset < count; offset++)
        {
            var centered = samples[start + offset] - mean;
            var angle = 2 * Math.PI * offset / samplesPerCycle;
            var fittedFundamental = cosineAmplitude * Math.Cos(angle) + sineAmplitude * Math.Sin(angle);
            var residual = centered - fittedFundamental;
            residualSquareSum += residual * residual;
        }

        var residualRms = Math.Sqrt(residualSquareSum / count);
        var peak = Math.Max(positivePeak, negativePeak);
        var peakAsymmetry = peak <= policy.MinimumFundamentalRms
            ? 0
            : Math.Abs(positivePeak - negativePeak) / peak;

        if (fundamentalRms < policy.MinimumFundamentalRms)
        {
            return new TransformerCtSaturationPhaseEvidence(
                fundamentalRms,
                residualRms,
                0,
                peakAsymmetry,
                false,
                $"Fundamental RMS {fundamentalRms:0.######} is below the CT-evidence floor {policy.MinimumFundamentalRms:0.######}.");
        }

        var distortionRatio = residualRms / fundamentalRms;
        return new TransformerCtSaturationPhaseEvidence(
            fundamentalRms,
            residualRms,
            distortionRatio,
            peakAsymmetry,
            true,
            "Fundamental-fit residual distortion and peak asymmetry evidence ready.");
    }

    public static TransformerCtSaturationEvidence EstimateThreePhase(
        IReadOnlyList<double> phaseA,
        IReadOnlyList<double> phaseB,
        IReadOnlyList<double> phaseC,
        int samplesPerCycle,
        TransformerCtSaturationEstimatorSettings? settings = null)
        => new(
            EstimatePhase(phaseA, samplesPerCycle, settings),
            EstimatePhase(phaseB, samplesPerCycle, settings),
            EstimatePhase(phaseC, samplesPerCycle, settings));

    private static void ValidateSampling(
        IReadOnlyList<double> samples,
        int samplesPerCycle,
        TransformerCtSaturationEstimatorSettings settings)
    {
        if (samplesPerCycle < settings.MinimumSamplesPerCycle)
            throw new ArgumentOutOfRangeException(
                nameof(samplesPerCycle),
                $"At least {settings.MinimumSamplesPerCycle} samples/cycle are required for CT saturation evidence.");

        var required = checked(samplesPerCycle * settings.WindowCycles);
        if (samples.Count < required)
            throw new ArgumentException(
                $"At least {required} samples are required for a {settings.WindowCycles}-cycle CT saturation window.",
                nameof(samples));
    }
}
