using System.Globalization;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualInjectionTests
{
    [TestMethod]
    public void BalancedProfile_ReconstructsEnteredRmsAnglesAndPositiveSequence()
    {
        var profile = VirtualInjectionPresets.Create("Normal balanced");
        var frame = VirtualInjectionGenerator.Generate(
            profile,
            DateTimeOffset.UtcNow,
            SmvTrustState.Healthy);
        var phasors = frame.Measurement.Phasors!;

        Assert.AreEqual(1, phasors.PhaseACurrent.Magnitude, 0.001);
        Assert.AreEqual(63.5, phasors.PhaseAVoltage.Magnitude, 0.001);
        Assert.AreEqual(1, phasors.PositiveSequenceCurrent.Magnitude, 0.002);
        Assert.IsTrue(phasors.NegativeSequenceCurrent.Magnitude < 0.002);
        Assert.AreEqual(0, RelativeAngleDegrees(phasors.PhaseAVoltage, phasors.PhaseACurrent), 0.02);
        Assert.AreEqual(-120, RelativeAngleDegrees(phasors.PhaseAVoltage, phasors.PhaseBVoltage), 0.02);
        Assert.AreEqual(120, RelativeAngleDegrees(phasors.PhaseAVoltage, phasors.PhaseCVoltage), 0.02);
    }

    [TestMethod]
    public void OffNominalInjection_UsesFixedNominalSamplingGrid()
    {
        var profile = VirtualInjectionPresets.Create("Normal balanced", frequencyHz: 60);
        var frame = VirtualInjectionGenerator.Generate(
            profile,
            DateTimeOffset.UtcNow,
            SmvTrustState.Healthy,
            samplesPerCycle: 80,
            cycles: 2,
            nominalFrequencyHz: 50);
        var phasors = frame.Measurement.Phasors!;

        Assert.AreEqual(50, frame.NominalFrequencyHz, 0.001);
        Assert.AreEqual(4000, frame.SampleRateHz, 0.001);
        Assert.AreEqual(60, phasors.FrequencyHz, 0.001);
        Assert.IsTrue(
            Math.Abs(phasors.PhaseACurrent.Magnitude - 1) > 0.05,
            "A 60 Hz injection must expose the response of the fixed 50 Hz DFT window rather than silently changing the sampling grid.");
    }

    [TestMethod]
    public void DisabledNeutralChannels_UseCalculatedPhaseSums()
    {
        var profile = VirtualInjectionPresets.Create("A-G fault");
        Assert.IsFalse(profile.ExplicitNeutralCurrent);
        Assert.IsFalse(profile.ExplicitNeutralVoltage);

        var frame = VirtualInjectionGenerator.Generate(
            profile,
            DateTimeOffset.UtcNow,
            SmvTrustState.Healthy);
        var phasors = frame.Measurement.Phasors!;

        Assert.IsFalse(phasors.NeutralCurrentAvailable);
        Assert.IsFalse(phasors.NeutralVoltageAvailable);
        Assert.AreEqual(
            (phasors.PhaseACurrent + phasors.PhaseBCurrent + phasors.PhaseCCurrent).Magnitude,
            phasors.ResidualCurrent.Magnitude,
            0.001);
        Assert.AreEqual("calculated-phase-sum", frame.NeutralCurrentProvenance);
        Assert.AreEqual("calculated-phase-sum", frame.NeutralVoltageProvenance);
    }

    [TestMethod]
    public void ExplicitNeutralChannels_OverrideBalancedPhaseSums()
    {
        var profile = VirtualInjectionPresets.Create("67N forward");
        var frame = VirtualInjectionGenerator.Generate(
            profile,
            DateTimeOffset.UtcNow,
            SmvTrustState.Healthy);
        var phasors = frame.Measurement.Phasors!;

        Assert.IsTrue(phasors.NeutralCurrentAvailable);
        Assert.IsTrue(phasors.NeutralVoltageAvailable);
        Assert.IsTrue((phasors.PhaseACurrent + phasors.PhaseBCurrent + phasors.PhaseCCurrent).Magnitude < 0.002);
        Assert.AreEqual(0.6, phasors.ResidualCurrent.Magnitude, 0.002);
        Assert.AreEqual(15, phasors.ResidualVoltage.Magnitude, 0.002);
        Assert.AreEqual("explicit-virtual-channel", frame.NeutralCurrentProvenance);
        Assert.AreEqual("explicit-virtual-channel", frame.NeutralVoltageProvenance);
    }

    [TestMethod]
    public void InvalidFrequency_IsRejectedBeforeGeneration()
    {
        var profile = VirtualInjectionPresets.Create("Normal balanced") with { FrequencyHz = 75 };

        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            VirtualInjectionGenerator.Generate(profile, DateTimeOffset.UtcNow, SmvTrustState.Healthy));
    }

    [TestMethod]
    public void Fingerprint_IsCultureInvariantAndChangesWithElectricalState()
    {
        var profile = VirtualInjectionPresets.Create("Normal balanced");
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("id-ID");
            var indonesian = profile.Fingerprint();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = profile.Fingerprint();
            var changed = (profile with { PhaseACurrent = new VirtualInjectionChannel(true, 1.1, 0) }).Fingerprint();

            Assert.AreEqual(indonesian, english);
            Assert.AreNotEqual(english, changed);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void VirtualInjection_DrivesProtectionAndTripLatchSurvivesClear()
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
        var timestamp = DateTimeOffset.UtcNow;
        ProtectionSnapshot snapshot = ProtectionSnapshot.Ready(timestamp);
        var fault = VirtualInjectionPresets.Create("A-G fault");

        for (var elapsed = 0; elapsed <= 30; elapsed += 5)
        {
            var frame = VirtualInjectionGenerator.Generate(
                fault,
                timestamp.AddMilliseconds(elapsed),
                SmvTrustState.Healthy).Measurement;
            snapshot = engine.Evaluate(frame);
        }

        Assert.IsTrue(snapshot.Phase50.Operated);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.IsTrue(snapshot.PhaseAPickup);

        var normal = VirtualInjectionGenerator.Generate(
            VirtualInjectionPresets.Create("Normal balanced"),
            timestamp.AddMilliseconds(40),
            SmvTrustState.Healthy).Measurement;
        snapshot = engine.Evaluate(normal);

        Assert.IsFalse(snapshot.Phase50.Pickup);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.AreEqual("50P-1", snapshot.LatchedOperation?.Element);
    }

    [TestMethod]
    public void DirectionalPresets_ProduceForwardOperateAndReverseRestraint()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousEnabled = false,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings
            {
                DirectionalPhase67Enabled = true,
                DirectionalPhase67PickupA = 1.25,
                DirectionalPhase67Delay = TimeSpan.FromMilliseconds(20),
                DirectionalPhase67CharacteristicAngleDeg = 45,
                DirectionalPhase67MinimumPolarizingVoltageV = 5,
                DirectionalPhase67Sense = DirectionalSense.Forward
            }
        };
        var timestamp = DateTimeOffset.UtcNow;

        var forward = Run(
            new ProtectionEngine(settings),
            VirtualInjectionPresets.Create("67P forward"),
            timestamp,
            30);
        var reverse = Run(
            new ProtectionEngine(settings),
            VirtualInjectionPresets.Create("67P reverse"),
            timestamp,
            40);

        Assert.IsTrue(forward.Feeder.DirectionalPhase67.Operated, forward.Feeder.PhaseDirectionalDecision.Reason);
        Assert.IsTrue(forward.TripLatched);
        Assert.IsFalse(reverse.Feeder.DirectionalPhase67.Pickup, reverse.Feeder.PhaseDirectionalDecision.Reason);
        Assert.IsFalse(reverse.TripLatched);
    }

    private static ProtectionSnapshot Run(
        ProtectionEngine engine,
        VirtualInjectionProfile profile,
        DateTimeOffset timestamp,
        int durationMs)
    {
        ProtectionSnapshot snapshot = ProtectionSnapshot.Ready(timestamp);
        for (var elapsed = 0; elapsed <= durationMs; elapsed += 5)
        {
            snapshot = engine.Evaluate(VirtualInjectionGenerator.Generate(
                profile,
                timestamp.AddMilliseconds(elapsed),
                SmvTrustState.Healthy).Measurement);
        }
        return snapshot;
    }

    private static double RelativeAngleDegrees(System.Numerics.Complex reference, System.Numerics.Complex value)
    {
        var angle = (value.Phase - reference.Phase) * 180 / Math.PI;
        while (angle > 180) angle -= 360;
        while (angle <= -180) angle += 360;
        return angle;
    }
}
