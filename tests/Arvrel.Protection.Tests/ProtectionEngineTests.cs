using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class ProtectionEngineTests
{
    [TestMethod]
    public void NormalLoad_RemainsSecure()
    {
        var engine = new ProtectionEngine(new ProtectionSettings());
        var snapshot = Run(engine, 1, 1, 1, 0.02, SmvTrustState.Healthy, 500);
        Assert.IsFalse(snapshot.TripLatched);
        Assert.IsFalse(snapshot.Phase50.Pickup);
        Assert.IsFalse(snapshot.Earth50.Pickup);
    }

    [TestMethod]
    public void PhaseInstantaneous_TripsAfterDefiniteDelay()
    {
        var engine = new ProtectionEngine(new ProtectionSettings());
        var snapshot = Run(engine, 8, 1, 1, 7, SmvTrustState.Healthy, 100);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.IsTrue(snapshot.Phase50.Operated);
        Assert.IsTrue(snapshot.PhaseAPickup);
    }

    [TestMethod]
    public void SmvTrustGate_BlocksOperationWithoutHidingPickup()
    {
        var engine = new ProtectionEngine(new ProtectionSettings());
        var trust = SmvTrustState.TripBlocked("SMPCNT_GAP", "Repeated gap");
        var snapshot = Run(engine, 8, 1, 1, 7, trust, 150);
        Assert.IsTrue(snapshot.Phase50.Pickup);
        Assert.IsTrue(snapshot.Blocked);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void EarthInstantaneous_OperatesFromResidualCurrent()
    {
        var engine = new ProtectionEngine(new ProtectionSettings());
        var snapshot = Run(engine, 1, 1, 1, 1.4, SmvTrustState.Healthy, 160);
        Assert.IsTrue(snapshot.Earth50.Operated);
        Assert.IsTrue(snapshot.EarthPickup);
        Assert.IsTrue(snapshot.TripLatched);
    }

    [TestMethod]
    public void Reset_ClearsTripLatchAndDynamicState()
    {
        var engine = new ProtectionEngine(new ProtectionSettings());
        Assert.IsTrue(Run(engine, 8, 1, 1, 7, SmvTrustState.Healthy, 120).TripLatched);
        engine.Reset();
        var snapshot = Run(engine, 1, 1, 1, 0.02, SmvTrustState.Healthy, 20);
        Assert.IsFalse(snapshot.TripLatched);
        Assert.AreEqual(0, snapshot.Phase50.Progress, 0.0001);
    }

    private static ProtectionSnapshot Run(
        ProtectionEngine engine,
        double a,
        double b,
        double c,
        double residual,
        SmvTrustState trust,
        int durationMs)
    {
        var timestamp = DateTimeOffset.UtcNow;
        ProtectionSnapshot snapshot = ProtectionSnapshot.Ready(timestamp);
        for (var elapsed = 0; elapsed <= durationMs; elapsed += 5)
        {
            snapshot = engine.Evaluate(new MeasurementFrame(timestamp.AddMilliseconds(elapsed), a, b, c, residual, trust));
        }
        return snapshot;
    }
}
