using System.Numerics;
using System.Text;

namespace Arvrel.Protection;

/// <summary>
/// One deterministic transformer-protection self-test result intended for public-beta
/// installation verification. The result is evidence from the same protection engine
/// used by the application; it is not a relay calibration or IEC 60255 type test.
/// </summary>
public sealed record TransformerPublicSelfTestCase(
    string Id,
    string Name,
    bool Passed,
    string Expected,
    string Observed,
    string Detail);

/// <summary>
/// Reproducible transformer public-test report. Scenario stimulus and timing are fixed;
/// ExecutedAt is informational only.
/// </summary>
public sealed record TransformerPublicSelfTestReport(
    string SuiteId,
    DateTimeOffset ExecutedAt,
    IReadOnlyList<TransformerPublicSelfTestCase> Cases)
{
    public int PassedCount => Cases.Count(test => test.Passed);
    public int FailedCount => Cases.Count - PassedCount;
    public bool AllPassed => FailedCount == 0;

    public string ToPlainText()
    {
        var text = new StringBuilder();
        text.AppendLine($"ARVREL transformer self-test · {SuiteId}");
        text.AppendLine($"Executed: {ExecutedAt:O}");
        text.AppendLine($"Result: {(AllPassed ? "PASS" : "FAIL")} · {PassedCount}/{Cases.Count} passed");
        text.AppendLine("Boundary: deterministic software verification only; not calibration, conformance, or protection-grade trip proof.");
        text.AppendLine();
        foreach (var test in Cases)
        {
            text.AppendLine($"{(test.Passed ? "PASS" : "FAIL")} · {test.Id} · {test.Name}");
            text.AppendLine($"  Expected: {test.Expected}");
            text.AppendLine($"  Observed: {test.Observed}");
            if (!string.IsNullOrWhiteSpace(test.Detail))
                text.AppendLine($"  Detail: {test.Detail}");
        }

        return text.ToString().TrimEnd();
    }
}

/// <summary>
/// Public-beta smoke suite for the transformer protection core. It deliberately uses
/// TransformerProtectionEngine rather than a simplified UI-side model so public users
/// can verify the packaged binary against the same authoritative 87T/REF/P13 logic.
/// </summary>
public static class TransformerPublicSelfTest
{
    public const string SuiteId = "transformer-public-beta-v1";

    public static TransformerPublicSelfTestReport RunAll()
    {
        var tests = new List<TransformerPublicSelfTestCase>
        {
            RunCase(
                "87T-THROUGH",
                "Compensated through-current stability",
                "87T remains restrained and virtual trip stays clear.",
                ThroughCurrentStability),
            RunCase(
                "87T-INTERNAL",
                "Internal phase differential operation",
                "Restrained 87T operates and latches a virtual trip.",
                InternalDifferentialOperation),
            RunCase(
                "87T-HS",
                "Unrestrained high-set operation",
                "87T-HS operates above its high-set threshold.",
                HighSetOperation),
            RunCase(
                "H2-BLOCK",
                "Second-harmonic inrush security",
                "20% H2 blocks restrained 87T and no virtual trip latches.",
                SecondHarmonicBlocking),
            RunCase(
                "H5-BLOCK",
                "Fifth-harmonic overexcitation security",
                "40% H5 blocks restrained 87T and no virtual trip latches.",
                FifthHarmonicBlocking),
            RunCase(
                "P13-EXT-SAT",
                "External fault with delayed HV CT saturation",
                "Restraint-leading external fault arms P13; later CT distortion is blocked without trip.",
                ExternalFaultCtSaturationSecurity),
            RunCase(
                "P13-INTERNAL-DIST",
                "Distorted internal fault dependability",
                "CT distortion alone does not arm/block P13; internal 87T still trips.",
                DistortedInternalFaultDependability),
            RunCase(
                "87N-HV",
                "HV restricted earth fault operation",
                "87N-HV operates with an independent neutral-current input.",
                RefHighVoltageOperation),
            RunCase(
                "87N-LV",
                "LV restricted earth fault operation",
                "87N-LV operates independently with an LV neutral-current input.",
                RefLowVoltageOperation),
            RunCase(
                "87N-NO-NEUTRAL",
                "REF neutral-CT security boundary",
                "Enabled REF blocks when an independent neutral-current input is unavailable.",
                RefWithoutNeutralCtBlocks)
        };

        return new TransformerPublicSelfTestReport(SuiteId, DateTimeOffset.UtcNow, tests);
    }

