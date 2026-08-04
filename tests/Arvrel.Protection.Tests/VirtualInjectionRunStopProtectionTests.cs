using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualInjectionRunStopProtectionTests
{
    [TestMethod]
    public void ExactPickupCurrent_TripsAfterConfiguredDelayOnlyWhileStarted()
    {
        const double pickupA = 4.0;
        var profile = VirtualInjectionPresets.Create("A-G fault") with
        {
            PhaseACurrent = new VirtualInjectionChannel(true, pickupA, 45)
        };
        var runtime = new VirtualInjectionRuntime(
            profile,
            initialTimestamp: DateTimeOffset.UnixEpoch);
        var engine = new ProtectionEngine(new ProtectionSettings
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = pickupA,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(20),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        });

        var stopped = engine.Evaluate(runtime.Advance(TimeSpan.Zero, false).Frame.Measurement);
        Assert.AreEqual(0, stopped.Phase50.OperatingQuantity, 0.0001);
        Assert.IsFalse(stopped.Phase50.Pickup);
        Assert.IsFalse(stopped.TripLatched);

        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(20), false);

        ProtectionSnapshot beforeDelay = stopped;
        for (var elapsed = 0; elapsed < 20; elapsed += 5)
            beforeDelay = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(5), false).Frame.Measurement);

        Assert.AreEqual(pickupA, beforeDelay.Phase50.OperatingQuantity, 0.002);
        Assert.IsTrue(beforeDelay.Phase50.Pickup);
        Assert.IsFalse(beforeDelay.Phase50.Operated);
        Assert.IsFalse(beforeDelay.TripLatched);

        var atDelay = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(5), false).Frame.Measurement);
        Assert.IsTrue(atDelay.Phase50.Operated);
        Assert.IsTrue(atDelay.TripLatched);
        Assert.AreEqual("50P-1", atDelay.LatchedOperation?.Element);

        runtime.Stop();
        var stoppedAgain = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(5), false).Frame.Measurement);
        Assert.AreEqual(0, stoppedAgain.Phase50.OperatingQuantity, 0.0001);
        Assert.IsFalse(stoppedAgain.Phase50.Pickup);
        Assert.IsTrue(stoppedAgain.TripLatched);
    }
}
