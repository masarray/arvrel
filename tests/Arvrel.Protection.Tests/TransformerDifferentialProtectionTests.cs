using System.Numerics;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerDifferentialProtectionTests
{
    [TestMethod]
    public void SafeDefaults_KeepAllTransformerTripFunctionsDisabled()
    {
        var settings = TransformerDifferentialIedProfile.CreateSafeDefaults();

        Assert.IsFalse(settings.AnyEnabled);
        Assert.IsFalse(settings.Differential87T.Enabled);
        Assert.IsFalse(settings.Differential87T.HighSetEnabled);
        Assert.IsFalse(settings.RefHighVoltage.Enabled);
        Assert.IsFalse(settings.RefLowVoltage.Enabled);
    }

    [TestMethod]
    public void Differential87T_RemainsStableForCompensatedThroughCurrent()
    {
        var settings = DifferentialSettings() with
        {
            Differential87T = DifferentialSettings().Differential87T with
            {
                LowVoltageCompensation = new TransformerWindingCompensation { ReversePolarity = true }
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(Balanced(5, 0));
        var lowVoltage = Winding(Balanced(4.8, 0));

        var snapshot = Run(engine, highVoltage, lowVoltage, 100);

        Assert.IsFalse(snapshot.Differential.Restrained87T.Pickup);
        Assert.IsFalse(snapshot.TripLatched);
        Assert.IsTrue(snapshot.Differential.Phases.All(phase => phase.OperatingCurrentPu < phase.ThresholdPu));
    }

    [TestMethod]
    public void Differential87T_OperatesForInternalPhaseFault()
    {
        var settings = DifferentialSettings() with
        {
            Differential87T = DifferentialSettings().Differential87T with
            {
                LowVoltageCompensation = new TransformerWindingCompensation { ReversePolarity = true }
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(1.2));
        var lowVoltage = Winding(SinglePhase(0.1));

        var snapshot = Run(engine, highVoltage, lowVoltage, 40);

        Assert.IsTrue(snapshot.Differential.Restrained87T.Operated, snapshot.Differential.Phases[0].Reason);
        Assert.IsTrue(snapshot.TripRequested);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.AreEqual("87T", snapshot.ActiveElement);
    }

    [TestMethod]
    public void Differential87T_SecondHarmonicBlockingRestrainsInrushLikeCurrent()
    {
        var settings = DifferentialSettings();
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(1.2)) with
        {
            SecondHarmonicRatio = new TransformerHarmonicRatios(0.20, 0, 0)
        };
        var lowVoltage = Winding(SinglePhase(0.1));

        var snapshot = Run(engine, highVoltage, lowVoltage, 100);

        Assert.IsTrue(snapshot.Differential.Phases[0].HarmonicBlocked);
        Assert.IsFalse(snapshot.Differential.Restrained87T.Operated);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void Differential87T_FifthHarmonicBlockingRestrainsOverexcitationLikeCurrent()
    {
        var settings = DifferentialSettings();
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(1.2)) with
        {
            FifthHarmonicRatio = new TransformerHarmonicRatios(0.40, 0, 0)
        };
        var lowVoltage = Winding(SinglePhase(0.1));

        var snapshot = Run(engine, highVoltage, lowVoltage, 100);

        Assert.IsTrue(snapshot.Differential.Phases[0].HarmonicBlocked);
        Assert.IsFalse(snapshot.Differential.Restrained87T.Operated);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void Differential87T_HarmonicRestraintRaisesOperateThreshold()
    {
        var baseSettings = DifferentialSettings();
        var settings = baseSettings with
        {
            Differential87T = baseSettings.Differential87T with
            {
                HarmonicSecurityMode = TransformerHarmonicSecurityMode.Restraint,
                SecondHarmonicRestraintGain = 3.0
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(1.2)) with
        {
            SecondHarmonicRatio = new TransformerHarmonicRatios(0.30, 0, 0)
        };
        var lowVoltage = Winding(SinglePhase(0.1));

        var snapshot = Run(engine, highVoltage, lowVoltage, 100);

        Assert.IsFalse(snapshot.Differential.Phases[0].HarmonicBlocked);
        Assert.IsTrue(snapshot.Differential.Phases[0].ThresholdPu > 0.9);
        Assert.IsFalse(snapshot.Differential.Restrained87T.Pickup);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void Differential87THighSet_CanBypassHarmonicBlocking()
    {
        var baseSettings = DifferentialSettings();
        var settings = baseSettings with
        {
            Differential87T = baseSettings.Differential87T with
            {
                HighSetEnabled = true,
                HighSetPickupPu = 5,
                HighSetDelay = TimeSpan.Zero,
                HighSetBypassesHarmonicSecurity = true
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(10)) with
        {
            SecondHarmonicRatio = new TransformerHarmonicRatios(0.50, 0, 0)
        };
        var lowVoltage = Winding(SinglePhase(0));

        var snapshot = Run(engine, highVoltage, lowVoltage, 5);

        Assert.IsTrue(snapshot.Differential.Phases[0].HarmonicBlocked);
        Assert.IsTrue(snapshot.Differential.HighSet87T.Operated);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.AreEqual("87T-HS", snapshot.ActiveElement);
    }

    [TestMethod]
    public void RefHighVoltage_OperatesForInternalGroundFault()
    {
        var settings = RefSettings(highVoltage: true);
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(1)) with
        {
            NeutralCurrentA = Polar(1, 0),
            NeutralCurrentAvailable = true
        };
        var lowVoltage = Winding(Balanced(0.2, 0));

        var snapshot = Run(engine, highVoltage, lowVoltage, 40);

        Assert.IsTrue(snapshot.RefHighVoltage.Element.Operated, snapshot.RefHighVoltage.Element.Reason);
        Assert.AreEqual(2, snapshot.RefHighVoltage.OperatingCurrentPu, 0.001);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.AreEqual("87N-HV", snapshot.ActiveElement);
    }

    [TestMethod]
    public void RefHighVoltage_RestrainsForExternalGroundFaultThroughCurrent()
    {
        var settings = RefSettings(highVoltage: true);
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(1)) with
        {
            NeutralCurrentA = Polar(1, 180),
            NeutralCurrentAvailable = true
        };
        var lowVoltage = Winding(Balanced(0.2, 0));

        var snapshot = Run(engine, highVoltage, lowVoltage, 100);

        Assert.AreEqual(0, snapshot.RefHighVoltage.OperatingCurrentPu, 0.001);
        Assert.IsFalse(snapshot.RefHighVoltage.Element.Pickup);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void RefLowVoltage_IsIndependentFromHighVoltageRef()
    {
        var settings = RefSettings(highVoltage: false);
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(Balanced(0.2, 0));
        var lowVoltage = Winding(SinglePhase(0.8)) with
        {
            NeutralCurrentA = Polar(0.8, 0),
            NeutralCurrentAvailable = true
        };

        var snapshot = Run(engine, highVoltage, lowVoltage, 40);

        Assert.AreEqual(ProtectionStageState.Disabled, snapshot.RefHighVoltage.Element.State);
        Assert.IsTrue(snapshot.RefLowVoltage.Element.Operated, snapshot.RefLowVoltage.Element.Reason);
        Assert.AreEqual("87N-LV", snapshot.ActiveElement);
    }

    [TestMethod]
    public void RefEnabledWithoutIndependentNeutralCt_IsBlockedSecurely()
    {
        var settings = RefSettings(highVoltage: true);
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(1));
        var lowVoltage = Winding(Balanced(0.2, 0));

        var snapshot = Run(engine, highVoltage, lowVoltage, 40);

        Assert.AreEqual(ProtectionStageState.Blocked, snapshot.RefHighVoltage.Element.State);
        Assert.IsTrue(snapshot.Blocked);
        Assert.IsFalse(snapshot.TripRequested);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void TripBlockedTrust_AllowsPickupButPreventsVirtualTrip()
    {
        var settings = DifferentialSettings();
        var blockedTrust = SmvTrustState.TripBlocked("TEST", "Trip permission intentionally removed.");
        var engine = new TransformerProtectionEngine(settings);
        var highVoltage = Winding(SinglePhase(1.2), blockedTrust);
        var lowVoltage = Winding(SinglePhase(0.1), blockedTrust);

        var snapshot = Run(engine, highVoltage, lowVoltage, 40);

        Assert.IsTrue(snapshot.Differential.Restrained87T.Operated);
        Assert.IsTrue(snapshot.Blocked);
        Assert.IsFalse(snapshot.TripRequested);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void SettingsFingerprint_ChangesWhenDifferentialCharacteristicChanges()
    {
        var first = DifferentialSettings();
        var second = first with
        {
            Differential87T = first.Differential87T with { Slope2 = 0.75 }
        };

        Assert.AreNotEqual(first.Fingerprint(), second.Fingerprint());
    }

    [TestMethod]
    public void InvalidDualSlopeCharacteristic_IsRejected()
    {
        var settings = new TransformerProtectionSettings
        {
            Differential87T = new TransformerDifferentialSettings
            {
                Enabled = true,
                Slope1 = 0.8,
                Slope2 = 0.4
            }
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => settings.Validate());
    }

    [TestMethod]
    public void Profile_AdvertisesOnlyCoreTransformerFunctionsAsImplemented()
    {
        var implemented = TransformerDifferentialIedProfile.Functions
            .Where(function => function.Status == TransformerIedFunctionStatus.Implemented)
            .Select(function => function.AnsiCode)
            .ToArray();

        CollectionAssert.AreEquivalent(new[] { "87T", "87T-HS", "87N-HV", "87N-LV" }, implemented);
        Assert.AreEqual("PDIF", TransformerDifferentialIedProfile.Iec61850DifferentialLogicalNode);
    }

    private static TransformerProtectionSettings DifferentialSettings() => new()
    {
        Differential87T = new TransformerDifferentialSettings
        {
            Enabled = true,
            MinimumPickupPu = 0.30,
            Slope1 = 0.25,
            Slope2 = 0.60,
            SlopeBreakpointPu = 2,
            OperateDelay = TimeSpan.FromMilliseconds(20),
            HighSetEnabled = false,
            HarmonicSecurityMode = TransformerHarmonicSecurityMode.Blocking,
            SecondHarmonicThreshold = 0.15,
            FifthHarmonicThreshold = 0.35
        }
    };

    private static TransformerProtectionSettings RefSettings(bool highVoltage) => new()
    {
        RefHighVoltage = new RestrictedEarthFaultSettings
        {
            Enabled = highVoltage,
            MinimumPickupPu = 0.15,
            BiasSlope = 0.20,
            OperateDelay = TimeSpan.FromMilliseconds(20)
        },
        RefLowVoltage = new RestrictedEarthFaultSettings
        {
            Enabled = !highVoltage,
            MinimumPickupPu = 0.15,
            BiasSlope = 0.20,
            OperateDelay = TimeSpan.FromMilliseconds(20)
        }
    };

    private static TransformerProtectionSnapshot Run(
        TransformerProtectionEngine engine,
        TransformerWindingMeasurement highVoltage,
        TransformerWindingMeasurement lowVoltage,
        int durationMs)
    {
        var timestamp = DateTimeOffset.UtcNow;
        TransformerProtectionSnapshot? snapshot = null;
        for (var elapsed = 0; elapsed <= durationMs; elapsed += 5)
        {
            snapshot = engine.Evaluate(new TransformerMeasurementFrame(
                timestamp.AddMilliseconds(elapsed),
                highVoltage,
                lowVoltage));
        }

        return snapshot ?? throw new InvalidOperationException("Transformer test produced no snapshot.");
    }

    private static TransformerWindingMeasurement Winding(
        TransformerPhaseCurrents currents,
        SmvTrustState? trust = null)
        => new(currents, trust ?? SmvTrustState.Healthy);

    private static TransformerPhaseCurrents SinglePhase(double magnitude)
        => new(Polar(magnitude, 0), Complex.Zero, Complex.Zero);

    private static TransformerPhaseCurrents Balanced(double magnitude, double angleDegrees)
        => new(
            Polar(magnitude, angleDegrees),
            Polar(magnitude, angleDegrees - 120),
            Polar(magnitude, angleDegrees + 120));

    private static Complex Polar(double magnitude, double angleDegrees)
        => Complex.FromPolarCoordinates(magnitude, angleDegrees * Math.PI / 180);
}
