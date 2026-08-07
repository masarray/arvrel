using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerCtSaturationEstimatorTests
{
    private const int SamplesPerCycle = 80;

    [TestMethod]
    public void PureFundamental_HasNegligibleResidualDistortion()
    {
        var evidence = TransformerCtSaturationEstimator.EstimatePhase(Sine(1.0), SamplesPerCycle);

        Assert.IsTrue(evidence.Reliable);
        Assert.AreEqual(1.0, evidence.FundamentalRms, 1e-9);
        Assert.IsTrue(evidence.DistortionRatio < 1e-10);
        Assert.IsTrue(evidence.PeakAsymmetry < 1e-10);
    }

    [TestMethod]
    public void OneSidedClipping_ProducesStrongDistortionAndAsymmetry()
    {
        var samples = Sine(1.0);
        var positiveLimit = 0.45;
        for (var index = 0; index < samples.Length; index++)
            samples[index] = Math.Min(samples[index], positiveLimit);

        var evidence = TransformerCtSaturationEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.IsTrue(evidence.Reliable);
        Assert.IsTrue(evidence.DistortionRatio > 0.25, evidence.Reason);
        Assert.IsTrue(evidence.PeakAsymmetry > 0.30, evidence.Reason);
    }

    [TestMethod]
    public void SymmetricClipping_ProducesSevereDistortionEvenWithLowAsymmetry()
    {
        var samples = Sine(1.0);
        const double limit = 0.45;
        for (var index = 0; index < samples.Length; index++)
            samples[index] = Math.Clamp(samples[index], -limit, limit);

        var evidence = TransformerCtSaturationEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.IsTrue(evidence.DistortionRatio > 0.30, evidence.Reason);
        Assert.IsTrue(evidence.PeakAsymmetry < 1e-10, evidence.Reason);
    }

    [TestMethod]
    public void RelativeEvidence_IsInvariantToAmplitudeScaling()
    {
        var first = Sine(1.0);
        var second = Sine(3.5);
        for (var index = 0; index < first.Length; index++)
        {
            first[index] = Math.Min(first[index], 0.45);
            second[index] = Math.Min(second[index], 0.45 * 3.5);
        }

        var firstEvidence = TransformerCtSaturationEstimator.EstimatePhase(first, SamplesPerCycle);
        var secondEvidence = TransformerCtSaturationEstimator.EstimatePhase(second, SamplesPerCycle);

        Assert.AreEqual(firstEvidence.DistortionRatio, secondEvidence.DistortionRatio, 1e-12);
        Assert.AreEqual(firstEvidence.PeakAsymmetry, secondEvidence.PeakAsymmetry, 1e-12);
    }

    [TestMethod]
    public void LatestCompleteWindow_IsUsed()
    {
        var clean = Sine(1.0);
        var clipped = Sine(1.0);
        for (var index = 0; index < clipped.Length; index++)
            clipped[index] = Math.Min(clipped[index], 0.45);
        var samples = clean.Concat(clipped).ToArray();

        var evidence = TransformerCtSaturationEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.IsTrue(evidence.DistortionRatio > 0.25);
        Assert.IsTrue(evidence.PeakAsymmetry > 0.30);
    }

    [TestMethod]
    public void LowFundamental_IsMarkedUnreliableInsteadOfCreatingLargeRatios()
    {
        var samples = new double[SamplesPerCycle];

        var evidence = TransformerCtSaturationEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.IsFalse(evidence.Reliable);
        Assert.AreEqual(0, evidence.DistortionRatio);
    }

    [TestMethod]
    public void NonFiniteWaveform_IsRejected()
    {
        var samples = Sine(1.0);
        samples[7] = double.NaN;

        Assert.ThrowsExactly<ArgumentException>(() =>
            TransformerCtSaturationEstimator.EstimatePhase(samples, SamplesPerCycle));
    }

    private static double[] Sine(double rms)
    {
        var samples = new double[SamplesPerCycle];
        for (var index = 0; index < samples.Length; index++)
            samples[index] = Math.Sqrt(2) * rms * Math.Sin(2 * Math.PI * index / SamplesPerCycle);
        return samples;
    }
}