    private static TransformerPublicSelfTestCase RunCase(
        string id,
        string name,
        string expected,
        Func<(bool Passed, string Observed, string Detail)> test)
    {
        try
        {
            var result = test();
            return new TransformerPublicSelfTestCase(id, name, result.Passed, expected, result.Observed, result.Detail);
        }
        catch (Exception ex)
        {
            return new TransformerPublicSelfTestCase(
                id,
                name,
                false,
                expected,
                $"EXCEPTION · {ex.GetType().Name}",
                ex.Message);
        }
    }

    private static (bool, string, string) ThroughCurrentStability()
    {
        var settings = DifferentialSettings(reverseLowVoltage: true);
        var snapshot = Run(
            new TransformerProtectionEngine(settings),
            Winding(Balanced(5.0, 0)),
            Winding(Balanced(4.8, 0)),
            100);
        var maximumIdiff = snapshot.Differential.Phases.Max(phase => phase.OperatingCurrentPu);
        var minimumThreshold = snapshot.Differential.Phases.Min(phase => phase.ThresholdPu);
        var passed = !snapshot.Differential.Restrained87T.Pickup && !snapshot.TripLatched && maximumIdiff < minimumThreshold;
        return (passed,
            $"87T={snapshot.Differential.Restrained87T.State} · max Idiff={maximumIdiff:0.000} pu · min threshold={minimumThreshold:0.000} pu · trip={snapshot.TripLatched}",
            snapshot.DecisionReason);
    }

    private static (bool, string, string) InternalDifferentialOperation()
    {
        var settings = DifferentialSettings(reverseLowVoltage: true);
        var snapshot = Run(
            new TransformerProtectionEngine(settings),
            Winding(SinglePhase(1.2)),
            Winding(SinglePhase(0.1)),
            40);
        var passed = snapshot.Differential.Restrained87T.Operated && snapshot.TripLatched && snapshot.ActiveElement == "87T";
        return (passed,
            $"87T={snapshot.Differential.Restrained87T.State} · active={snapshot.ActiveElement} · trip={snapshot.TripLatched}",
            snapshot.Differential.Phases[0].Reason);
    }

    private static (bool, string, string) HighSetOperation()
    {
        var baseSettings = DifferentialSettings(reverseLowVoltage: false);
        var settings = baseSettings with
        {
            Differential87T = baseSettings.Differential87T with
            {
                HighSetEnabled = true,
                HighSetPickupPu = 5,
                HighSetDelay = TimeSpan.Zero
            }
        };
        var snapshot = Run(
            new TransformerProtectionEngine(settings),
            Winding(SinglePhase(10)),
            Winding(SinglePhase(0)),
            5);
        var passed = snapshot.Differential.HighSet87T.Operated && snapshot.TripLatched && snapshot.ActiveElement == "87T-HS";
        return (passed,
            $"87T-HS={snapshot.Differential.HighSet87T.State} · Iop={snapshot.Differential.HighSet87T.OperatingQuantityPu:0.000} pu · trip={snapshot.TripLatched}",
            snapshot.Differential.HighSet87T.Reason);
    }

    private static (bool, string, string) SecondHarmonicBlocking()
    {
        var settings = DifferentialSettings(reverseLowVoltage: false);
        var highVoltage = Winding(SinglePhase(1.2)) with
        {
            SecondHarmonicRatio = new TransformerHarmonicRatios(0.20, 0, 0)
        };
        var snapshot = Run(
            new TransformerProtectionEngine(settings),
            highVoltage,
            Winding(SinglePhase(0.1)),
            100);
        var phase = snapshot.Differential.Phases[0];
        var passed = phase.HarmonicBlocked && !snapshot.Differential.Restrained87T.Operated && !snapshot.TripLatched;
        return (passed,
            $"H2={phase.SecondHarmonicRatio:P0} · harmonicBlock={phase.HarmonicBlocked} · 87T={snapshot.Differential.Restrained87T.State} · trip={snapshot.TripLatched}",
            phase.Reason);
    }

