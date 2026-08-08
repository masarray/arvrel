using System.Numerics;

namespace Arvrel.Protection;

/// <summary>
/// Settings for the vendor-neutral transformer current harmonic estimator.
/// The estimator uses a coherent orthogonal DFT over an integer number of cycles.
/// H2/H5 ratios are reported as RMS harmonic magnitude divided by RMS fundamental magnitude.
/// </summary>
public sealed record TransformerHarmonicEstimatorSettings
{
    public int WindowCycles { get; init; } = 1;
    public int MinimumSamplesPerCycle { get; init; } = 16;
    public double MinimumFundamentalRms { get; init; } = 1e-6;

    public void Validate()
    {
        if (WindowCycles is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(WindowCycles));
        if (MinimumSamplesPerCycle is < 12 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(MinimumSamplesPerCycle));
        if (!double.IsFinite(MinimumFundamentalRms) || MinimumFundamentalRms <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumFundamentalRms));
    }
}

public sealed record TransformerPhaseHarmonicEstimate(
    Complex Fundamental,
    Complex Second,
    Complex Fifth,
    double FundamentalRms,
    double SecondRms,
    double FifthRms,
    double SecondRatio,
    double FifthRatio,
    bool RatioReliable,
    string Reason);

public sealed record TransformerThreePhaseHarmonicEstimate(
    TransformerPhaseHarmonicEstimate PhaseA,
    TransformerPhaseHarmonicEstimate PhaseB,
    TransformerPhaseHarmonicEstimate PhaseC)
{
    public TransformerHarmonicRatios SecondRatios => new(
        PhaseA.SecondRatio,
        PhaseB.SecondRatio,
        PhaseC.SecondRatio);

    public TransformerHarmonicRatios FifthRatios => new(
        PhaseA.FifthRatio,
        PhaseB.FifthRatio,
        PhaseC.FifthRatio);

    public bool AllRatiosReliable =>
        PhaseA.RatioReliable &&
        PhaseB.RatioReliable &&
        PhaseC.RatioReliable;
}

/// <summary>
/// Coherent DFT estimator for transformer differential harmonic security inputs.
/// It deliberately estimates waveform content only; blocking/restraint decisions remain
/// inside TransformerProtectionEngine.
/// </summary>
public static class TransformerHarmonicEstimator
{
    public static TransformerPhaseHarmonicEstimate EstimatePhase(
        IReadOnlyList<double> samples,
        int samplesPerCycle,
        TransformerHarmonicEstimatorSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var policy = settings ?? new TransformerHarmonicEstimatorSettings();
        policy.Validate();
        ValidateSampling(samples, samplesPerCycle, policy);

        var windowSamples = checked(samplesPerCycle * policy.WindowCycles);
        var start = samples.Count - windowSamples;
        var mean = 0.0;
        for (var index = start; index < samples.Count; index++)
        {
            var value = samples[index];
            if (!double.IsFinite(value))
                throw new ArgumentException("Harmonic estimation requires finite waveform samples.", nameof(samples));
            mean += value;
        }
        mean /= windowSamples;

        var fundamental = EstimateOrder(samples, start, windowSamples, samplesPerCycle, 1, mean);
        var second = EstimateOrder(samples, start, windowSamples, samplesPerCycle, 2, mean);
        var fifth = EstimateOrder(samples, start, windowSamples, samplesPerCycle, 5, mean);
        var fundamentalRms = fundamental.Magnitude;
        var secondRms = second.Magnitude;
        var fifthRms = fifth.Magnitude;

        if (fundamentalRms < policy.MinimumFundamentalRms)
        {
            return new TransformerPhaseHarmonicEstimate(
                fundamental,
                second,
                fifth,
                fundamentalRms,
                secondRms,
                fifthRms,
                0,
                0,
                false,
                $"Fundamental RMS {fundamentalRms:0.######} is below the ratio floor {policy.MinimumFundamentalRms:0.######}.");
        }

        return new TransformerPhaseHarmonicEstimate(
            fundamental,
            second,
            fifth,
            fundamentalRms,
            secondRms,
            fifthRms,
            secondRms / fundamentalRms,
            fifthRms / fundamentalRms,
            true,
            "H1/H2/H5 coherent DFT estimate ready.");
    }

    public static TransformerThreePhaseHarmonicEstimate EstimateThreePhase(
        IReadOnlyList<double> phaseA,
        IReadOnlyList<double> phaseB,
        IReadOnlyList<double> phaseC,
        int samplesPerCycle,
        TransformerHarmonicEstimatorSettings? settings = null)
    {
        return new TransformerThreePhaseHarmonicEstimate(
            EstimatePhase(phaseA, samplesPerCycle, settings),
            EstimatePhase(phaseB, samplesPerCycle, settings),
            EstimatePhase(phaseC, samplesPerCycle, settings));
    }

    private static void ValidateSampling(
        IReadOnlyList<double> samples,
        int samplesPerCycle,
        TransformerHarmonicEstimatorSettings settings)
    {
        if (samplesPerCycle < settings.MinimumSamplesPerCycle)
            throw new ArgumentOutOfRangeException(
                nameof(samplesPerCycle),
                $"At least {settings.MinimumSamplesPerCycle} samples/cycle are required for H5 estimation.");
        if (samplesPerCycle <= 10)
            throw new ArgumentOutOfRangeException(nameof(samplesPerCycle), "H5 must remain below Nyquist.");

        var required = checked(samplesPerCycle * settings.WindowCycles);
        if (samples.Count < required)
            throw new ArgumentException(
                $"At least {required} samples are required for a {settings.WindowCycles}-cycle harmonic window.",
                nameof(samples));
    }

    private static Complex EstimateOrder(
        IReadOnlyList<double> samples,
        int start,
        int count,
        int samplesPerCycle,
        int harmonicOrder,
        double mean)
    {
        var real = 0.0;
        var imaginary = 0.0;
        for (var offset = 0; offset < count; offset++)
        {
            var value = samples[start + offset] - mean;
            var angle = -2 * Math.PI * harmonicOrder * offset / samplesPerCycle;
            real += value * Math.Cos(angle);
            imaginary += value * Math.Sin(angle);
        }

        // Peak sinusoid coefficient -> RMS phasor magnitude.
        var scale = Math.Sqrt(2) / count;
        return new Complex(real * scale, imaginary * scale);
    }
}
