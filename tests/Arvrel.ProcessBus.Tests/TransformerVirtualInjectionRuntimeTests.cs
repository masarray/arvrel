using Arvrel.ProcessBus;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class TransformerVirtualInjectionRuntimeTests
{
    [TestMethod]
    public void BalancedThroughLoad_UsesSynchronizedDistinctSides_AndRemainsStable()
    {
        var runtime = new TransformerVirtualInjectionRuntime();

        _ = runtime.Start();
        var snapshot = runtime.Advance(TimeSpan.FromMilliseconds(25));

        Assert.AreEqual(TransformerVirtualInjectionDefaults.HighVoltageStreamKey, runtime.HighVoltageSnapshot.Stream.Key);
        Assert.AreEqual(TransformerVirtualInjectionDefaults.LowVoltageStreamKey, runtime.LowVoltageSnapshot.Stream.Key);
        Assert.AreNotEqual(runtime.HighVoltageSnapshot.Stream.Key, runtime.LowVoltageSnapshot.Stream.Key);
        Assert.AreEqual((byte)2, runtime.HighVoltageSnapshot.SampleSynchronization);
        Assert.AreEqual((byte)2, runtime.LowVoltageSnapshot.SampleSynchronization);
        Assert.AreEqual(runtime.HighVoltageSnapshot.SampleCounter, runtime.LowVoltageSnapshot.SampleCounter);
        Assert.AreEqual("PAIR_ALIGNED", snapshot.Pairing.Code);
        Assert.IsNotNull(snapshot.Protection);
        Assert.IsFalse(snapshot.TripLatched);

        foreach (var phase in snapshot.Protection!.Differential.Phases)
            Assert.IsTrue(phase.OperatingCurrentPu < 1e-6, $"{phase.Phase} Idiff was {phase.OperatingCurrentPu:R}");
    }

    [TestMethod]
    public void InternalPhaseFault_VectorDrivesAuthoritative87T_ToTrip()
    {
        var runtime = new TransformerVirtualInjectionRuntime();
        var stable = runtime.ActiveProfile;
        var fault = stable with
        {
            Name = "Internal A fault",
            HighVoltageA = stable.HighVoltageA with { RmsA = stable.HighVoltageA.RmsA * 2.5 },
            LowVoltageA = stable.LowVoltageA with { RmsA = 0 }
        };
        runtime.ApplyProfile(fault);

        _ = runtime.Start();
        var snapshot = runtime.Advance(TimeSpan.FromMilliseconds(25));

        Assert.IsNotNull(snapshot.Protection);
        var phaseA = snapshot.Protection!.Differential.Phases.Single(phase => phase.Phase == TransformerPhase.A);
        Assert.IsTrue(phaseA.OperatingCurrentPu > phaseA.ThresholdPu);
        Assert.IsTrue(snapshot.Protection.Differential.Restrained87T.Pickup);
        Assert.IsTrue(snapshot.TripLatched);
    }

    [TestMethod]
    public void NeutralNgrChannel_IsIndependentAndNeverPromotesCalculatedResidual()
    {
        var runtime = new TransformerVirtualInjectionRuntime();
        var profile = runtime.ActiveProfile with
        {
            Name = "HV NGR",
            HighVoltageNeutral = new TransformerVirtualInjectionChannel(0.5, 15, true),
            LowVoltageNeutral = new TransformerVirtualInjectionChannel(0, 0, false)
        };
        runtime.ApplyProfile(profile);

        _ = runtime.Start();
        var snapshot = runtime.Advance(TimeSpan.FromMilliseconds(20));

        Assert.IsNotNull(snapshot.Measurement);
        Assert.IsTrue(snapshot.Measurement!.HighVoltage.NeutralCurrentAvailable);
        Assert.IsFalse(snapshot.Measurement.LowVoltage.NeutralCurrentAvailable);
        Assert.AreEqual(0.5, snapshot.Measurement.HighVoltage.NeutralCurrentA.Magnitude, 1e-6);
        Assert.AreEqual(0.0, snapshot.Measurement.LowVoltage.NeutralCurrentA.Magnitude, 1e-9);
        Assert.IsTrue(runtime.HighVoltageSnapshot.Measurement.Phasors!.NeutralCurrentAvailable);
        Assert.IsFalse(runtime.LowVoltageSnapshot.Measurement.Phasors!.NeutralCurrentAvailable);
    }

    [TestMethod]
    public void StoppedSource_ProjectsZeroWaveform_WithoutEvaluatingProtection()
    {
        var runtime = new TransformerVirtualInjectionRuntime();
        _ = runtime.Start();
        _ = runtime.Advance(TimeSpan.FromMilliseconds(25));

        var stopped = runtime.Stop();

        Assert.IsFalse(runtime.IsRunning);
        Assert.AreEqual(TransformerRuntimeState.WaitingForPair, stopped.State);
        Assert.IsTrue(runtime.HighVoltageSnapshot.Waveform.PhaseA.All(sample => Math.Abs(sample) < 1e-12));
        Assert.IsTrue(runtime.LowVoltageSnapshot.Waveform.PhaseA.All(sample => Math.Abs(sample) < 1e-12));
    }
}
