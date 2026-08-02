using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayAnnunciationLatchTests
{
    [TestMethod]
    public void AgFault_ChangesPhaseAAndEarthFromPickupToLatchedTripUntilReset()
    {
        var engine = new ProtectionEngine(AgFaultSettings(TimeSpan.FromMilliseconds(20)));
        var annunciation = new RelayAnnunciationLatch();
        var start = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

        var pickupSnapshot = engine.Evaluate(AgFaultFrame(start));
        var pickup = annunciation.Observe(pickupSnapshot);

        Assert.IsTrue(pickup.PickupActive);
        Assert.IsFalse(pickup.TripLatched);
        Assert.AreEqual(RelayLampState.Pickup, pickup.PhaseA);
        Assert.AreEqual(RelayLampState.Pickup, pickup.Earth);
        Assert.AreEqual(RelayLampState.Off, pickup.PhaseB);
        Assert.AreEqual(RelayLampState.Off, pickup.PhaseC);

        var tripSnapshot = engine.Evaluate(AgFaultFrame(start.AddMilliseconds(20)));
        var trip = annunciation.Observe(tripSnapshot);

        Assert.IsFalse(trip.PickupActive, "The common pickup lamp must not remain amber once trip is latched.");
        Assert.IsTrue(trip.TripLatched);
        Assert.AreEqual(RelayLampState.Trip, trip.PhaseA);
        Assert.AreEqual(RelayLampState.Trip, trip.Earth);
        Assert.AreEqual(RelayLampState.Off, trip.PhaseB);
        Assert.AreEqual(RelayLampState.Off, trip.PhaseC);

        var clearedFaultSnapshot = engine.Evaluate(NormalFrame(start.AddMilliseconds(25)));
        var latched = annunciation.Observe(clearedFaultSnapshot);

        Assert.IsTrue(latched.TripLatched);
        Assert.AreEqual(RelayLampState.Trip, latched.PhaseA);
        Assert.AreEqual(RelayLampState.Trip, latched.Earth);

        engine.Reset();
        var reset = annunciation.Observe(engine.Evaluate(NormalFrame(start.AddMilliseconds(30))));

        Assert.IsFalse(reset.PickupActive);
        Assert.IsFalse(reset.TripLatched);
        Assert.AreEqual(RelayLampState.Off, reset.PhaseA);
        Assert.AreEqual(RelayLampState.Off, reset.Earth);
    }

    [TestMethod]
    public void Pickup_DropsOutWithoutCreatingAFalseLatch()
    {
        var engine = new ProtectionEngine(AgFaultSettings(TimeSpan.FromMilliseconds(100)));
        var annunciation = new RelayAnnunciationLatch();
        var start = DateTimeOffset.UtcNow;

        var pickup = annunciation.Observe(engine.Evaluate(AgFaultFrame(start)));
        Assert.AreEqual(RelayLampState.Pickup, pickup.PhaseA);
        Assert.AreEqual(RelayLampState.Pickup, pickup.Earth);

        var droppedOut = annunciation.Observe(engine.Evaluate(NormalFrame(start.AddMilliseconds(20))));
        Assert.IsFalse(droppedOut.PickupActive);
        Assert.IsFalse(droppedOut.TripLatched);
        Assert.AreEqual(RelayLampState.Off, droppedOut.PhaseA);
        Assert.AreEqual(RelayLampState.Off, droppedOut.Earth);
    }

    private static ProtectionSettings AgFaultSettings(TimeSpan delay)
        => new()
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 4,
            PhaseInstantaneousDelay = delay,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = true,
            EarthInstantaneousPickupA = 0.8,
            EarthInstantaneousDelay = delay,
            EarthTimeEnabled = false
        };

    private static MeasurementFrame AgFaultFrame(DateTimeOffset timestamp)
        => new(timestamp, 8.4, 1.02, 0.98, 7.34, SmvTrustState.Healthy);

    private static MeasurementFrame NormalFrame(DateTimeOffset timestamp)
        => new(timestamp, 1, 1.02, 0.98, 0.02, SmvTrustState.Healthy);
}
