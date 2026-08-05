using Arvrel.Application.Laboratory;
using Arvrel.Desktop.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class WaveformAxisLayoutTests
{
    [TestMethod]
    public void FiftyHertzTwoCycleWindowProducesQuarterCycleTicks()
    {
        var axis = WaveformAxisLayout.Create(CreateWaveform(
            sampleCount: 160,
            frequencyHz: 50,
            samplesPerCycle: 80,
            sampleRateHz: 4000));

        Assert.AreEqual(40, axis.DurationMilliseconds, 1e-9);
        Assert.AreEqual(20, axis.CycleMilliseconds, 1e-9);
        Assert.AreEqual(9, axis.Ticks.Count);

        CollectionAssert.AreEqual(
            new[] { "0 ms", "10 ms", "20 ms", "30 ms", "40 ms" },
            axis.Ticks.Where(tick => tick.IsMajor).Select(tick => tick.Label).ToArray());

        var cycleTicks = axis.Ticks.Where(tick => tick.IsCycleBoundary).ToArray();
        Assert.AreEqual(3, cycleTicks.Length);
        Assert.AreEqual(0, cycleTicks[0].NormalizedPosition, 1e-9);
        Assert.AreEqual(0.5, cycleTicks[1].NormalizedPosition, 1e-9);
        Assert.AreEqual(1, cycleTicks[2].NormalizedPosition, 1e-9);
    }

    [TestMethod]
    public void SixtyHertzLabelsRemainFrequencyAwareAndCultureInvariant()
    {
        var axis = WaveformAxisLayout.Create(CreateWaveform(
            sampleCount: 192,
            frequencyHz: 60,
            samplesPerCycle: 96,
            sampleRateHz: 5760));

        Assert.AreEqual(1000d / 30d, axis.DurationMilliseconds, 1e-9);
        Assert.AreEqual(1000d / 60d, axis.CycleMilliseconds, 1e-9);

        CollectionAssert.AreEqual(
            new[] { "0 ms", "8.33 ms", "16.67 ms", "25 ms", "33.33 ms" },
            axis.Ticks.Where(tick => tick.IsMajor).Select(tick => tick.Label).ToArray());
    }

    [TestMethod]
    public void ViewportReservesAStableAxisBand()
    {
        var viewport = WaveformAxisLayout.CreateViewport(600, 330);

        Assert.IsTrue(viewport.IsRenderable);
        Assert.AreEqual(10, viewport.Left, 1e-9);
        Assert.AreEqual(6, viewport.Top, 1e-9);
        Assert.AreEqual(580, viewport.Width, 1e-9);
        Assert.AreEqual(296, viewport.Height, 1e-9);
        Assert.AreEqual(302, viewport.AxisY, 1e-9);
        Assert.AreEqual(viewport.Bottom, viewport.AxisY, 1e-9);
    }

    [TestMethod]
    public void EmptyWaveformDoesNotInventTimingTicks()
    {
        var axis = WaveformAxisLayout.Create(CreateWaveform(
            sampleCount: 0,
            frequencyHz: 50,
            samplesPerCycle: 80,
            sampleRateHz: 4000));

        Assert.AreEqual(0, axis.Ticks.Count);
        Assert.AreEqual(0, axis.DurationMilliseconds, 1e-9);
    }

    private static ScenarioWaveform CreateWaveform(
        int sampleCount,
        double frequencyHz,
        int samplesPerCycle,
        double sampleRateHz)
    {
        var samples = new double[sampleCount];
        return new ScenarioWaveform(
            samples,
            (double[])samples.Clone(),
            (double[])samples.Clone(),
            (double[])samples.Clone(),
            frequencyHz,
            samplesPerCycle,
            sampleRateHz);
    }
}
