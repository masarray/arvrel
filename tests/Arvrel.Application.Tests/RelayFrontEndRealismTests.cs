using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class RelayFrontEndRealismTests
{
    [TestMethod]
    public void InputClippingCanPreventProtectionPickup()
    {
        var source = CreateSourceWithPhaseACurrent(8);
        var profile = new VirtualRelayFrontEndProfile(
            16,
            CurrentFullScaleRms: 3,
            VoltageFullScaleRms: 300,
            AdcSampleRateHz: 4_000,
            MeasurementGroupDelay: TimeSpan.Zero,
            QuantizationEnabled: true,
            ClippingEnabled: true);
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(FastPhase50Settings()),
            frontEndProfile: profile);

        bench.StartInjection();
        var result = bench.Advance(TimeSpan.FromMilliseconds(25));

        Assert.AreEqual(3.0, result.RelayMeasurement.PhaseA, 0.001);
        Assert.IsFalse(result.Protection.Phase50.Pickup, "A clipped 3 A relay input must not satisfy a 4 A pickup even when the injector commands 8 A.");
        Assert.IsFalse(result.Protection.TripLatched);
        Assert.IsTrue(bench.FrontEndSnapshot.ClippedValueCount > 0);
    }

    [TestMethod]
    public void AdcQuantizationProducesDeterministicDiscreteMagnitude()
    {
        var source = CreateSourceWithPhaseACurrent(3.33);
        var profile = new VirtualRelayFrontEndProfile(
            8,
            CurrentFullScaleRms: 8,
            VoltageFullScaleRms: 300,
            AdcSampleRateHz: 4_000,
            MeasurementGroupDelay: TimeSpan.Zero,
            QuantizationEnabled: true,
            ClippingEnabled: true);
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(FastPhase50Settings()),
            frontEndProfile: profile);

        bench.StartInjection();
        var result = bench.Advance(TimeSpan.FromMilliseconds(10));
        var counts = result.RelayMeasurement.PhaseA / profile.CurrentLeastSignificantRms;

        Assert.AreEqual(Math.Round(counts), counts, 1e-9, "Relay-side RMS should lie exactly on the configured ADC magnitude grid.");
        Assert.IsTrue(bench.FrontEndSnapshot.QuantizedValueCount > 0);
    }

    [TestMethod]
    public void SampleHoldAndGroupDelayKeepPreviousMeasurementUntilNewDataArrives()
    {
        var source = CreateSourceWithPhaseACurrent(1);
        var profile = new VirtualRelayFrontEndProfile(
            16,
            CurrentFullScaleRms: 20,
            VoltageFullScaleRms: 300,
            AdcSampleRateHz: 1_000,
            MeasurementGroupDelay: TimeSpan.FromMilliseconds(2),
            QuantizationEnabled: false,
            ClippingEnabled: false);
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(FastPhase50Settings()),
            frontEndProfile: profile)
        {
            AutoStopOnTrip = false
        };

        bench.StartInjection();
        bench.Advance(TimeSpan.FromMilliseconds(20));
        source.ApplyContinuousProfile(RealTestWorkflowEngine.WithSignalRms(
            source.ActiveProfile,
            VirtualInjectionSignal.PhaseACurrent,
            6,
            "front-end step"));

        var beforeRelease = bench.Advance(TimeSpan.FromMilliseconds(1.5));
        Assert.AreEqual(1.0, beforeRelease.RelayMeasurement.PhaseA, 0.05, "The relay should still see the held pre-step value before ADC capture plus group delay has elapsed.");

        var afterRelease = bench.Advance(TimeSpan.FromMilliseconds(2.5));
        Assert.AreEqual(6.0, afterRelease.RelayMeasurement.PhaseA, 0.05, "The delayed acquired value should eventually become authoritative at the relay measurement boundary.");
        Assert.IsTrue(bench.FrontEndSnapshot.CaptureCount > 0);
    }

    [TestMethod]
    public void MeasurementGroupDelayMovesInternalRelayTripLaterThanIdealFrontEnd()
    {
        var idealSource = CreateSourceWithPhaseACurrent(8);
        var delayedSource = CreateSourceWithPhaseACurrent(8);
        var settings = FastPhase50Settings();
        var idealBench = new ClosedLoopVirtualTestBench(
            idealSource,
            new ResearchProtectionEngine(settings),
            frontEndProfile: VirtualRelayFrontEndProfile.Ideal)
        {
            AutoStopOnTrip = false
        };
        var delayedBench = new ClosedLoopVirtualTestBench(
            delayedSource,
            new ResearchProtectionEngine(settings),
            frontEndProfile: new VirtualRelayFrontEndProfile(
                16,
                20,
                300,
                4_000,
                TimeSpan.FromMilliseconds(2),
                QuantizationEnabled: false,
                ClippingEnabled: false))
        {
            AutoStopOnTrip = false
        };

        idealBench.StartInjection();
        delayedBench.StartInjection();
        var ideal = idealBench.Advance(TimeSpan.FromMilliseconds(50));
        var delayed = delayedBench.Advance(TimeSpan.FromMilliseconds(50));

        Assert.IsNotNull(ideal.Protection.LatchedOperation);
        Assert.IsNotNull(delayed.Protection.LatchedOperation);
        var shift = delayed.Protection.LatchedOperation!.TripTimestamp - ideal.Protection.LatchedOperation!.TripTimestamp;
        Assert.IsTrue(shift >= TimeSpan.FromMilliseconds(2), $"Expected at least 2 ms front-end shift, observed {shift.TotalMilliseconds:0.000} ms.");
    }

    [TestMethod]
    public void ContactBounceAndInputDebounceAreVisibleOnlyInExternalTestSetTiming()
    {
        var source = CreateSourceWithPhaseACurrent(8);
        var contact = new VirtualRelayContactProfile(
            PickupOperateDelay: TimeSpan.FromMilliseconds(1),
            TripOperateDelay: TimeSpan.FromMilliseconds(3),
            ReleaseDelay: TimeSpan.FromMilliseconds(1),
            ContactBounceDuration: TimeSpan.FromMilliseconds(1),
            ContactBouncePeriod: TimeSpan.FromMilliseconds(0.25),
            TestSetInputDebounce: TimeSpan.FromMilliseconds(0.5));
        var bench = new ClosedLoopVirtualTestBench(
            source,
            new ResearchProtectionEngine(FastPhase50Settings()),
            contactProfile: contact,
            frontEndProfile: VirtualRelayFrontEndProfile.Ideal);

        bench.StartInjection();
        var result = bench.Advance(TimeSpan.FromMilliseconds(60));

        Assert.IsNotNull(result.Protection.LatchedOperation);
        Assert.IsNotNull(result.TestSet.TripDetectedAt);
        var internalTrip = result.Protection.LatchedOperation!.TripTimestamp;
        var externalTrip = result.TestSet.TripDetectedAt!.Value;
        var externalPath = externalTrip - internalTrip;

        Assert.IsTrue(
            externalPath >= TimeSpan.FromMilliseconds(4.5),
            $"The externally observed trip must include operate delay, bounce settling and BI debounce. Observed {externalPath.TotalMilliseconds:0.000} ms.");
        Assert.IsFalse(source.IsRunning, "Auto-stop still belongs to the debounced TESTSET.BI1 edge.");
    }

    [TestMethod]
    public void NumericalRelayProfileIsDeterministicAndEvidenceAddressable()
    {
        var profile = VirtualRelayFrontEndProfile.NumericalRelayDefault;

        profile.Validate();

        Assert.AreEqual(16, profile.AdcBits);
        Assert.AreEqual(4_000, profile.AdcSampleRateHz);
        Assert.AreEqual(1.5, profile.MeasurementGroupDelay.TotalMilliseconds, 1e-9);
        Assert.AreEqual(64, profile.Fingerprint().Length);
    }

    private static DeterministicLabScenario CreateSourceWithPhaseACurrent(double currentRms)
    {
        var source = new DeterministicLabScenario();
        source.ApplyPreset("Normal balanced");
        source.ApplyContinuousProfile(RealTestWorkflowEngine.WithSignalRms(
            source.ActiveProfile,
            VirtualInjectionSignal.PhaseACurrent,
            currentRms,
            "front-end realism"));
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
