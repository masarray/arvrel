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
    public void OneResetTransactionFullyReleasesFrozenDesktopFeedbackAndRearms()
    {
        var source = CreateFaultSource();
        var relay = new ResearchProtectionEngine(FastPhase50Settings());
        var bench = new ClosedLoopVirtualTestBench(
            source,
            relay,
            contactProfile: VirtualRelayContactProfile.RealisticNumericalRelay,
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault,
            metrologyProfile: MetrologyTimingProfile.CmcStyle);

        Assert.IsTrue(bench.StartInjection());
        var first = bench.Advance(TimeSpan.FromMilliseconds(150));

        Assert.AreEqual(VirtualTestSetTimerState.Completed, first.TestSet.TimerState);
        Assert.IsTrue(first.Protection.TripLatched);
        Assert.IsTrue(first.TestSet.TripInput);
        Assert.IsFalse(source.IsRunning, "Accepted BI1 must auto-stop the virtual source before reset.");
        var completedRunId = first.TestSet.TestRunId;
        var capture = bench.TripCapture;
        Assert.IsNotNull(capture);

        var reset = ClosedLoopRelayResetTransaction.Execute(bench, relay);

        Assert.IsTrue(reset.ResetApplied);
        Assert.IsTrue(reset.ReadyToRearm, reset.Detail);
        Assert.IsFalse(reset.SourceWasRunning);
        Assert.IsFalse(reset.FinalStep.Protection.TripLatched);
        Assert.IsFalse(reset.FinalStep.TestSet.PickupContactRaw, "BO2 must release during the same reset transaction.");
        Assert.IsFalse(reset.FinalStep.TestSet.TripContactRaw, "BO1 must release during the same reset transaction.");
        Assert.IsFalse(reset.FinalStep.TestSet.PickupInput, "TESTSET.BI2 must be low before RESET reports ready-to-rearm.");
        Assert.IsFalse(reset.FinalStep.TestSet.TripInput, "TESTSET.BI1 must be low before RESET reports ready-to-rearm.");
        Assert.IsFalse(source.IsRunning, "Relay reset must not restart or mutate the virtual source.");
        Assert.AreSame(capture, bench.TripCapture, "Completed trip evidence must survive relay reset for inspection.");
        Assert.IsTrue(
            reset.SimulatedSettleTime > bench.FeedbackSettleTime,
            "The desktop causal acquisition window should require more than the old fixed feedback delay, reproducing the former multi-click reset condition.");

        Assert.IsTrue(bench.TryStartInjection(out var reason), reason);
        Assert.IsNull(reason);
        Assert.AreEqual(VirtualTestSetTimerState.Armed, bench.TestSetSnapshot.TimerState);
        Assert.IsTrue(source.IsRunning);
        Assert.IsTrue(bench.TestSetSnapshot.TestRunId > completedRunId);
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
