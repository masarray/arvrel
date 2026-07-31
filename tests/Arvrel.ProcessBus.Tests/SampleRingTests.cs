using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class SampleRingTests
{
    [TestMethod]
    public void CompletedScopeWindowDoesNotSlideWhileNextWindowIsFilling()
    {
        var ring = new SampleRing(512);
        for (var sample = 0; sample < 160; sample++)
            ring.Add(sample);

        var firstWindow = ring.Last(160);

        for (var sample = 160; sample < 200; sample++)
            ring.Add(sample);

        var partialNextWindow = ring.Last(160);
        CollectionAssert.AreEqual(firstWindow, partialNextWindow);

        for (var sample = 200; sample < 320; sample++)
            ring.Add(sample);

        var nextCompleteWindow = ring.Last(160);
        Assert.AreEqual(160d, nextCompleteWindow[0]);
        Assert.AreEqual(319d, nextCompleteWindow[^1]);
    }

    [TestMethod]
    public void RmsCalculationStillUsesNewestSamplesWhileDisplayWindowIsLocked()
    {
        var ring = new SampleRing(512);
        for (var sample = 0; sample < 160; sample++)
            ring.Add(0);
        for (var sample = 0; sample < 40; sample++)
            ring.Add(3);

        Assert.IsTrue(ring.Last(160).All(value => value == 0));
        Assert.AreEqual(3d, ring.RmsLast(40), 0.000001);
    }
}
