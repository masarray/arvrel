using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class P0TimingBudgetRegressionTests
{
    [TestMethod]
    public void DesktopRealismProfileKeepsBinaryFeedbackWithinExactModeledBudget()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("A-G fault");
        var settings = Phase50OnlySettings(TimeSpan.FromMilliseconds(60));
        var contact = VirtualRelayContactProfile.RealisticNumericalRelay;
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(settings),
            contactProfile: contact,
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault);

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(250));

        Assert.AreEqual(VirtualTestSetTimerState.Completed, result.TestSet.TimerState);
        Assert.IsNotNull(result.TestSet.RelayPickupDetectedAt);
        Assert.IsNotNull(result.TestSet.RelayTripDetectedAt);
        Assert.IsNotNull(result.TestSet.PickupDetectedAt);
        Assert.IsNotNull(result.TestSet.TripDetectedAt);

        var pickupPath = result.TestSet.PickupDetectedAt!.Value - result.TestSet.RelayPickupDetectedAt!.Value;
        var tripPath = result.TestSet.TripDetectedAt!.Value - result.TestSet.RelayTripDetectedAt!.Value;
        var expectedPickupPath = contact.PickupOperateDelay + contact.ContactBounceDuration + contact.TestSetInputDebounce;
        var expectedTripPath = contact.TripOperateDelay + contact.ContactBounceDuration + contact.TestSetInputDebounce;

        Assert.AreEqual(expectedPickupPath.TotalMilliseconds, pickupPath.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            $"BO2 request -> TESTSET.BI2 observed {pickupPath.TotalMilliseconds:0.000} ms.");
        Assert.AreEqual(expectedTripPath.TotalMilliseconds, tripPath.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            $"BO1 request -> TESTSET.BI1 observed {tripPath.TotalMilliseconds:0.000} ms.");
    }

    [TestMethod]
    public void DefaultDesktopFaultKeepsPhase50AndExternalTripTimingCorrelated()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("A-G fault");
        var settings = new ProtectionSettings();
        var contact = VirtualRelayContactProfile.RealisticNumericalRelay;
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(settings),
            contactProfile: contact,
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault);

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(250));

        Assert.AreEqual(VirtualTestSetTimerState.Completed, result.TestSet.TimerState);
        Assert.AreEqual("50P-1", result.Protection.LatchedOperation?.Element,
            "The default A-G desktop scenario should still be reported as the faster 50P-1 operation.");
        Assert.IsNotNull(result.TestSet.RelayPickupToTrip);
        Assert.AreEqual(settings.PhaseInstantaneousDelay.TotalMilliseconds,
            result.TestSet.RelayPickupToTrip!.Value.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            $"Default desktop relay P->T observed {result.TestSet.RelayPickupToTrip.Value.TotalMilliseconds:0.000} ms.");

        Assert.IsNotNull(result.TestSet.RelayTripDetectedAt);
        Assert.IsNotNull(result.TestSet.TripDetectedAt);
        var externalTripPath = result.TestSet.TripDetectedAt!.Value - result.TestSet.RelayTripDetectedAt!.Value;
        var expectedTripPath = contact.TripOperateDelay + contact.ContactBounceDuration + contact.TestSetInputDebounce;
        Assert.AreEqual(expectedTripPath.TotalMilliseconds,
            externalTripPath.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            $"Default desktop relay trip -> TESTSET.BI1 observed {externalTripPath.TotalMilliseconds:0.000} ms.");
    }

    [TestMethod]
    public void Phase50PickupToTripMatchesConfiguredDefiniteTimeOnDeterministicGrid()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("A-G fault");
        var delay = TimeSpan.FromMilliseconds(60);
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(Phase50OnlySettings(delay)),
            contactProfile: VirtualRelayContactProfile.RealisticNumericalRelay,
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault)
        {
            AutoStopOnTrip = false
        };

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(180));

        Assert.IsNotNull(result.TestSet.RelayPickupToTrip);
        Assert.AreEqual(delay.TotalMilliseconds, result.TestSet.RelayPickupToTrip!.Value.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            $"Observed relay P->T {result.TestSet.RelayPickupToTrip.Value.TotalMilliseconds:0.000} ms.");
    }

    [TestMethod]
    public void DesktopAnalogInjectionDoesNotAddAnUnmodeledWholeCycleBeforePickup()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("A-G fault");
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(Phase50OnlySettings(TimeSpan.FromMilliseconds(60))),
            contactProfile: VirtualRelayContactProfile.RealisticNumericalRelay,
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault)
        {
            AutoStopOnTrip = false
        };

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(80));

        Assert.IsNotNull(result.TestSet.RelayPickupTime);
        var maximumExpected = VirtualRelayFrontEndProfile.NumericalRelayDefault.MeasurementGroupDelay +
                              ClosedLoopVirtualTestBench.SimulationQuantum;
        Assert.IsTrue(result.TestSet.RelayPickupTime <= maximumExpected,
            $"START -> relay pickup was {result.TestSet.RelayPickupTime!.Value.TotalMilliseconds:0.000} ms; expected <= {maximumExpected.TotalMilliseconds:0.000} ms.");
    }

    private static ProtectionSettings Phase50OnlySettings(TimeSpan delay)
        => new()
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 4,
            PhaseInstantaneousDelay = delay,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings()
        };
}
