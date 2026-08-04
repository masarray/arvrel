using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualInjectionRuntimeTests
{
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
        Assert.AreEqual("coherent", runtime.WindowStatus);
    }

    [TestMethod]
    public void ProfileChange_BlocksPickupAndTripUntilNominalCycleIsComplete()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("Normal balanced"),
            initialTimestamp: DateTimeOffset.UnixEpoch);

        Assert.IsTrue(runtime.Apply(VirtualInjectionPresets.Create("A-G fault")));

        var initial = runtime.Advance(TimeSpan.Zero, trustDegraded: false);
        Assert.IsTrue(initial.Frame.Measurement.SmvTrust.AllowsMeasurement);
        Assert.IsFalse(initial.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsFalse(initial.Frame.Measurement.SmvTrust.AllowsTrip);
        Assert.AreEqual("INJECTION_REBUILD", initial.Frame.Measurement.SmvTrust.Code);
        Assert.AreEqual("rebuilding", initial.WindowStatus);

        var incomplete = runtime.Advance(TimeSpan.FromMilliseconds(19), trustDegraded: false);
        Assert.IsFalse(incomplete.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsFalse(incomplete.Frame.Measurement.SmvTrust.AllowsTrip);

        var complete = runtime.Advance(TimeSpan.FromMilliseconds(1), trustDegraded: false);
        Assert.IsTrue(complete.Frame.Measurement.SmvTrust.AllowsMeasurement);
        Assert.IsTrue(complete.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsTrue(complete.Frame.Measurement.SmvTrust.AllowsTrip);
        Assert.AreEqual("coherent", complete.WindowStatus);
    }

    [TestMethod]
    public void SampleCounter_UsesFixedNominalSampleRate()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("Normal balanced", frequencyHz: 60),
            samplesPerCycle: 80,
            nominalFrequencyHz: 50,
            initialTimestamp: DateTimeOffset.UnixEpoch);

        var snapshot = runtime.Advance(TimeSpan.FromMilliseconds(10), trustDegraded: false);

        Assert.AreEqual(4000, runtime.SampleRateHz, 0.001);
        Assert.AreEqual(40L, snapshot.SampleCounter);
        Assert.AreEqual(60, snapshot.Frame.Profile.FrequencyHz, 0.001);
        Assert.AreEqual(50, snapshot.Frame.NominalFrequencyHz, 0.001);
    }

    [TestMethod]
    public void ResetCanRetainOrReplaceActiveProfile()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("A-G fault"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        var faultFingerprint = runtime.InjectionFingerprint;

        runtime.Reset(runtime.ActiveProfile);
        Assert.AreEqual(faultFingerprint, runtime.InjectionFingerprint);
        Assert.AreEqual("A-G fault", runtime.ActiveProfile.Name);

        runtime.Reset(VirtualInjectionPresets.Create("Normal balanced"));
        Assert.AreNotEqual(faultFingerprint, runtime.InjectionFingerprint);
        Assert.AreEqual("Normal balanced", runtime.ActiveProfile.Name);
        Assert.AreEqual(0L, runtime.SampleCounter);
        Assert.AreEqual("coherent", runtime.WindowStatus);
    }

    [TestMethod]
    public void ExternalTrustDegradationOverridesCoherentInjectionPermission()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("A-G fault"),
            initialTimestamp: DateTimeOffset.UnixEpoch);

        var snapshot = runtime.Advance(TimeSpan.Zero, trustDegraded: true);

        Assert.IsTrue(snapshot.Frame.Measurement.SmvTrust.AllowsMeasurement);
        Assert.IsTrue(snapshot.Frame.Measurement.SmvTrust.AllowsPickup);
        Assert.IsFalse(snapshot.Frame.Measurement.SmvTrust.AllowsTrip);
        Assert.AreEqual("SMPCNT_GAP", snapshot.Frame.Measurement.SmvTrust.Code);
    }
}