    private static (bool, string, string) FifthHarmonicBlocking()
    {
        var settings = DifferentialSettings(reverseLowVoltage: false);
        var highVoltage = Winding(SinglePhase(1.2)) with
        {
            FifthHarmonicRatio = new TransformerHarmonicRatios(0.40, 0, 0)
        };
        var snapshot = Run(
            new TransformerProtectionEngine(settings),
            highVoltage,
            Winding(SinglePhase(0.1)),
            100);
        var phase = snapshot.Differential.Phases[0];
        var passed = phase.HarmonicBlocked && !snapshot.Differential.Restrained87T.Operated && !snapshot.TripLatched;
        return (passed,
            $"H5={phase.FifthHarmonicRatio:P0} · harmonicBlock={phase.HarmonicBlocked} · 87T={snapshot.Differential.Restrained87T.State} · trip={snapshot.TripLatched}",
            phase.Reason);
    }

    private static (bool, string, string) ExternalFaultCtSaturationSecurity()
    {
        var engine = new TransformerProtectionEngine(SecuritySettings());
        var start = DateTimeOffset.UnixEpoch;
        _ = engine.Evaluate(Frame(start, 1, 1));
        var armed = engine.Evaluate(Frame(start.AddMilliseconds(5), 6, 6));
        var blocked = engine.Evaluate(Frame(
            start.AddMilliseconds(10),
            3,
            6,
            highVoltageEvidence: SaturatedEvidence()));
        var phase = blocked.Differential.Phases[0];
        var passed = armed.Differential.Phases[0].ExternalFaultArmed &&
                     phase.HighVoltageCtSaturationSuspected &&
                     phase.HighVoltageExternalFaultBlocked &&
                     blocked.ExternalFaultSecurity.AnyBlocked &&
                     !blocked.TripLatched;
        return (passed,
            $"armed={armed.ExternalFaultSecurity.AnyArmed} · HV saturation={phase.HighVoltageCtSaturationSuspected} · securityHold={blocked.ExternalFaultSecurity.AnyBlocked} · trip={blocked.TripLatched}",
            phase.Reason);
    }

    private static (bool, string, string) DistortedInternalFaultDependability()
    {
        var baseSettings = SecuritySettings();
        var settings = baseSettings with
        {
            Differential87T = baseSettings.Differential87T with
            {
                OperateDelay = TimeSpan.FromMilliseconds(10)
            }
        };
        var engine = new TransformerProtectionEngine(settings);
        var start = DateTimeOffset.UnixEpoch;
        _ = engine.Evaluate(Frame(start, 1, 1));
        TransformerProtectionSnapshot? snapshot = null;
        for (var milliseconds = 5; milliseconds <= 20; milliseconds += 5)
        {
            snapshot = engine.Evaluate(Frame(
                start.AddMilliseconds(milliseconds),
                6,
                0,
                highVoltageEvidence: SaturatedEvidence()));
        }

        if (snapshot is null)
            throw new InvalidOperationException("Internal-fault self-test produced no snapshot.");

        var phase = snapshot.Differential.Phases[0];
        var passed = phase.HighVoltageCtSaturationSuspected &&
                     !phase.ExternalFaultArmed &&
                     !phase.ExternalFaultSecurityBlocked &&
                     snapshot.Differential.Restrained87T.Operated &&
                     snapshot.TripLatched;
        return (passed,
            $"HV saturation={phase.HighVoltageCtSaturationSuspected} · armed={phase.ExternalFaultArmed} · blocked={phase.ExternalFaultSecurityBlocked} · 87T={snapshot.Differential.Restrained87T.State} · trip={snapshot.TripLatched}",
            phase.Reason);
    }

    private static (bool, string, string) RefHighVoltageOperation()
    {
        var engine = new TransformerProtectionEngine(RefSettings(highVoltage: true));
        var highVoltage = Winding(SinglePhase(1)) with
        {
            NeutralCurrentA = Polar(1, 0),
            NeutralCurrentAvailable = true
        };
        var snapshot = Run(engine, highVoltage, Winding(Balanced(0.2, 0)), 40);
        var passed = snapshot.RefHighVoltage.Element.Operated && snapshot.TripLatched && snapshot.ActiveElement == "87N-HV";
        return (passed,
            $"87N-HV={snapshot.RefHighVoltage.Element.State} · Iop={snapshot.RefHighVoltage.OperatingCurrentPu:0.000} pu · neutralAvailable={snapshot.RefHighVoltage.NeutralCurrentAvailable} · trip={snapshot.TripLatched}",
            snapshot.RefHighVoltage.Element.Reason);
    }

