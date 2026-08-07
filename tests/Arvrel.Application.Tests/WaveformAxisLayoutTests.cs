using Arvrel.Application.Laboratory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

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
        Assert.AreEqual(39.75, axis.LastSampleTimestampMilliseconds, 1e-9);
        Assert.AreEqual(20, axis.SignalCycleMilliseconds, 1e-9);
        Assert.AreEqual(20, axis.NominalCycleMilliseconds, 1e-9);
        Assert.IsFalse(axis.UsesOffNominalCycleGrid);
        Assert.AreEqual(WaveformTimeOrigin.DisplayedWindowStart, axis.TimeOrigin);
        Assert.AreEqual(9, axis.Ticks.Count);

        CollectionAssert.AreEqual(
            new[] { "0 ms", "10 ms", "20 ms", "30 ms", "40 ms" },
            axis.Ticks.Where(tick => tick.IsMajor).Select(tick => tick.Label).ToArray());

        var cycleTicks = axis.Ticks.Where(tick => tick.IsCycleBoundary).ToArray();
        Assert.AreEqual(3, cycleTicks.Length);
        Assert.AreEqual(0, cycleTicks[0].NormalizedPosition, 1e-9);
        Assert.AreEqual(0.5, cycleTicks[1].NormalizedPosition, 1e-9);
        Assert.AreEqual(1, cycleTicks[2].NormalizedPosition, 1e-9);
        Assert.IsTrue(axis.Ticks[^1].IsWindowBoundary);
    }

    [TestMethod]
    public void SixtyHertzOnFixedFourKilohertzGridUsesSignalCycleMarkers()
    {
        var axis = WaveformAxisLayout.Create(CreateWaveform(
            sampleCount: 160,
            frequencyHz: 60,
            samplesPerCycle: 80,
            sampleRateHz: 4000));

        Assert.AreEqual(40, axis.DurationMilliseconds, 1e-9);
        Assert.AreEqual(1000d / 60d, axis.SignalCycleMilliseconds, 1e-9);
        Assert.AreEqual(20, axis.NominalCycleMilliseconds, 1e-9);
        Assert.IsTrue(axis.UsesOffNominalCycleGrid);

        CollectionAssert.AreEqual(
            new[] { "0 ms", "8.33 ms", "16.67 ms", "25 ms", "33.33 ms", "40 ms" },
            axis.Ticks.Where(tick => tick.IsMajor).Select(tick => tick.Label).ToArray());

        var cycleTicks = axis.Ticks.Where(tick => tick.IsCycleBoundary).ToArray();
        Assert.AreEqual(3, cycleTicks.Length);
        Assert.AreEqual(0, cycleTicks[0].TimeMilliseconds, 1e-9);
        Assert.AreEqual(1000d / 60d, cycleTicks[1].TimeMilliseconds, 1e-9);
        Assert.AreEqual(2000d / 60d, cycleTicks[2].TimeMilliseconds, 1e-9);
        Assert.IsTrue(axis.Ticks[^1].IsWindowBoundary);
        Assert.IsFalse(axis.Ticks[^1].IsCycleBoundary);
    }

    [TestMethod]
    public void SampleProjectionUsesTrueSamplingTimestamps()
    {
        Assert.AreEqual(0, WaveformAxisLayout.NormalizeSamplePosition(0, 160), 1e-9);
        Assert.AreEqual(0.5, WaveformAxisLayout.NormalizeSamplePosition(80, 160), 1e-9);
        Assert.AreEqual(159d / 160d, WaveformAxisLayout.NormalizeSamplePosition(159, 160), 1e-9);
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => WaveformAxisLayout.NormalizeSamplePosition(160, 160));
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

    [TestMethod]
    public void InvalidTimingMetadataIsRejected()
    {
        var samples = new double[160];
        var invalid = new ScenarioWaveform(
            samples,
            (double[])samples.Clone(),
            (double[])samples.Clone(),
            (double[])samples.Clone(),
            60,
            80,
            0);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => WaveformAxisLayout.Create(invalid));
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
