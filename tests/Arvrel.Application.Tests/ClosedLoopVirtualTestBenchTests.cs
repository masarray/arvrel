using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class ClosedLoopVirtualTestBenchTests
{
    [TestMethod]
    public void ConnectedTripFeedbackStopsInjectorFromBinaryInputEdge()
    {
        var source = CreateFaultSource();
        var relay = new ResearchProtectionEngine(FastPhase50Settings());
        var bench = new ClosedLoopVirtualTestBench(source, relay);

        Assert.IsTrue(bench.StartInjection());
        var result = bench.Advance(TimeSpan.FromMilliseconds(100));

        Assert.IsTrue(result.Protection.TripLatched);
        Assert.IsNotNull(result.Protection.LatchedOperation);
        Assert.IsNotNull(result.TestSet.TripDetectedAt);
        Assert.IsNotNull(result.TestSet.TripTime);
        Assert.IsFalse(source.IsRunning, "The injector must stop only after the wired test-set BI observes trip.");
        Assert.AreEqual(result.TestSet.TripDetectedAt, result.TestSet.OutputStoppedAt);

        var start = result.TestSet.InjectionStartedAt!.Value;
        var internalTripFromStart = result.Protection.LatchedOperation!.TripTimestamp - start;
        Assert.AreEqual(
            VirtualRelayContactProfile.NumericalRelayDefault.TripOperateDelay.TotalMilliseconds,
            (result.TestSet.TripTime!.Value - internalTripFromStart).TotalMilliseconds,
            0.001,
            "External test-set timing must include the virtual relay output-contact delay.");
    }

    [TestMethod]
    public void DisconnectedTripWirePreventsTestSetFromSeeingRelayTrip()
    {
        var source = CreateFaultSource();
        var relay = new ResearchProtectionEngine(FastPhase50Settings());
        var bench = new ClosedLoopVirtualTestBench(source, relay);
        bench.SetWireConnected(VirtualTestBenchTopology.TripFeedbackWireId, connected: false);

        bench.StartInjection();
        var result = bench.Advance(TimeSpan.FromMilliseconds(100));

        Assert.IsTrue(result.Protection.TripLatched, "The relay itself should still trip.");
        Assert.IsNull(result.TestSet.TripDetectedAt, "An open BO1->BI1 wire must make trip invisible to the injector.");
        Assert.IsNull(result.TestSet.TripTime);
        Assert.IsTrue(source.IsRunning, "The injector must not stop by peeking at ProtectionSnapshot.TripLatched.");
    }

    [TestMethod]
    public void DisconnectedAnalogWireChangesWhatRelayActuallyMeasures()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("A-G fault");
        var relay = new ResearchProtectionEngine(FastPhase50Settings());
        var bench = new ClosedLoopVirtualTestBench(source, relay);
        bench.SetWireConnected("testset.ia->relay.ia", connected: false);

        bench.StartInjection();
        var result = bench.Advance(TimeSpan.FromMilliseconds(100));

        Assert.IsTrue(source.ActiveProfile.PhaseACurrent.Rms > 4, "The injector remains configured above pickup.");
        Assert.AreEqual(0d, result.RelayMeasurement.PhaseA, 1e-9, "The relay input must see the open IA wire, not injector state directly.");
        Assert.IsFalse(result.Protection.Phase50.Pickup);
        Assert.IsFalse(result.Protection.TripLatched);
    }

    [TestMethod]
    public void TopologyFingerprintChangesWhenWireStateChanges()
    {
        var topology = VirtualTestBenchTopology.Default;
        var disconnected = topology.WithWireConnection(VirtualTestBenchTopology.TripFeedbackWireId, false);

        Assert.AreNotEqual(topology.Fingerprint(), disconnected.Fingerprint());
        Assert.IsTrue(topology.IsBinaryConnected(VirtualBinarySignal.Trip));
        Assert.IsFalse(disconnected.IsBinaryConnected(VirtualBinarySignal.Trip));
    }

    [TestMethod]
    public void SimulationAuthorityUsesExistingFourKilohertzSampleGrid()
    {
        Assert.AreEqual(0.25, ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds, 1e-9);
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