    private static (bool, string, string) RefLowVoltageOperation()
    {
        var engine = new TransformerProtectionEngine(RefSettings(highVoltage: false));
        var lowVoltage = Winding(SinglePhase(0.8)) with
        {
            NeutralCurrentA = Polar(0.8, 0),
            NeutralCurrentAvailable = true
        };
        var snapshot = Run(engine, Winding(Balanced(0.2, 0)), lowVoltage, 40);
        var passed = snapshot.RefLowVoltage.Element.Operated && snapshot.TripLatched && snapshot.ActiveElement == "87N-LV";
        return (passed,
            $"87N-LV={snapshot.RefLowVoltage.Element.State} · Iop={snapshot.RefLowVoltage.OperatingCurrentPu:0.000} pu · neutralAvailable={snapshot.RefLowVoltage.NeutralCurrentAvailable} · trip={snapshot.TripLatched}",
            snapshot.RefLowVoltage.Element.Reason);
    }

    private static (bool, string, string) RefWithoutNeutralCtBlocks()
    {
        var engine = new TransformerProtectionEngine(RefSettings(highVoltage: true));
        var snapshot = Run(engine, Winding(SinglePhase(1)), Winding(Balanced(0.2, 0)), 40);
        var passed = snapshot.RefHighVoltage.Element.State == ProtectionStageState.Blocked &&
                     !snapshot.RefHighVoltage.NeutralCurrentAvailable &&
                     snapshot.Blocked &&
                     !snapshot.TripLatched;
        return (passed,
            $"87N-HV={snapshot.RefHighVoltage.Element.State} · neutralAvailable={snapshot.RefHighVoltage.NeutralCurrentAvailable} · overallBlocked={snapshot.Blocked} · trip={snapshot.TripLatched}",
            snapshot.RefHighVoltage.Element.Reason);
    }

    private static TransformerProtectionSettings DifferentialSettings(bool reverseLowVoltage) => new()
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
            FifthHarmonicThreshold = 0.35,
            LowVoltageCompensation = new TransformerWindingCompensation
            {
                ReversePolarity = reverseLowVoltage
            }
        }
    };

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
        TransformerProtectionSnapshot? snapshot = null;
        for (var elapsed = 0; elapsed <= durationMs; elapsed += 5)
        {
            snapshot = engine.Evaluate(new TransformerMeasurementFrame(
                DateTimeOffset.UnixEpoch.AddMilliseconds(elapsed),
                highVoltage,
                lowVoltage));
        }

        return snapshot ?? throw new InvalidOperationException("Transformer self-test produced no protection snapshot.");
    }

    private static TransformerMeasurementFrame Frame(
        DateTimeOffset timestamp,
        double highVoltageMagnitude,
        double lowVoltageMagnitude,
        TransformerCtSaturationEvidence? highVoltageEvidence = null,
        TransformerCtSaturationEvidence? lowVoltageEvidence = null,
        double? highVoltageNeutral = null)
    {
        var highVoltage = Winding(SinglePhase(highVoltageMagnitude)) with
        {
            CtSaturationEvidence = highVoltageEvidence ?? TransformerCtSaturationEvidence.None,
            NeutralCurrentAvailable = highVoltageNeutral.HasValue,
            NeutralCurrentA = highVoltageNeutral.HasValue
                ? Polar(Math.Abs(highVoltageNeutral.Value), highVoltageNeutral.Value < 0 ? 180 : 0)
                : Complex.Zero
        };
        var lowVoltage = Winding(SinglePhase(lowVoltageMagnitude)) with
        {
            CtSaturationEvidence = lowVoltageEvidence ?? TransformerCtSaturationEvidence.None
        };
        return new TransformerMeasurementFrame(timestamp, highVoltage, lowVoltage);
    }

    private static TransformerCtSaturationEvidence SaturatedEvidence()
        => new(
            new TransformerCtSaturationPhaseEvidence(1, 0.30, 0.30, 0.35, true, "deterministic self-test saturation evidence"),
            TransformerCtSaturationPhaseEvidence.None,
            TransformerCtSaturationPhaseEvidence.None);

    private static TransformerWindingMeasurement Winding(TransformerPhaseCurrents currents)
        => new(currents, SmvTrustState.Healthy);

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