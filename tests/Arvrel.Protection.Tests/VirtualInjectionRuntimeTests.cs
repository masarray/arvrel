using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualInjectionRuntimeTests
{
    [TestMethod]
    public void RuntimeStartsStoppedAndOutputsZero()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("Normal balanced"),
            initialTimestamp: DateTimeOffset.UnixEpoch);

        var snapshot = runtime.Advance(TimeSpan.Zero, trustDegraded: false);
        var phasors = snapshot.Frame.Measurement.Phasors!;

        Assert.IsFalse(runtime.IsRunning);
        Assert.AreEqual("stopped", runtime.OutputState);
        Assert.AreEqual("stopped", runtime.WindowStatus);
        Assert.AreEqual(0, phasors.PhaseAVoltage.Magnitude, 0.0001);
        Assert.AreEqual(0, phasors.PhaseACurrent.Magnitude, 0.0001);
        Assert.AreEqual(0, phasors.ResidualCurrent.Magnitude, 0.0001);
        Assert.AreEqual("Normal balanced", snapshot.ConfiguredProfile.Name);
        StringAssert.Contains(snapshot.Frame.Profile.Name, "output stopped");
    }

    [TestMethod]
    public void InvalidProfile_DoesNotReplaceLastValidInjection()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("Normal balanced"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        var originalFingerprint = runtime.InjectionFingerprint;
        var invalid = VirtualInjectionPresets.Create("A-G fault") with { FrequencyHz = 100 };

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => runtime.Apply(invalid));

        Assert.AreEqual(originalFingerprint, runtime.InjectionFingerprint);
        Assert.AreEqual("Normal balanced", runtime.ActiveProfile.Name);
        Assert.AreEqual("stopped", runtime.WindowStatus);
        Assert.IsFalse(runtime.IsRunning);
    }

    [TestMethod]
    public void ConfiguredValuesRemainZeroUntilStartThenRebuildOneCycle()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("Normal balanced"),
            initialTimestamp: DateTimeOffset.UnixEpoch);

        Assert.IsTrue(runtime.Apply(VirtualInjectionPresets.Create("A-G fault")));
        var armed = runtime.Advance(TimeSpan.Zero, trustDegraded: false);
        Assert.AreEqual(0, armed.Frame.Measurement.Phasors!.PhaseACurrent.Magnitude, 0.0001);
        Assert.IsTrue(armed.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsTrue(armed.Frame.Measurement.SmvTrust.AllowsTrip);

        Assert.IsTrue(runtime.Start());
        var starting = runtime.Advance(TimeSpan.Zero, trustDegraded: false);
        Assert.AreEqual(8.4, starting.Frame.Measurement.Phasors!.PhaseACurrent.Magnitude, 0.002);
        Assert.IsTrue(starting.Frame.Measurement.SmvTrust.AllowsMeasurement);
        Assert.IsFalse(starting.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsFalse(starting.Frame.Measurement.SmvTrust.AllowsTrip);
        Assert.AreEqual("INJECTION_REBUILD", starting.Frame.Measurement.SmvTrust.Code);
        Assert.AreEqual("rebuilding", starting.WindowStatus);

        var complete = runtime.Advance(TimeSpan.FromMilliseconds(20), trustDegraded: false);
        Assert.IsTrue(complete.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsTrue(complete.Frame.Measurement.SmvTrust.AllowsTrip);
        Assert.AreEqual("coherent", complete.WindowStatus);
        Assert.AreEqual("running", complete.OutputState);
    }

    [TestMethod]
    public void ProfileChangeWhileRunning_BlocksPickupAndTripUntilNominalCycleIsComplete()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("Normal balanced"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(20), trustDegraded: false);

        Assert.IsTrue(runtime.Apply(VirtualInjectionPresets.Create("A-G fault")));

        var initial = runtime.Advance(TimeSpan.Zero, trustDegraded: false);
        Assert.IsTrue(initial.Frame.Measurement.SmvTrust.AllowsMeasurement);
        Assert.IsFalse(initial.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsFalse(initial.Frame.Measurement.SmvTrust.AllowsTrip);
        Assert.AreEqual("INJECTION_REBUILD", initial.Frame.Measurement.SmvTrust.Code);

        var incomplete = runtime.Advance(TimeSpan.FromMilliseconds(19), trustDegraded: false);
        Assert.IsFalse(incomplete.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsFalse(incomplete.Frame.Measurement.SmvTrust.AllowsTrip);

        var complete = runtime.Advance(TimeSpan.FromMilliseconds(1), trustDegraded: false);
        Assert.IsTrue(complete.Frame.Measurement.SmvTrust.AllowsMeasurement);
        Assert.IsTrue(complete.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsTrue(complete.Frame.Measurement.SmvTrust.AllowsTrip);
    }

    [TestMethod]
    public void StopForcesZeroWithoutChangingConfiguredProfile()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("A-G fault"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        var configuredFingerprint = runtime.InjectionFingerprint;
        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(20), trustDegraded: false);

        Assert.IsTrue(runtime.Stop());
        var stopped = runtime.Advance(TimeSpan.Zero, trustDegraded: false);

        Assert.AreEqual(configuredFingerprint, runtime.InjectionFingerprint);
        Assert.AreEqual("A-G fault", runtime.ActiveProfile.Name);
        Assert.AreEqual(0, stopped.Frame.Measurement.Phasors!.PhaseACurrent.Magnitude, 0.0001);
        Assert.AreEqual(0, stopped.Frame.Measurement.Phasors.ResidualCurrent.Magnitude, 0.0001);
        Assert.AreEqual("stopped", stopped.OutputState);
    }

    [TestMethod]
    public void SampleCounter_UsesFixedNominalSampleRate()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("Normal balanced", frequencyHz: 60),
            samplesPerCycle: 80,
            nominalFrequencyHz: 50,
            initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();

        var snapshot = runtime.Advance(TimeSpan.FromMilliseconds(10), trustDegraded: false);

        Assert.AreEqual(4000, runtime.SampleRateHz, 0.001);
        Assert.AreEqual(40L, snapshot.SampleCounter);
        Assert.AreEqual(60, snapshot.Frame.Profile.FrequencyHz, 0.001);
        Assert.AreEqual(50, snapshot.Frame.NominalFrequencyHz, 0.001);
    }

    [TestMethod]
    public void ResetCanRetainOrReplaceProfileAndAlwaysStopsOutput()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("A-G fault"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        var faultFingerprint = runtime.InjectionFingerprint;
        runtime.Start();

        runtime.Reset(runtime.ActiveProfile);
        Assert.AreEqual(faultFingerprint, runtime.InjectionFingerprint);
        Assert.AreEqual("A-G fault", runtime.ActiveProfile.Name);
        Assert.IsFalse(runtime.IsRunning);

        runtime.Reset(VirtualInjectionPresets.Create("Normal balanced"));
        Assert.AreNotEqual(faultFingerprint, runtime.InjectionFingerprint);
        Assert.AreEqual("Normal balanced", runtime.ActiveProfile.Name);
        Assert.AreEqual(0L, runtime.SampleCounter);
        Assert.AreEqual("stopped", runtime.WindowStatus);
    }

    [TestMethod]
    public void ExternalTrustDegradationOverridesCoherentInjectionPermission()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("A-G fault"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(20), trustDegraded: false);

        var snapshot = runtime.Advance(TimeSpan.Zero, trustDegraded: true);

        Assert.IsTrue(snapshot.Frame.Measurement.SmvTrust.AllowsMeasurement);
        Assert.IsTrue(snapshot.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsFalse(snapshot.Frame.Measurement.SmvTrust.AllowsTrip);
        Assert.AreEqual("SMPCNT_GAP", snapshot.Frame.Measurement.SmvTrust.Code);
    }

    [TestMethod]
    public void ProtectionTripsOnlyWhenStartedCurrentMeetsPickupAndDelay()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = 4,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(20),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        };
        var engine = new ProtectionEngine(settings);
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("A-G fault"),
            initialTimestamp: DateTimeOffset.UnixEpoch);

        var stopped = engine.Evaluate(runtime.Advance(TimeSpan.Zero, false).Frame.Measurement);
        Assert.IsFalse(stopped.Phase50.Pickup);
        Assert.IsFalse(stopped.TripLatched);

        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(20), false);
        ProtectionSnapshot running = stopped;
        for (var elapsed = 0; elapsed <= 25; elapsed += 5)
            running = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(5), false).Frame.Measurement);

        Assert.IsTrue(running.Phase50.Operated);
        Assert.IsTrue(running.TripLatched);
        Assert.IsTrue(running.PhaseAPickup);
        Assert.AreEqual("50P-1", running.LatchedOperation?.Element);

        runtime.Stop();
        var zero = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(5), false).Frame.Measurement);
        Assert.IsFalse(zero.Phase50.Pickup);
        Assert.IsTrue(zero.TripLatched);
    }
}