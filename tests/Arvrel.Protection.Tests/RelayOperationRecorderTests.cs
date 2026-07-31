using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayOperationRecorderTests
{
    [TestMethod]
    public void Recorder_CapturesPickupTripAndOperateTimeFromSnapshotTimestamps()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousPickupA = 2,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(20),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        };
        var engine = new ProtectionEngine(settings);
        var recorder = new RelayOperationRecorder();
        var start = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);

        var firstFrame = new MeasurementFrame(start, 4, 1, 1, 0, SmvTrustState.Healthy);
        var pickup = recorder.Observe(engine.Evaluate(firstFrame), firstFrame);
        Assert.IsTrue(pickup.PickupStarted);
        Assert.IsFalse(pickup.TripOccurred);
        Assert.AreEqual(start, pickup.Operation?.PickupTimestamp);

        RelayOperationTransition transition = pickup;
        for (var elapsed = 5; elapsed <= 20; elapsed += 5)
        {
            var frame = new MeasurementFrame(start.AddMilliseconds(elapsed), 4, 1, 1, 0, SmvTrustState.Healthy);
            transition = recorder.Observe(engine.Evaluate(frame), frame);
        }

        Assert.IsTrue(transition.TripOccurred);
        Assert.IsNotNull(transition.Operation);
        Assert.IsNotNull(transition.Operation.OperateTime);
        Assert.AreEqual("50P-1", transition.Operation.TripElement);
        Assert.AreEqual(20, transition.Operation.OperateTime.Value.TotalMilliseconds, 0.001);
        Assert.AreEqual(4, transition.Operation.TripCurrentA, 0.001);
    }

    [TestMethod]
    public void Recorder_ExposesBlockedOperationWithoutCreatingTripTimestamp()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousPickupA = 2,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(10),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        };
        var engine = new ProtectionEngine(settings);
        var recorder = new RelayOperationRecorder();
        var start = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);
        var trust = SmvTrustState.TripBlocked("SMPCNT_GAP", "Repeated sample-counter gap");

        RelayOperationTransition transition = new(false, false, false, false, null);
        for (var elapsed = 0; elapsed <= 15; elapsed += 5)
        {
            var frame = new MeasurementFrame(start.AddMilliseconds(elapsed), 4, 1, 1, 0, trust);
            transition = recorder.Observe(engine.Evaluate(frame), frame);
        }

        Assert.IsTrue(transition.BlockedStarted);
        Assert.IsFalse(transition.TripOccurred);
        Assert.IsNotNull(recorder.Current);
        Assert.IsNull(recorder.Current.TripTimestamp);
    }
}
