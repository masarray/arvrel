using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayOperationRecorderTests
{
    [TestMethod]
    public void Recorder_CapturesPickupTripAndOperateTimeFromSnapshotTimestamps()
    {
        var settings = InstantaneousOnlySettings(TimeSpan.FromMilliseconds(20));
        var engine = new ProtectionEngine(settings);
        var recorder = new RelayOperationRecorder();
        var start = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);

        var firstFrame = Frame(start, SmvTrustState.Healthy);
        var pickup = recorder.Observe(engine.Evaluate(firstFrame), firstFrame);
        Assert.IsTrue(pickup.PickupStarted);
        Assert.IsFalse(pickup.TripOccurred);
        Assert.AreEqual(start, pickup.Operation?.PickupTimestamp);

        RelayOperationTransition transition = pickup;
        for (var elapsed = 5; elapsed <= 20; elapsed += 5)
        {
            var frame = Frame(start.AddMilliseconds(elapsed), SmvTrustState.Healthy);
            transition = recorder.Observe(engine.Evaluate(frame), frame);
        }

        Assert.IsTrue(transition.TripOccurred);
        Assert.IsNotNull(transition.Operation);
        Assert.IsNotNull(transition.Operation.OperateTime);
        Assert.AreEqual("50P-1", transition.Operation.TripElement);
        Assert.AreEqual(20, transition.Operation.OperateTime.Value.TotalMilliseconds, 0.001);
        Assert.AreEqual(4, transition.Operation.TripCurrentA, 0.001);

        engine.Reset();
        var resetFrame = new MeasurementFrame(start.AddMilliseconds(25), 1, 1, 1, 0, SmvTrustState.Healthy);
        var reset = recorder.Observe(engine.Evaluate(resetFrame), resetFrame);
        Assert.IsTrue(reset.ResetObserved);
        Assert.IsNotNull(recorder.Current);
        Assert.IsNotNull(recorder.Current.TripTimestamp);
    }

    [TestMethod]
    public void Recorder_ExposesBlockedOperationWithoutCreatingTripTimestamp()
    {
        var engine = new ProtectionEngine(InstantaneousOnlySettings(TimeSpan.FromMilliseconds(10)));
        var recorder = new RelayOperationRecorder();
        var start = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);
        var trust = SmvTrustState.TripBlocked("SMPCNT_GAP", "Repeated sample-counter gap");
        var blockedSeen = false;
        RelayOperationTransition transition = new(false, false, false, false, null);

        for (var elapsed = 0; elapsed <= 15; elapsed += 5)
        {
            var frame = Frame(start.AddMilliseconds(elapsed), trust);
            transition = recorder.Observe(engine.Evaluate(frame), frame);
            blockedSeen |= transition.BlockedStarted;
        }

        Assert.IsTrue(blockedSeen);
        Assert.IsFalse(transition.TripOccurred);
        Assert.IsNotNull(recorder.Current);
        Assert.IsNull(recorder.Current.TripTimestamp);
    }

    private static ProtectionSettings InstantaneousOnlySettings(TimeSpan delay)
        => new()
        {
            PhaseInstantaneousPickupA = 2,
            PhaseInstantaneousDelay = delay,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        };

    private static MeasurementFrame Frame(DateTimeOffset timestamp, SmvTrustState trust)
        => new(timestamp, 4, 1, 1, 0, trust);
}
