using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualInjectionRunStopProtectionTests
{
    [TestMethod]
    public void PickupCurrentWithNumericalHeadroom_TripsAfterConfiguredDelayOnlyWhileStarted()
    {
        const double pickupA = 4.0;
        const double injectedA = pickupA + 0.01;
        var profile = VirtualInjectionPresets.Create("A-G fault") with
        {
            // The virtual source is reconstructed through platform math routines before
            // the phasor estimator evaluates it. Keep a small, explicit engineering
            // headroom so this run/stop test does not accidentally become a test of
            // platform-specific transcendental rounding at an exact binary boundary.
            PhaseACurrent = new VirtualInjectionChannel(true, injectedA, 45)
        };
        var runtime = new VirtualInjectionRuntime(
            profile,
            initialTimestamp: DateTimeOffset.UnixEpoch);
        var engine = CreatePhaseInstantaneousEngine(pickupA);

        var stopped = engine.Evaluate(runtime.Advance(TimeSpan.Zero, false).Frame.Measurement);
        Assert.AreEqual(0, stopped.Phase50.OperatingQuantity, 0.0001);
        Assert.IsFalse(stopped.Phase50.Pickup);
        Assert.IsFalse(stopped.TripLatched);

        runtime.Start();
        ProtectionSnapshot firstAllowedPickup = stopped;
        for (var elapsed = 0; elapsed < 20; elapsed += 5)
            firstAllowedPickup = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(5), false).Frame.Measurement);

        Assert.AreEqual(injectedA, firstAllowedPickup.Phase50.OperatingQuantity, 0.002);
        Assert.IsTrue(firstAllowedPickup.Phase50.Pickup);
        Assert.IsFalse(firstAllowedPickup.Phase50.Operated);
        Assert.IsFalse(firstAllowedPickup.TripLatched);

        var beforeDelay = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(10), false).Frame.Measurement);
        Assert.IsTrue(beforeDelay.Phase50.Pickup);
        Assert.IsFalse(beforeDelay.Phase50.Operated);
        Assert.IsFalse(beforeDelay.TripLatched);

        var afterDelay = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(15), false).Frame.Measurement);
        Assert.IsTrue(afterDelay.Phase50.Operated);
        Assert.IsTrue(afterDelay.TripLatched);
        Assert.AreEqual("50P-1", afterDelay.LatchedOperation?.Element);

        runtime.Stop();
        var stoppedAgain = engine.Evaluate(runtime.Advance(TimeSpan.FromMilliseconds(5), false).Frame.Measurement);
        Assert.AreEqual(0, stoppedAgain.Phase50.OperatingQuantity, 0.0001);
        Assert.IsFalse(stoppedAgain.Phase50.Pickup);
        Assert.IsTrue(stoppedAgain.TripLatched);
    }

    [TestMethod]
    public void RelayReset_ClearsRelayOnly_WhileRunningSourceRemainsEnergizedAndCanRetrip()
    {
        const double pickupA = 4.0;
        var profile = VirtualInjectionPresets.Create("A-G fault") with
        {
            PhaseACurrent = new VirtualInjectionChannel(true, 8.4, 45)
        };
        var runtime = new VirtualInjectionRuntime(
            profile,
            initialTimestamp: DateTimeOffset.UnixEpoch);
        var engine = CreatePhaseInstantaneousEngine(pickupA);

        Assert.IsTrue(runtime.Start());
        var firstTrip = EvaluateUntilTrip(runtime, engine);
        Assert.IsTrue(firstTrip.TripLatched);
        Assert.AreEqual("50P-1", firstTrip.LatchedOperation?.Element);

        var configuredProfileBeforeReset = runtime.ActiveProfile;
        var outputProfileBeforeReset = runtime.OutputProfile;
        var configuredFingerprintBeforeReset = runtime.InjectionFingerprint;
        var outputFingerprintBeforeReset = runtime.OutputFingerprint;
        var outputStateBeforeReset = runtime.OutputState;

        engine.Reset();

        Assert.IsTrue(runtime.IsRunning, "Resetting relay equipment must not stop the virtual test source.");
        Assert.AreEqual(configuredProfileBeforeReset, runtime.ActiveProfile);
        Assert.AreEqual(outputProfileBeforeReset, runtime.OutputProfile);
        Assert.AreEqual(configuredFingerprintBeforeReset, runtime.InjectionFingerprint);
        Assert.AreEqual(outputFingerprintBeforeReset, runtime.OutputFingerprint);
        Assert.AreEqual(outputStateBeforeReset, runtime.OutputState);

        var immediatelyAfterReset = engine.Evaluate(
            runtime.Advance(TimeSpan.Zero, false).Frame.Measurement);
        Assert.IsTrue(immediatelyAfterReset.Phase50.Pickup);
        Assert.IsFalse(immediatelyAfterReset.Phase50.Operated);
        Assert.IsFalse(immediatelyAfterReset.TripLatched);
        Assert.AreEqual(8.4, immediatelyAfterReset.Phase50.OperatingQuantity, 0.002);

        var secondTrip = EvaluateUntilTrip(runtime, engine);
        Assert.IsTrue(secondTrip.TripLatched,
            "A fault that remains injected must be able to operate the reset relay again after its configured delay.");
        Assert.AreEqual("50P-1", secondTrip.LatchedOperation?.Element);
        Assert.IsTrue(runtime.IsRunning);
        Assert.AreEqual(configuredFingerprintBeforeReset, runtime.InjectionFingerprint);
        Assert.AreEqual(outputFingerprintBeforeReset, runtime.OutputFingerprint);
    }

    private static ProtectionEngine CreatePhaseInstantaneousEngine(double pickupA)
        => new(new ProtectionSettings
        {
            PhaseInstantaneousEnabled = true,
            PhaseInstantaneousPickupA = pickupA,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(20),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        });

    private static ProtectionSnapshot EvaluateUntilTrip(
        VirtualInjectionRuntime runtime,
        ProtectionEngine engine)
    {
        var snapshot = engine.Evaluate(runtime.Advance(TimeSpan.Zero, false).Frame.Measurement);
        for (var elapsed = 0; elapsed < 100 && !snapshot.TripLatched; elapsed += 5)
        {
            snapshot = engine.Evaluate(
                runtime.Advance(TimeSpan.FromMilliseconds(5), false).Frame.Measurement);
        }
        return snapshot;
    }
}
