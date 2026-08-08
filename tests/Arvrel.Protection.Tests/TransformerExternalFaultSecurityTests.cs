using System.Numerics;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerExternalFaultSecurityTests
{
    [TestMethod]
    public void SafeDefaults_KeepExternalFaultSecurityDisabled()
    {
        var settings = new TransformerDifferentialSettings();

        Assert.IsFalse(settings.ExternalFaultSecurity.Enabled);
        Assert.IsTrue(settings.ExternalFaultSecurity.SuperviseHighSet);
        Assert.IsTrue(settings.ExternalFaultSecurity.SuperviseRef);
    }

    [TestMethod]
    public void RestraintLeadingThroughFault_ArmsWithoutBlockingCleanCurrent()
    {
        var engine = new TransformerProtectionEngine(SecuritySettings());
        var timestamp = DateTimeOffset.UtcNow;
        _ = engine.Evaluate(Frame(timestamp, 1, 1));

        var result = engine.Evaluate(Frame(timestamp.AddMilliseconds(5), 6, 6));

        var phase = result.Differential.Phases[0];
        Assert.IsTrue(phase.ExternalFaultArmed, phase.Reason);
        Assert.IsFalse(phase.ExternalFaultSecurityBlocked);
        Assert.IsFalse(result.ExternalFaultSecurity.AnyBlocked);
        Assert.IsFalse(result.TripRequested);
    }

    [TestMethod]
    public void DelayedHvCtSaturation_BlocksSpurious87TDifferentialAndPreventsTrip()
    {
        var engine = new TransformerProtectionEngine(SecuritySettings());
        var timestamp = DateTimeOffset.UtcNow;
        _ = engine.Evaluate(Frame(timestamp, 1, 1));
        _ = engine.Evaluate(Frame(timestamp.AddMilliseconds(5), 6, 6));

        var result = engine.Evaluate(Frame(
            timestamp.AddMilliseconds(10),
            3,
            6,
            highVoltageEvidence: SaturatedEvidence()));

        var phase = result.Differential.Phases[0];
        Assert.IsTrue(phase.HighVoltageCtSaturationSuspected, phase.Reason);
        Assert.IsTrue(phase.HighVoltageExternalFaultBlocked, phase.Reason);
        Assert.IsTrue(result.ExternalFaultSecurity.AnyBlocked);
        Assert.AreEqual(ProtectionStageState.Blocked, result.Differential.Restrained87T.State);
        Assert.IsFalse(result.Differential.Restrained87T.Operated);
        Assert.IsFalse(result.TripRequested);
        Assert.IsFalse(result.TripLatched);
    }

    [TestMethod]
    public void SecurityHold_PersistsWhenInstantaneousDistortionEvidenceClears()
    {
        var engine = new TransformerProtectionEngine(SecuritySettings());
        var timestamp = DateTimeOffset.UtcNow;
        _ = engine.Evaluate(Frame(timestamp, 1, 1));
        _ = engine.Evaluate(Frame(timestamp.AddMilliseconds(5), 6, 6));
        _ = engine.Evaluate(Frame(
            timestamp.AddMilliseconds(10),
            3,
            6,
            highVoltageEvidence: SaturatedEvidence()));

        var held = engine.Evaluate(Frame(timestamp.AddMilliseconds(50), 3, 6));
        var expired = engine.Evaluate(Frame(timestamp.AddMilliseconds(140), 6, 6));

        Assert.IsTrue(held.ExternalFaultSecurity.AnyBlocked);
        Assert.IsTrue(held.Differential.Phases[0].HighVoltageExternalFaultBlocked);
        Assert.IsFalse(expired.ExternalFaultSecurity.AnyBlocked);
        Assert.IsFalse(expired.TripLatched);
    }

    [TestMethod]
    public void DistortedInternalFault_DoesNotArmExternalFaultSecurityAndStillTrips()
    {
        var settings = SecuritySettings() with
        {
            Differential87T = SecuritySettings().Differential87T with
            {
                OperateDelay = TimeSpan.FromMilliseconds(10)
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var timestamp = DateTimeOffset.UtcNow;
        _ = engine.Evaluate(Frame(timestamp, 1, 1));

        TransformerProtectionSnapshot? result = null;
        for (var milliseconds = 5; milliseconds <= 20; milliseconds += 5)
        {
            result = engine.Evaluate(Frame(
                timestamp.AddMilliseconds(milliseconds),
                6,
                0,
                highVoltageEvidence: SaturatedEvidence()));
        }

        Assert.IsNotNull(result);
        var phase = result.Differential.Phases[0];
        Assert.IsTrue(phase.HighVoltageCtSaturationSuspected);
        Assert.IsFalse(phase.ExternalFaultArmed);
        Assert.IsFalse(phase.ExternalFaultSecurityBlocked);
        Assert.IsTrue(result.Differential.Restrained87T.Operated, phase.Reason);
        Assert.IsTrue(result.TripLatched);
    }

    [TestMethod]
    public void HighSet_IsSupervisedByDefaultDuringExternalFaultCtSaturation()
    {
        var baseSettings = SecuritySettings();
        var settings = baseSettings with
        {
            Differential87T = baseSettings.Differential87T with
            {
                HighSetEnabled = true,
                HighSetPickupPu = 2,
                HighSetDelay = TimeSpan.Zero
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var timestamp = DateTimeOffset.UtcNow;
        _ = engine.Evaluate(Frame(timestamp, 1, 1));
        _ = engine.Evaluate(Frame(timestamp.AddMilliseconds(5), 6, 6));

        var result = engine.Evaluate(Frame(
            timestamp.AddMilliseconds(10),
            0,
            6,
            highVoltageEvidence: SaturatedEvidence()));

        Assert.IsTrue(result.ExternalFaultSecurity.AnyBlocked);
        Assert.AreEqual(ProtectionStageState.Blocked, result.Differential.HighSet87T.State);
        Assert.IsFalse(result.Differential.HighSet87T.Operated);
        Assert.IsFalse(result.TripLatched);
    }

    [TestMethod]
    public void HighSet_CanExplicitlyBypassExternalFaultSupervisor()
    {
        var baseSettings = SecuritySettings();
        var settings = baseSettings with
        {
            Differential87T = baseSettings.Differential87T with
            {
                HighSetEnabled = true,
                HighSetPickupPu = 2,
                HighSetDelay = TimeSpan.Zero,
                ExternalFaultSecurity = baseSettings.Differential87T.ExternalFaultSecurity with
                {
                    SuperviseHighSet = false
                }
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var timestamp = DateTimeOffset.UtcNow;
        _ = engine.Evaluate(Frame(timestamp, 1, 1));
        _ = engine.Evaluate(Frame(timestamp.AddMilliseconds(5), 6, 6));

        var result = engine.Evaluate(Frame(
            timestamp.AddMilliseconds(10),
            0,
            6,
            highVoltageEvidence: SaturatedEvidence()));

        Assert.IsTrue(result.ExternalFaultSecurity.AnyBlocked);
        Assert.IsTrue(result.Differential.HighSet87T.Operated);
        Assert.IsTrue(result.TripLatched);
        Assert.AreEqual("87T-HS", result.ActiveElement);
    }

    [TestMethod]
    public void RefHighVoltage_IsSupervisedByHighVoltageCtSaturationEvidence()
    {
        var baseSettings = SecuritySettings();
        var settings = baseSettings with
        {
            Differential87T = baseSettings.Differential87T with { Enabled = false },
            RefHighVoltage = new RestrictedEarthFaultSettings
            {
                Enabled = true,
                MinimumPickupPu = 0.15,
                BiasSlope = 0.20,
                OperateDelay = TimeSpan.Zero
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var timestamp = DateTimeOffset.UtcNow;
        _ = engine.Evaluate(Frame(timestamp, 1, 1, highVoltageNeutral: -1));
        _ = engine.Evaluate(Frame(timestamp.AddMilliseconds(5), 6, 6, highVoltageNeutral: -6));

        var result = engine.Evaluate(Frame(
            timestamp.AddMilliseconds(10),
            3,
            6,
            highVoltageEvidence: SaturatedEvidence(),
            highVoltageNeutral: -6));

        Assert.IsTrue(result.ExternalFaultSecurity.RefHighVoltageBlocked);
        Assert.IsTrue(result.RefHighVoltage.ExternalFaultSecurityBlocked);
        Assert.AreEqual(ProtectionStageState.Blocked, result.RefHighVoltage.Element.State);
        Assert.IsTrue(result.RefHighVoltage.OperatingCurrentPu > result.RefHighVoltage.ThresholdPu);
        Assert.IsFalse(result.RefHighVoltage.Element.Operated);
        Assert.IsFalse(result.TripLatched);
    }

    [TestMethod]
    public void DisabledSecurity_PreservesLegacyDifferentialBehavior()
    {
        var settings = SecuritySettings() with
        {
            Differential87T = SecuritySettings().Differential87T with
            {
                ExternalFaultSecurity = new TransformerExternalFaultSecuritySettings(),
                OperateDelay = TimeSpan.Zero
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var timestamp = DateTimeOffset.UtcNow;
        _ = engine.Evaluate(Frame(timestamp, 1, 1));
        _ = engine.Evaluate(Frame(timestamp.AddMilliseconds(5), 6, 6));

        var result = engine.Evaluate(Frame(
            timestamp.AddMilliseconds(10),
            3,
            6,
            highVoltageEvidence: SaturatedEvidence()));

        Assert.IsFalse(result.ExternalFaultSecurity.Enabled);
        Assert.IsTrue(result.Differential.Restrained87T.Operated);
        Assert.IsTrue(result.TripLatched);
    }

    [TestMethod]
    public void InvalidSevereDistortionThreshold_IsRejected()
    {
        var settings = new TransformerProtectionSettings
        {
            Differential87T = new TransformerDifferentialSettings
            {
                ExternalFaultSecurity = new TransformerExternalFaultSecuritySettings
                {
                    Enabled = true,
                    DistortionRatioThreshold = 0.25,
                    SevereDistortionRatioThreshold = 0.10
                }
            }
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => settings.Validate());
    }

    private static TransformerProtectionSettings SecuritySettings() => new()
    {
        Differential87T = new TransformerDifferentialSettings
        {
            Enabled = true,
            MinimumPickupPu = 0.30,
            Slope1 = 0.25,
            Slope2 = 0.60,
            SlopeBreakpointPu = 2,
            OperateDelay = TimeSpan.FromMilliseconds(20),
            HarmonicSecurityMode = TransformerHarmonicSecurityMode.Disabled,
            LowVoltageCompensation = new TransformerWindingCompensation { ReversePolarity = true },
            ExternalFaultSecurity = new TransformerExternalFaultSecuritySettings
            {
                Enabled = true,
                MinimumBiasPu = 2,
                MinimumBiasIncreasePu = 0.5,
                MaximumInitialDifferentialToBiasRatio = 0.20,
                ArmingDuration = TimeSpan.FromMilliseconds(80),
                SecurityHold = TimeSpan.FromMilliseconds(120),
                DistortionRatioThreshold = 0.12,
                PeakAsymmetryThreshold = 0.08,
                SevereDistortionRatioThreshold = 0.25,
                SuperviseHighSet = true,
                SuperviseRef = true
            }
        }
    };

    private static TransformerMeasurementFrame Frame(
        DateTimeOffset timestamp,
        double highVoltageMagnitude,
        double lowVoltageMagnitude,
        TransformerCtSaturationEvidence? highVoltageEvidence = null,
        TransformerCtSaturationEvidence? lowVoltageEvidence = null,
        double? highVoltageNeutral = null)
    {
        var highVoltage = new TransformerWindingMeasurement(
            SinglePhase(highVoltageMagnitude),
            SmvTrustState.Healthy)
        {
            CtSaturationEvidence = highVoltageEvidence ?? TransformerCtSaturationEvidence.None,
            NeutralCurrentAvailable = highVoltageNeutral.HasValue,
            NeutralCurrentA = highVoltageNeutral.HasValue ? Polar(Math.Abs(highVoltageNeutral.Value), highVoltageNeutral.Value < 0 ? 180 : 0) : Complex.Zero
        };
        var lowVoltage = new TransformerWindingMeasurement(
            SinglePhase(lowVoltageMagnitude),
            SmvTrustState.Healthy)
        {
            CtSaturationEvidence = lowVoltageEvidence ?? TransformerCtSaturationEvidence.None
        };
        return new TransformerMeasurementFrame(timestamp, highVoltage, lowVoltage);
    }

    private static TransformerCtSaturationEvidence SaturatedEvidence()
        => new(
            new TransformerCtSaturationPhaseEvidence(1, 0.30, 0.30, 0.35, true, "synthetic saturation"),
            TransformerCtSaturationPhaseEvidence.None,
            TransformerCtSaturationPhaseEvidence.None);

    private static TransformerPhaseCurrents SinglePhase(double magnitude)
        => new(Polar(magnitude, 0), Complex.Zero, Complex.Zero);

    private static Complex Polar(double magnitude, double angleDegrees)
        => Complex.FromPolarCoordinates(magnitude, angleDegrees * Math.PI / 180);
}
