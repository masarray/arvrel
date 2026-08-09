using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class DesktopClosedLoopTimingRegressionTests
{
    [TestMethod]
    public void DesktopA_G50PChainMatchesConfiguredRelayAndBinaryPathBudgets()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("A-G fault");

        var settings = new ProtectionSettings
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 4,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(60),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings()
        };
        var contact = VirtualRelayContactProfile.RealisticNumericalRelay;
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(settings),
            contactProfile: contact,
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault);

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(200));

        Assert.IsNotNull(result.Protection.LatchedOperation);
        Assert.IsNotNull(result.TestSet.InjectionStartedAt);
        Assert.IsNotNull(result.TestSet.TripDetectedAt);
        Assert.IsNotNull(result.TestSet.PickupDetectedAt);

        var operation = result.Protection.LatchedOperation!;
        var relayPickupToTrip = operation.TripTimestamp - operation.PickupTimestamp;
        Assert.AreEqual(
            settings.PhaseInstantaneousDelay.TotalMilliseconds,
            relayPickupToTrip.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            $"50P pickup→operate must match its 60 ms definite-time setting. Observed {relayPickupToTrip.TotalMilliseconds:0.000} ms.");

        var externalTripPath = result.TestSet.TripDetectedAt!.Value - operation.TripTimestamp;
        var expectedExternalTripPath = contact.TripOperateDelay +
                                       contact.ContactBounceDuration +
                                       contact.TestSetInputDebounce;
        Assert.AreEqual(
            expectedExternalTripPath.TotalMilliseconds,
            externalTripPath.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            $"Relay operate→BO1→TESTSET.BI1 must equal the configured 4.5 ms path. Observed {externalTripPath.TotalMilliseconds:0.000} ms.");

        var externalPickupPath = result.TestSet.PickupDetectedAt!.Value - operation.PickupTimestamp;
        var expectedExternalPickupPath = contact.PickupOperateDelay +
                                         contact.ContactBounceDuration +
                                         contact.TestSetInputDebounce;
        Assert.AreEqual(
            expectedExternalPickupPath.TotalMilliseconds,
            externalPickupPath.TotalMilliseconds,
            ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds,
            $"Relay pickup→BO2→TESTSET.BI2 must equal the configured 2.5 ms path. Observed {externalPickupPath.TotalMilliseconds:0.000} ms.");

        Assert.IsFalse(source.IsRunning, "BI1 acceptance must auto-stop the virtual secondary injector.");
    }
}
