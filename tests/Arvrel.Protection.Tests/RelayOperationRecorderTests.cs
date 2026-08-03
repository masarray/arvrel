using System.Numerics;
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
        Assert.AreEqual(4, transition.Operation.TripQuantity, 0.001);
        Assert.AreEqual("I OP", transition.Operation.QuantitySymbol);
        Assert.AreEqual("A", transition.Operation.QuantityUnit);

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

    [TestMethod]
    public void Recorder_UsesVoltageQuantityAndUnitForUndervoltageTrip()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousEnabled = false,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings
            {
                Undervoltage27Enabled = true,
                Undervoltage27PickupV = 90,
                Undervoltage27Delay = TimeSpan.FromMilliseconds(20),
                Undervoltage27Logic = VoltageSelectionLogic.TwoOfThree
            }
        };
        var engine = new ProtectionEngine(settings);
        var recorder = new RelayOperationRecorder();
        var start = DateTimeOffset.UtcNow;
        var tripSeen = false;

        for (var elapsed = 0; elapsed <= 25; elapsed += 5)
        {
            var phasors = BalancedPhasors(0.5, 70);
            var frame = new MeasurementFrame(
                start.AddMilliseconds(elapsed),
                0.5,
                0.5,
                0.5,
                0,
                SmvTrustState.Healthy,
                phasors);
            var transition = recorder.Observe(engine.Evaluate(frame), frame);
            tripSeen |= transition.TripOccurred;
        }

        Assert.IsTrue(tripSeen);
        Assert.IsNotNull(recorder.Current);
        Assert.AreEqual("27", recorder.Current.TripElement);
        Assert.AreEqual("V OP", recorder.Current.QuantitySymbol);
        Assert.AreEqual("V", recorder.Current.QuantityUnit);
        Assert.AreEqual(70, recorder.Current.TripQuantity, 0.001);
    }

    [TestMethod]
    public void Recorder_RecoversShortFaultEvidenceWhenUiFirstObservesAfterDropout()
    {
        var engine = new ProtectionEngine(InstantaneousOnlySettings(TimeSpan.FromMilliseconds(20)));
        var recorder = new RelayOperationRecorder();
        var start = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

        engine.Evaluate(Frame(start, SmvTrustState.Healthy));
        var operated = engine.Evaluate(Frame(start.AddMilliseconds(20), SmvTrustState.Healthy));
        Assert.IsTrue(operated.TripLatched);
        var afterDropout = engine.Evaluate(new MeasurementFrame(
            start.AddMilliseconds(25),
            1,
            1,
            1,
            0,
            SmvTrustState.Healthy));

        var transition = recorder.Observe(afterDropout, new MeasurementFrame(
            start.AddMilliseconds(25),
            1,
            1,
            1,
            0,
            SmvTrustState.Healthy));

        Assert.IsTrue(transition.PickupStarted, "The faceplate must receive the missed pickup edge from engine evidence.");
        Assert.IsTrue(transition.TripOccurred);
        Assert.AreEqual(start, transition.Operation?.PickupTimestamp);
        Assert.AreEqual(start.AddMilliseconds(20), transition.Operation?.TripTimestamp);
        Assert.AreEqual(20, transition.Operation?.OperateTime?.TotalMilliseconds ?? -1, 0.001);
        Assert.AreEqual("50P-1", transition.Operation?.TripElement);
    }

    [TestMethod]
    public void Recorder_UsesOperatedFeederElementInsteadOfEarlierLegacyPickup()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 1,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(500),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings
            {
                Undervoltage27Enabled = true,
                Undervoltage27PickupV = 90,
                Undervoltage27Delay = TimeSpan.FromMilliseconds(20),
                Undervoltage27Logic = VoltageSelectionLogic.TwoOfThree
            }
        };
        var engine = new ProtectionEngine(settings);
        var recorder = new RelayOperationRecorder();
        var start = new DateTimeOffset(2026, 8, 3, 13, 0, 0, TimeSpan.Zero);
        ProtectionSnapshot snapshot = ProtectionSnapshot.Ready(start);
        MeasurementFrame frame = null!;

        for (var elapsed = 0; elapsed <= 30; elapsed += 5)
        {
            var phasors = BalancedPhasors(2, 70);
            frame = new MeasurementFrame(
                start.AddMilliseconds(elapsed),
                2,
                2,
                2,
                0,
                SmvTrustState.Healthy,
                phasors);
            snapshot = engine.Evaluate(frame);
        }

        var transition = recorder.Observe(snapshot, frame);

        Assert.IsTrue(transition.TripOccurred);
        Assert.AreEqual("27", transition.Operation?.PickupElement);
        Assert.AreEqual("27", transition.Operation?.TripElement);
        Assert.AreEqual("V", transition.Operation?.QuantityUnit);
        Assert.AreEqual(20, transition.Operation?.OperateTime?.TotalMilliseconds ?? -1, 0.001);
    }

    [TestMethod]
    public void Recorder_DoesNotReplaceCompletedTripWithLaterPickupBeforeReset()
    {
        var engine = new ProtectionEngine(InstantaneousOnlySettings(TimeSpan.FromMilliseconds(10)));
        var recorder = new RelayOperationRecorder();
        var start = DateTimeOffset.UtcNow;

        var pickupFrame = Frame(start, SmvTrustState.Healthy);
        recorder.Observe(engine.Evaluate(pickupFrame), pickupFrame);
        var tripFrame = Frame(start.AddMilliseconds(10), SmvTrustState.Healthy);
        recorder.Observe(engine.Evaluate(tripFrame), tripFrame);
        var completed = recorder.Current;

        var dropoutFrame = new MeasurementFrame(start.AddMilliseconds(15), 1, 1, 1, 0, SmvTrustState.Healthy);
        recorder.Observe(engine.Evaluate(dropoutFrame), dropoutFrame);
        var laterPickupFrame = Frame(start.AddMilliseconds(20), SmvTrustState.Healthy);
        recorder.Observe(engine.Evaluate(laterPickupFrame), laterPickupFrame);

        Assert.IsNotNull(completed);
        Assert.AreSame(completed, recorder.Current);
        Assert.IsNotNull(recorder.Current?.TripTimestamp);
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

    private static PhasorMeasurementSet BalancedPhasors(double current, double voltage)
    {
        return new PhasorMeasurementSet(
            Polar(current, 0),
            Polar(current, -120),
            Polar(current, 120),
            Complex.Zero,
            Polar(voltage, 0),
            Polar(voltage, -120),
            Polar(voltage, 120),
            Complex.Zero,
            50);
    }

    private static Complex Polar(double magnitude, double angleDegrees)
        => Complex.FromPolarCoordinates(magnitude, angleDegrees * Math.PI / 180);
}
