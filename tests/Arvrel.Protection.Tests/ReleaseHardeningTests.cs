using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class ReleaseHardeningTests
{
    [TestMethod]
    public void RejectedFrameTimestamps_DoNotAdvanceDefiniteTimeAccumulator()
    {
        var engine = new ProtectionEngine(new ProtectionSettings
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 1,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(100),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        });
        var start = new DateTimeOffset(2026, 8, 3, 15, 0, 0, TimeSpan.Zero);

        var pickup = engine.Evaluate(new MeasurementFrame(start, 2, 0, 0, 0, SmvTrustState.Healthy));
        Assert.IsTrue(pickup.Phase50.Pickup);
        Assert.IsFalse(pickup.Phase50.Operated);

        for (var elapsed = 10; elapsed <= 200; elapsed += 10)
            engine.ObserveRejectedFrameTimestamp(start.AddMilliseconds(elapsed));

        var firstAcceptedAfterRejects = engine.Evaluate(new MeasurementFrame(
            start.AddMilliseconds(205),
            2,
            0,
            0,
            0,
            SmvTrustState.Healthy));

        Assert.IsFalse(firstAcceptedAfterRejects.Phase50.Operated);
        Assert.IsFalse(firstAcceptedAfterRejects.TripLatched);
        Assert.IsTrue(firstAcceptedAfterRejects.Phase50.Progress < 0.1,
            $"Rejected frame time advanced the timer to {firstAcceptedAfterRejects.Phase50.Progress:P1}.");
    }

    [TestMethod]
    public void SimultaneousEarth50AndPhase51Operation_PreservesEarth50Precedence()
    {
        var engine = new ProtectionEngine(new ProtectionSettings
        {
            PhaseInstantaneousEnabled = false,
            PhaseTimeEnabled = true,
            PhaseTimePickupA = 1,
            PhaseTimeCurve = IecCurveFamily.DefiniteTime,
            PhaseTimeDefiniteDelay = TimeSpan.FromMilliseconds(10),
            PhaseTimeMinimumOperateTime = TimeSpan.Zero,
            EarthInstantaneousEnabled = true,
            EarthInstantaneousPickupA = 1,
            EarthInstantaneousDelay = TimeSpan.FromMilliseconds(10),
            EarthTimeEnabled = false
        });
        var start = new DateTimeOffset(2026, 8, 3, 16, 0, 0, TimeSpan.Zero);

        engine.Evaluate(new MeasurementFrame(start, 2, 2, 2, 2, SmvTrustState.Healthy));
        var operated = engine.Evaluate(new MeasurementFrame(
            start.AddMilliseconds(10),
            2,
            2,
            2,
            2,
            SmvTrustState.Healthy));

        Assert.IsTrue(operated.Earth50.Operated);
        Assert.IsTrue(operated.Phase51.Operated);
        Assert.IsTrue(operated.TripLatched);
        Assert.AreEqual("50N", operated.ActiveElement);
        Assert.AreEqual("50N", operated.LatchedOperation?.Element);
        Assert.AreEqual("3I0", operated.LatchedOperation?.QuantitySymbol);
    }
}
