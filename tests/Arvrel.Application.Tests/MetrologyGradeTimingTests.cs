using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class MetrologyGradeTimingTests
{
    [TestMethod]
    public void CmcStyleProfileUsesMicrosecondClockAndTenKilohertzBinarySampling()
    {
        var profile = MetrologyTimingProfile.CmcStyle;
        profile.Validate();

        Assert.AreEqual(1, profile.ClockResolutionMicroseconds);
        Assert.AreEqual(10_000, profile.BinaryInputSampleRateHz);
        Assert.AreEqual(100, profile.BinaryInputSamplePeriodMicroseconds);
        Assert.AreEqual(0.5, profile.BinaryInputDeglitch.TotalMilliseconds, 1e-9);
        Assert.AreEqual(0, profile.BinaryInputDebounceHoldoff.TotalMilliseconds, 1e-9);
    }

    [TestMethod]
    public void UnsupportedClockResolutionIsRejectedInsteadOfMisreported()
    {
        var profile = MetrologyTimingProfile.CmcStyle with { ClockResolutionMicroseconds = 100 };

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => profile.Validate(),
            "Until every metrology timestamp producer is quantized to another domain, the engine must advertise only its implemented 1 us clock.");
    }

    [TestMethod]
    public void ArmedElapsedTimeAdvancesFromMetrologyClockBetweenDiscreteEvents()
    {
        var source = CreateSource("Normal balanced");
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousEnabled = false,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings()
        };
        var bench = CreateMetrologyBench(source, settings) { AutoStopOnTrip = false };

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(3));

        Assert.AreEqual(VirtualTestSetTimerState.Armed, result.TestSet.TimerState);
        Assert.AreEqual(3.0, result.TestSet.ElapsedTime!.Value.TotalMilliseconds, 1e-9,
            "The live timer must advance from the monotonic clock even when OutputApplied is still the newest discrete timeline event.");
        Assert.AreEqual(3_000L, result.TestSet.ObservedMicroseconds);
    }

    [TestMethod]
    public void CausalRelayFrontEndStartsFromSettledPreFaultHistoryAndRespondsWithoutOneCycleBlackout()
    {
        var source = CreateSource("A-G fault");
        var bench = CreateMetrologyBench(source, Phase50OnlySettings(delayMilliseconds: 60));
        bench.AutoStopOnTrip = false;

        Assert.IsTrue(bench.StartInjection());
        Assert.IsTrue(bench.CausalFrontEndSnapshot!.MeasurementValid,
            "A powered relay should enter a test with a settled pre-fault acquisition history instead of an empty measurement window.");

        var early = bench.Advance(TimeSpan.FromMilliseconds(5));
        Assert.IsFalse(early.Protection.Phase50.Pickup,
            "The causal filter must not jump immediately to the final fault phasor before enough post-T0 samples replace pre-fault history.");

        var responded = bench.Advance(TimeSpan.FromMilliseconds(10));
        Assert.IsTrue(responded.Protection.Phase50.Pickup,
            "A strong 8.4 A fault should cross the 4 A threshold during the causal window transition, without an artificial full-cycle startup blackout.");
        Assert.IsNotNull(bench.CausalFrontEndSnapshot.MeasurementValidAt);
    }

    [TestMethod]
    public void DesktopDefaultA_B_GRunHasNoUnexplainedTripTimingGap()
    {
        var source = CreateSource("A-B-G fault");
        var settings = new ProtectionSettings();
        var bench = CreateMetrologyBench(source, settings);

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(250));

        Assert.AreEqual(VirtualTestSetTimerState.Completed, result.TestSet.TimerState);
        Assert.IsNotNull(result.TestSet.RelayPickupTime,
            "Generic relay ANY-PICKUP timing should remain observable because it drives BO2/TESTSET.BI2.");
        Assert.IsNotNull(result.TestSet.RelayTripTime);
        Assert.IsNotNull(result.TestSet.RelayTripToBi1);
        Assert.IsNotNull(result.TestSet.TripTime);
        Assert.AreEqual("50P-1", result.Protection.LatchedOperation?.Element);

        var genericPickupMs = result.TestSet.RelayPickupTime!.Value.TotalMilliseconds;
        Assert.IsTrue(genericPickupMs < 6,
            $"With all desktop elements enabled, an earth/time element may assert generic ANY-PICKUP before 50P. Observed generic pickup {genericPickupMs:0.000} ms.");

        var operationTiming = RelayOperationTimingCorrelator.Correlate(result.TestSet, result.Protection);
        Assert.IsNotNull(operationTiming);
        Assert.AreEqual("50P-1", operationTiming!.Element);
        Assert.IsTrue(operationTiming.ElementPickupFromStart > result.TestSet.RelayPickupTime,
            "Generic BO2 ANY-PICKUP may precede the element-specific 50P pickup and must not be substituted into 50P P→T timing.");
        Assert.IsTrue(operationTiming.ElementPickupFromStart.TotalMilliseconds < 20,
            $"The settled causal 50P measurement should recognize the strong A-B-G fault well before the old ~40 ms source/phasor latency. Observed {operationTiming.ElementPickupFromStart.TotalMilliseconds:0.000} ms.");

        Assert.AreEqual(
            settings.PhaseInstantaneousDelay.TotalMilliseconds,
            operationTiming.ElementPickupToTrip.TotalMilliseconds,
            1e-9,
            "The operated 50P element must measure exactly 60 ms pickup→trip when the setting is exactly representable on the relay processing grid.");
        Assert.IsTrue(operationTiming.LiveTripMatchesOperationRecord(ClosedLoopVirtualTestBench.SimulationQuantum),
            $"Live TripLatched/BO1 request must correlate with the 50P operation record within one processing quantum. Difference {operationTiming.OperationRecordToLiveTripRequest?.TotalMilliseconds:0.000} ms.");

        var external = result.TestSet.RelayTripToBi1!.Value.TotalMilliseconds;
        Assert.IsTrue(external >= 4.5 && external <= 4.6,
            $"Live trip request→BO1 bounce/deglitch→10 kHz BI1 must stay inside the modeled 4.5..4.6 ms budget. Observed {external:0.000} ms.");

        var measuredTripMs = result.TestSet.TripTime!.Value.TotalMilliseconds;
        Assert.IsTrue(measuredTripMs < 90,
            $"The previous unexplained ~124.75 ms desktop result must not regress. The settled causal 60 ms 50P chain should complete well below 90 ms with this profile. Observed {measuredTripMs:0.000} ms.");

        Assert.AreEqual(100, result.TestSet.TimingResolutionMicroseconds);
        Assert.IsNotNull(result.TestSet.TripDetectedMicroseconds);
        var bi1Event = result.TestSet.MetrologyTimeline!
            .Single(item => item.Kind == MetrologyEventKind.TestSetBi1Trip);
        Assert.AreEqual(result.TestSet.TripDetectedMicroseconds!.Value, bi1Event.Microseconds,
            "Relative test duration may carry the asynchronous T0 phase; the accepted BI1 event itself must remain authoritative and self-consistent on the independent BI clock.");
    }

    [TestMethod]
    public void TripCaptureTimestampDescribesStoredStateNotEarlierBiEdge()
    {
        var source = CreateSource("A-G fault");
        var bench = CreateMetrologyBench(source, Phase50OnlySettings(delayMilliseconds: 20));

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(150));
        var capture = bench.TripCapture;

        Assert.IsNotNull(capture);
        Assert.IsNotNull(result.TestSet.TripDetectedAt);
        Assert.AreEqual(capture!.RelayMeasurement.Timestamp, capture.CapturedAt,
            "CapturedAt must timestamp the source/relay/protection state actually stored in the capture.");
        Assert.IsTrue(capture.CapturedAt >= result.TestSet.TripDetectedAt!.Value,
            "A 10 kHz BI edge may precede the next 4 kHz relay quantum, but stored state must never be labeled as if it existed before that quantum.");
        Assert.IsTrue(capture.CapturedAt - result.TestSet.TripDetectedAt.Value < ClosedLoopVirtualTestBench.SimulationQuantum,
            "Capture-state observation should remain less than one relay processing quantum after the accepted BI edge.");
    }

    [TestMethod]
    public void MetrologyTimelineOrdersPhysicalClosedLoopEvents()
    {
        var source = CreateSource("A-G fault");
        var bench = CreateMetrologyBench(source, Phase50OnlySettings(delayMilliseconds: 20));

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(150));
        var timeline = result.TestSet.MetrologyTimeline;

        Assert.IsNotNull(timeline);
        CollectionAssert.Contains(timeline!.Select(item => item.Kind).ToList(), MetrologyEventKind.OutputApplied);
        CollectionAssert.Contains(timeline.Select(item => item.Kind).ToList(), MetrologyEventKind.RelayPickupAssert);
        CollectionAssert.Contains(timeline.Select(item => item.Kind).ToList(), MetrologyEventKind.RelayTripRequest);
        CollectionAssert.Contains(timeline.Select(item => item.Kind).ToList(), MetrologyEventKind.TestSetBi2Pickup);
        CollectionAssert.Contains(timeline.Select(item => item.Kind).ToList(), MetrologyEventKind.TestSetBi1Trip);
        CollectionAssert.Contains(timeline.Select(item => item.Kind).ToList(), MetrologyEventKind.OutputStopCommand);

        for (var index = 1; index < timeline.Count; index++)
        {
            Assert.IsTrue(timeline[index].Microseconds >= timeline[index - 1].Microseconds,
                "Metrology timeline must be monotonic in the TESTSET clock domain.");
        }

        var tripRequest = timeline.First(item => item.Kind == MetrologyEventKind.RelayTripRequest).Microseconds;
        var bi1 = timeline.First(item => item.Kind == MetrologyEventKind.TestSetBi1Trip).Microseconds;
        Assert.AreEqual(result.TestSet.RelayTripToBi1!.Value.TotalMilliseconds, (bi1 - tripRequest) / 1_000d, 1e-9);
    }

    [TestMethod]
    public void OpenTripWireStillPreventsMeasuredTripInMetrologyMode()
    {
        var source = CreateSource("A-G fault");
        var bench = CreateMetrologyBench(source, Phase50OnlySettings(delayMilliseconds: 20));
        bench.SetWireConnected(VirtualTestBenchTopology.TripFeedbackWireId, connected: false);

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(150));

        Assert.IsTrue(result.Protection.TripLatched);
        Assert.IsNull(result.TestSet.TripTime);
        Assert.IsNull(result.TestSet.TripDetectedMicroseconds);
        Assert.IsTrue(source.IsRunning,
            "Relay internals must never auto-stop the virtual test set when BO1→BI1 is open.");
    }

    private static ClosedLoopVirtualTestBench CreateMetrologyBench(
        DeterministicLabScenario source,
        ProtectionSettings settings)
        => new(
            source,
            new ResearchProtectionEngine(settings),
            contactProfile: VirtualRelayContactProfile.RealisticNumericalRelay,
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault,
            metrologyProfile: MetrologyTimingProfile.CmcStyle);

    private static DeterministicLabScenario CreateSource(string preset)
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset(preset);
        return source;
    }

    private static ProtectionSettings Phase50OnlySettings(double delayMilliseconds)
        => new()
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 4,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(delayMilliseconds),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings()
        };
}
