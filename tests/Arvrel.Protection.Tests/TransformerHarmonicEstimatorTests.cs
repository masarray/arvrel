using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerHarmonicEstimatorTests
{
    private const int SamplesPerCycle = 80;

    [TestMethod]
    public void PureFundamental_ReportsNegligibleH2AndH5()
    {
        var samples = BuildWaveform(2, fundamentalRms: 1.0);

        var estimate = TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.IsTrue(estimate.RatioReliable);
        Assert.AreEqual(1.0, estimate.FundamentalRms, 1e-9);
        Assert.AreEqual(0.0, estimate.SecondRatio, 1e-9);
        Assert.AreEqual(0.0, estimate.FifthRatio, 1e-9);
    }

    [TestMethod]
    public void TwentyPercentSecondHarmonic_IsRecoveredAsTwentyPercent()
    {
        var samples = BuildWaveform(2, fundamentalRms: 1.0, secondRms: 0.20);

        var estimate = TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.AreEqual(0.20, estimate.SecondRatio, 1e-9);
        Assert.AreEqual(0.0, estimate.FifthRatio, 1e-9);
    }

    [TestMethod]
    public void ThirtyFivePercentFifthHarmonic_IsRecoveredAsThirtyFivePercent()
    {
        var samples = BuildWaveform(2, fundamentalRms: 2.0, fifthRms: 0.70);

        var estimate = TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.AreEqual(0.35, estimate.FifthRatio, 1e-9);
        Assert.AreEqual(0.0, estimate.SecondRatio, 1e-9);
    }

    [TestMethod]
    public void MixedH2AndH5_AreIndependentlyRecovered()
    {
        var samples = BuildWaveform(
            2,
            fundamentalRms: 1.5,
            secondRms: 0.30,
            fifthRms: 0.525,
            fundamentalDegrees: 37,
            secondDegrees: -81,
            fifthDegrees: 143);

        var estimate = TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.AreEqual(0.20, estimate.SecondRatio, 1e-9);
        Assert.AreEqual(0.35, estimate.FifthRatio, 1e-9);
    }

    [TestMethod]
    public void HarmonicRatios_AreInvariantToSignalAmplitudeAndPhase()
    {
        var first = TransformerHarmonicEstimator.EstimatePhase(
            BuildWaveform(1, 1.0, 0.18, 0.31, fundamentalDegrees: 10, secondDegrees: 20, fifthDegrees: 30),
            SamplesPerCycle);
        var second = TransformerHarmonicEstimator.EstimatePhase(
            BuildWaveform(1, 12.0, 2.16, 3.72, fundamentalDegrees: -123, secondDegrees: 71, fifthDegrees: -17),
            SamplesPerCycle);

        Assert.AreEqual(first.SecondRatio, second.SecondRatio, 1e-9);
        Assert.AreEqual(first.FifthRatio, second.FifthRatio, 1e-9);
    }

    [TestMethod]
    public void DcOffset_IsRejectedByWindowMeanRemoval()
    {
        var samples = BuildWaveform(1, 1.0, secondRms: 0.20, fifthRms: 0.35, dcOffset: 8.5);

        var estimate = TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.AreEqual(1.0, estimate.FundamentalRms, 1e-9);
        Assert.AreEqual(0.20, estimate.SecondRatio, 1e-9);
        Assert.AreEqual(0.35, estimate.FifthRatio, 1e-9);
    }

    [TestMethod]
    public void TwoCycleWindow_ProducesSameStationaryRatios()
    {
        var settings = new TransformerHarmonicEstimatorSettings { WindowCycles = 2 };
        var samples = BuildWaveform(3, 1.0, secondRms: 0.16, fifthRms: 0.28);

        var estimate = TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle, settings);

        Assert.AreEqual(0.16, estimate.SecondRatio, 1e-9);
        Assert.AreEqual(0.28, estimate.FifthRatio, 1e-9);
    }

    [TestMethod]
    public void LastCompleteWindow_IsAuthoritative()
    {
        var samples = BuildWaveform(2, 1.0, secondRms: 0.10, fifthRms: 0.10);
        var lastCycle = BuildWaveform(1, 1.0, secondRms: 0.25, fifthRms: 0.40);
        Array.Copy(lastCycle, 0, samples, SamplesPerCycle, SamplesPerCycle);

        var estimate = TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle);

        Assert.AreEqual(0.25, estimate.SecondRatio, 1e-9);
        Assert.AreEqual(0.40, estimate.FifthRatio, 1e-9);
    }

    [TestMethod]
    public void FundamentalBelowFloor_DoesNotPublishExplodingRatios()
    {
        var settings = new TransformerHarmonicEstimatorSettings { MinimumFundamentalRms = 0.05 };
        var samples = BuildWaveform(1, fundamentalRms: 0.01, secondRms: 0.20, fifthRms: 0.30);

        var estimate = TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle, settings);

        Assert.IsFalse(estimate.RatioReliable);
        Assert.AreEqual(0.0, estimate.SecondRatio);
        Assert.AreEqual(0.0, estimate.FifthRatio);
        StringAssert.Contains(estimate.Reason, "below the ratio floor");
    }

    [TestMethod]
    public void ThreePhaseEstimator_ProducesPerPhaseRatios()
    {
        var a = BuildWaveform(1, 1.0, secondRms: 0.15, fifthRms: 0.05);
        var b = BuildWaveform(1, 1.0, secondRms: 0.25, fifthRms: 0.10, fundamentalDegrees: -120);
        var c = BuildWaveform(1, 1.0, secondRms: 0.35, fifthRms: 0.20, fundamentalDegrees: 120);

        var estimate = TransformerHarmonicEstimator.EstimateThreePhase(a, b, c, SamplesPerCycle);

        Assert.AreEqual(0.15, estimate.SecondRatios.PhaseA, 1e-9);
        Assert.AreEqual(0.25, estimate.SecondRatios.PhaseB, 1e-9);
        Assert.AreEqual(0.35, estimate.SecondRatios.PhaseC, 1e-9);
        Assert.AreEqual(0.20, estimate.FifthRatios.PhaseC, 1e-9);
        Assert.IsTrue(estimate.AllRatiosReliable);
    }

    [TestMethod]
    public void InsufficientWindow_IsRejected()
    {
        var samples = new double[SamplesPerCycle - 1];

        Assert.ThrowsExactly<ArgumentException>(() =>
            TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle));
    }

    [TestMethod]
    public void H5RequiresAdequateSampleRate()
    {
        var samples = new double[10];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TransformerHarmonicEstimator.EstimatePhase(samples, 10));
    }

    [TestMethod]
    public void NonFiniteSample_IsRejected()
    {
        var samples = BuildWaveform(1, 1.0);
        samples[17] = double.NaN;

        Assert.ThrowsExactly<ArgumentException>(() =>
            TransformerHarmonicEstimator.EstimatePhase(samples, SamplesPerCycle));
    }

    private static double[] BuildWaveform(
        int cycles,
        double fundamentalRms,
        double secondRms = 0,
        double fifthRms = 0,
        double fundamentalDegrees = 0,
        double secondDegrees = 0,
        double fifthDegrees = 0,
        double dcOffset = 0)
    {
        var count = cycles * SamplesPerCycle;
        var result = new double[count];
        var fundamentalPhase = Degrees(fundamentalDegrees);
        var secondPhase = Degrees(secondDegrees);
        var fifthPhase = Degrees(fifthDegrees);

        for (var index = 0; index < count; index++)
        {
            var fundamentalAngle = 2 * Math.PI * index / SamplesPerCycle;
            result[index] = dcOffset
                + RmsSine(fundamentalRms, fundamentalAngle + fundamentalPhase)
                + RmsSine(secondRms, 2 * fundamentalAngle + secondPhase)
                + RmsSine(fifthRms, 5 * fundamentalAngle + fifthPhase);
        }

        return result;
    }

    private static double RmsSine(double rms, double angle)
        => rms * Math.Sqrt(2) * Math.Sin(angle);

    private static double Degrees(double value) => value * Math.PI / 180;
}
