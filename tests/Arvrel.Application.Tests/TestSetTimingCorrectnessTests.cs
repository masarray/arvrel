using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class TestSetTimingCorrectnessTests
{
    [TestMethod]
    public void NewTimedTestCannotArmWhilePreviousTripFeedbackIsStillActive()
    {
        var source = CreateFaultSource();
        var relay = new ResearchProtectionEngine(FastPhase50Settings());
        var bench = new ClosedLoopVirtualTestBench(source, relay);

        Assert.IsTrue(bench.StartInjection());
        var first = bench.Advance(TimeSpan.FromMilliseconds(100));

        Assert.AreEqual(VirtualTestSetTimerState.Completed, first.TestSet.TimerState);
        Assert.IsTrue(first.TestSet.TripInput);
        Assert.IsFalse(source.IsRunning);
        var firstRunId = first.TestSet.TestRunId;
        var firstTrip = first.TestSet.TripDetectedAt;

        Assert.IsFalse(bench.TryStartInjection(out var reason));

        var blocked = bench.TestSetSnapshot;
        Assert.AreEqual(VirtualTestSetTimerState.Blocked, blocked.TimerState);
        Assert.AreEqual(firstRunId, blocked.TestRunId, "A blocked arm attempt must not create a new test run.");
        Assert.AreEqual(firstTrip, blocked.TripDetectedAt, "The previous measured edge must not be replaced by a synthetic new edge.");
        Assert.IsTrue(blocked.TripInput, "The physical feedback state must remain visible instead of being forced low.");
        StringAssert.Contains(reason ?? string.Empty, "TESTSET.BI1 TRIP");
        Assert.IsFalse(source.IsRunning);
    }

    [TestMethod]
    public void RelayResetAllowsFeedbackToReleaseBeforeNextTimedRunArms()
    {
        var source = CreateFaultSource();
        var relay = new ResearchProtectionEngine(FastPhase50Settings());
        var bench = new ClosedLoopVirtualTestBench(source, relay);

        Assert.IsTrue(bench.StartInjection());
        var first = bench.Advance(TimeSpan.FromMilliseconds(100));
        Assert.IsTrue(first.TestSet.TripInput);

        relay.Reset();

        Assert.IsTrue(bench.TryStartInjection(out var reason), reason);
        Assert.IsNull(reason);
        Assert.AreEqual(VirtualTestSetTimerState.Armed, bench.TestSetSnapshot.TimerState);
        Assert.IsFalse(bench.TestSetSnapshot.TripInput, "BI1 must physically release through the modeled path before the next run arms.");
        Assert.IsTrue(source.IsRunning);
        Assert.IsTrue(bench.TestSetSnapshot.TestRunId > first.TestSet.TestRunId);
    }

    [TestMethod]
    public void AutoStopReturnsAtAcceptedBi1EdgeAndPreservesExactFaultCapture()
    {
        var source = CreateFaultSource();
        var relay = new ResearchProtectionEngine(FastPhase50Settings());
        var bench = new ClosedLoopVirtualTestBench(source, relay);

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(100));

        Assert.AreEqual(VirtualTestSetTimerState.Completed, result.TestSet.TimerState);
        Assert.IsNotNull(result.TestSet.TripDetectedAt);
        Assert.AreEqual(result.RelayMeasurement.Timestamp, result.TestSet.TripDetectedAt!.Value,
            "The returned step must be the exact quantum where TESTSET.BI1 accepted the trip edge.");
        Assert.IsTrue(result.Source.IsRunning,
            "The captured source frame is the energized fault frame that existed at the edge, even though output is stopped immediately afterward.");
        Assert.IsFalse(result.TestSet.OutputRunning);
        Assert.IsFalse(source.IsRunning);

        var capture = bench.TripCapture;
        Assert.IsNotNull(capture);
        Assert.AreEqual(result.TestSet.TestRunId, capture!.TestRunId);
        Assert.AreEqual(result.TestSet.TripDetectedAt, capture.TestSet.TripDetectedAt);
        Assert.IsTrue(capture.Source.Measurement.PhaseA > 4,
            "Trip capture must retain the injected fault measurement rather than a post-stop zero frame.");

        var afterStop = bench.Advance(TimeSpan.FromMilliseconds(40));
        Assert.AreEqual(0d, afterStop.Source.Measurement.PhaseA, 1e-9,
            "The physical virtual output should be zero after auto-stop.");
        Assert.IsTrue(bench.TripCapture!.Source.Measurement.PhaseA > 4,
            "The frozen trip capture must survive later zero-output simulation frames.");
    }

    [TestMethod]
    public void CurrentRunExposesComparableRelayAndTestSetTimingDomains()
    {
        var source = CreateFaultSource();
        var relay = new ResearchProtectionEngine(FastPhase50Settings());
        var bench = new ClosedLoopVirtualTestBench(source, relay);

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(100));

        Assert.IsNotNull(result.TestSet.RelayPickupTime);
        Assert.IsNotNull(result.TestSet.RelayTripTime);
        Assert.IsNotNull(result.TestSet.RelayPickupToTrip);
        Assert.IsNotNull(result.TestSet.TripTime);
        Assert.IsTrue(result.TestSet.RelayTripTime < result.TestSet.TripTime,
            "START→relay internal trip must precede START→TESTSET.BI1 by the modeled output-contact path.");
        Assert.AreEqual(
            FastPhase50Settings().PhaseInstantaneousDelay.TotalMilliseconds,
            result.TestSet.RelayPickupToTrip!.Value.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            "Relay P→T must describe the same test run and remain distinct from test-set START→BI1 timing.");
    }

    private static DeterministicLabScenario CreateFaultSource()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("Three-phase fault");
        return source;
    }

    private static ProtectionSettings FastPhase50Settings()
        => new()
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 4,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(10),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings()
        };
}
