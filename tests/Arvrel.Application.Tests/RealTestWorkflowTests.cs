using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class RealTestWorkflowTests
{
    [TestMethod]
    public void ContinuousSourceUpdateDoesNotRebuildCoherenceWindow()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("Normal balanced");
        source.StartInjection();
        source.Advance(TimeSpan.FromMilliseconds(20));
        var beforeIndex = source.SourceSampleIndex;

        var changed = RealTestWorkflowEngine.WithSignalRms(
            source.ActiveProfile,
            VirtualInjectionSignal.PhaseACurrent,
            3.25,
            "continuous update");
        Assert.IsTrue(source.ApplyContinuousProfile(changed));

        Assert.AreEqual("coherent", source.WindowStatus);
        Assert.AreEqual(beforeIndex, source.SourceSampleIndex, "A live test-set step must preserve waveform/sample phase continuity.");
        Assert.AreEqual(3.25, source.ActiveProfile.PhaseACurrent.Rms, 1e-9);
    }

    [TestMethod]
    public void StepRampStopsOnWiredPickupInput()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("Normal balanced");
        var bench = new ClosedLoopVirtualTestBench(source, new ResearchProtectionEngine(PickupOnlySettings()));
        var engine = new RealTestWorkflowEngine(source, bench);

        var result = engine.RunRamp(new StepRampDefinition(
            "50P pickup ramp",
            VirtualInjectionSignal.PhaseACurrent,
            2,
            6,
            0.5,
            TimeSpan.FromMilliseconds(2),
            RealTestFeedback.Pickup));

        Assert.AreEqual(RealTestWorkflowOutcome.FeedbackDetected, result.Outcome);
        Assert.AreEqual(4.0, result.DetectedAtRms!.Value, 1e-9);
        Assert.IsTrue(result.Observations.Last().PickupInput);
        Assert.IsFalse(source.IsRunning, "Workflow completion must leave the virtual test-set output de-energized.");
    }

    [TestMethod]
    public void OpenPickupFeedbackWireMakesRampReportNoFeedback()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("Normal balanced");
        var bench = new ClosedLoopVirtualTestBench(source, new ResearchProtectionEngine(PickupOnlySettings()));
        bench.SetWireConnected(VirtualTestBenchTopology.PickupFeedbackWireId, connected: false);
        var engine = new RealTestWorkflowEngine(source, bench);

        var result = engine.RunRamp(new StepRampDefinition(
            "open BI2",
            VirtualInjectionSignal.PhaseACurrent,
            2,
            6,
            0.5,
            TimeSpan.FromMilliseconds(2),
            RealTestFeedback.Pickup));

        Assert.AreEqual(RealTestWorkflowOutcome.NoFeedback, result.Outcome);
        Assert.IsNull(result.DetectedAtRms);
        Assert.IsFalse(result.Observations.Any(point => point.PickupInput));
    }

    [TestMethod]
    public void TripRampUsesBi1AndIncludesRelayOutputContactPath()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("Normal balanced");
        var settings = PickupOnlySettings() with { PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(5) };
        var bench = new ClosedLoopVirtualTestBench(source, new ResearchProtectionEngine(settings));
        var engine = new RealTestWorkflowEngine(source, bench);

        var result = engine.RunRamp(new StepRampDefinition(
            "50P trip ramp",
            VirtualInjectionSignal.PhaseACurrent,
            2,
            5,
            0.5,
            TimeSpan.FromMilliseconds(10),
            RealTestFeedback.Trip));

        Assert.AreEqual(RealTestWorkflowOutcome.FeedbackDetected, result.Outcome);
        Assert.AreEqual(4.0, result.DetectedAtRms!.Value, 1e-9);
        Assert.IsTrue(result.Observations.Last().TripInput);
    }

    [TestMethod]
    public void PulseRampReturnsPulseThatFirstProducesPickup()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("Normal balanced");
        var bench = new ClosedLoopVirtualTestBench(source, new ResearchProtectionEngine(PickupOnlySettings()));
        var engine = new RealTestWorkflowEngine(source, bench);

        var result = engine.RunPulseRamp(new PulseRampDefinition(
            "50P pulse ramp",
            VirtualInjectionSignal.PhaseACurrent,
            2,
            3,
            5,
            0.5,
            TimeSpan.FromMilliseconds(3),
            TimeSpan.FromMilliseconds(3),
            RealTestFeedback.Pickup));

        Assert.AreEqual(RealTestWorkflowOutcome.FeedbackDetected, result.Outcome);
        Assert.AreEqual(3, result.PulsesApplied);
        Assert.AreEqual(4.0, result.DetectedAtRms!.Value, 1e-9);
    }

    [TestMethod]
    public void PickupDropoutSearchUsesExternalBi2StateForBothDirections()
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("Normal balanced");
        var settings = PickupOnlySettings() with { PhaseInstantaneousDropoutRatio = 0.80 };
        var bench = new ClosedLoopVirtualTestBench(source, new ResearchProtectionEngine(settings));
        var engine = new RealTestWorkflowEngine(source, bench);

        var result = engine.RunPickupDropoutSearch(new PickupDropoutSearchDefinition(
            "50P pickup dropout",
            VirtualInjectionSignal.PhaseACurrent,
            2,
            5,
            0.5,
            TimeSpan.FromMilliseconds(2)));

        Assert.AreEqual(RealTestWorkflowOutcome.Completed, result.Outcome);
        Assert.AreEqual(4.0, result.PickupRms!.Value, 1e-9);
        Assert.AreEqual(3.0, result.DropoutRms!.Value, 1e-9);
        Assert.AreEqual(0.75, result.DropoutRatio!.Value, 1e-9);
        Assert.IsTrue(result.Observations.Any(point => point.Stage == "DROPOUT SEARCH" && !point.PickupInput));
    }

    [TestMethod]
    public void StateSequenceAdvancesFaultStateOnWiredTripEdge()
    {
        var source = new DeterministicLabScenario();
        var settings = PickupOnlySettings() with { PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(5) };
        var bench = new ClosedLoopVirtualTestBench(source, new ResearchProtectionEngine(settings));
        var engine = new RealTestWorkflowEngine(source, bench);
        var sequence = new StateSequenceDefinition(
            "fault sequence",
            new[]
            {
                new RealTestState("Pre-fault", VirtualInjectionPresets.Create("Normal balanced"), TimeSpan.FromMilliseconds(5)),
                new RealTestState("Fault", VirtualInjectionPresets.Create("Three-phase fault"), TimeSpan.FromMilliseconds(30), RealTestFeedback.Trip),
                new RealTestState("Post-fault", VirtualInjectionPresets.Create("Normal balanced"), TimeSpan.FromMilliseconds(5))
            });

        var result = engine.RunStateSequence(sequence);

        Assert.AreEqual(RealTestWorkflowOutcome.Completed, result.Outcome);
        Assert.AreEqual(3, result.States.Count);
        Assert.AreEqual("trip-edge", result.States[1].ExitReason);
        Assert.IsTrue(result.States[1].TripObserved);
        Assert.IsTrue(result.States[1].EndedAt - result.States[1].StartedAt < TimeSpan.FromMilliseconds(30));
    }

    [TestMethod]
    public void StateSequenceCannotAdvanceOnTripWhenBi1WireIsOpen()
    {
        var source = new DeterministicLabScenario();
        var settings = PickupOnlySettings() with { PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(5) };
        var bench = new ClosedLoopVirtualTestBench(source, new ResearchProtectionEngine(settings));
        bench.SetWireConnected(VirtualTestBenchTopology.TripFeedbackWireId, connected: false);
        var engine = new RealTestWorkflowEngine(source, bench);
        var sequence = new StateSequenceDefinition(
            "open trip feedback",
            new[]
            {
                new RealTestState("Pre-fault", VirtualInjectionPresets.Create("Normal balanced"), TimeSpan.FromMilliseconds(2)),
                new RealTestState("Fault", VirtualInjectionPresets.Create("Three-phase fault"), TimeSpan.FromMilliseconds(20), RealTestFeedback.Trip)
            });

        var result = engine.RunStateSequence(sequence);

        Assert.AreEqual("duration", result.States[1].ExitReason);
        Assert.IsFalse(result.States[1].TripObserved, "Workflow authority must remain on TESTSET.BI1, not relay TripLatched.");
    }

    private static ProtectionSettings PickupOnlySettings()
        => new()
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 4,
            PhaseInstantaneousDelay = TimeSpan.FromSeconds(5),
            PhaseInstantaneousDropoutRatio = 0.95,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings()
        };
}
