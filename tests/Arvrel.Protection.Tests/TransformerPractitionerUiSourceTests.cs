using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerPractitionerUiSourceTests
{
    [TestMethod]
    public void PractitionerWorkspace_ExposesDualStreamEngineeringAndStandardSlopeControls()
    {
        var xaml = Read("src", "Arvrel.App", "TransformerIedWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"HvStreamCombo\"");
        StringAssert.Contains(xaml, "x:Name=\"LvStreamCombo\"");
        StringAssert.Contains(xaml, "RATED POWER (MVA)");
        StringAssert.Contains(xaml, "VECTOR GROUP");
        StringAssert.Contains(xaml, "HV phase");
        StringAssert.Contains(xaml, "LV phase");
        StringAssert.Contains(xaml, "Is1 (pu)");
        StringAssert.Contains(xaml, "K1 (%)");
        StringAssert.Contains(xaml, "Is2 (pu)");
        StringAssert.Contains(xaml, "K2 (%)");
        StringAssert.Contains(xaml, "H2 THRESHOLD (%)");
        StringAssert.Contains(xaml, "H5 THRESHOLD (%)");
        StringAssert.Contains(xaml, "REF HV");
        StringAssert.Contains(xaml, "REF LV");
        StringAssert.Contains(xaml, "Virtual protection only · no physical trip output");
    }

    [TestMethod]
    public void CharacteristicView_UsesProtectionModelsStandardSlopeInsteadOfDuplicatingAlgorithm()
    {
        var scope = Read("src", "Arvrel.App", "Controls", "TransformerDifferentialCharacteristicScope.cs");

        StringAssert.Contains(scope, "settings.StandardSlopeThresholdPu(bias)");
        StringAssert.Contains(scope, "TransformerDifferentialPhaseSnapshot");
        StringAssert.Contains(scope, "phase.RestraintCurrentPu");
        StringAssert.Contains(scope, "phase.OperatingCurrentPu");
        Assert.IsFalse(scope.Contains("Slope1 *", StringComparison.Ordinal));
        Assert.IsFalse(scope.Contains("Slope2 *", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RuntimeWorkspace_ReusesP11ExecutionAndEvidenceBoundaries()
    {
        var codeBehind = Read("src", "Arvrel.App", "TransformerIedWindow.xaml.cs");
        var entryPoint = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(codeBehind, "new TransformerProcessBusProtectionRuntime(_controller, configuration)");
        StringAssert.Contains(codeBehind, "_runtime.EvaluateCurrent()");
        StringAssert.Contains(codeBehind, "_runtime.Reset()");
        StringAssert.Contains(codeBehind, "_runtime.CaptureEvidence()");
        StringAssert.Contains(codeBehind, "snapshot.EffectiveSettingsFingerprint");
        StringAssert.Contains(codeBehind, "snapshot.Pairing");
        StringAssert.Contains(codeBehind, "snapshot.Measurement.HighVoltage.SecondHarmonicRatio");
        StringAssert.Contains(codeBehind, "protection.RefHighVoltage");
        StringAssert.Contains(codeBehind, "protection.RefLowVoltage");

        StringAssert.Contains(entryPoint, "new TransformerIedWindow(_processBus)");
        StringAssert.Contains(entryPoint, "Transformer differential requires two independent HV/LV Sampled Values streams");
        Assert.IsFalse(codeBehind.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PractitionerWorkspace_ShowsWhyTheRelayActsNotOnlyFinalTripState()
    {
        var xaml = Read("src", "Arvrel.App", "TransformerIedWindow.xaml");
        var codeBehind = Read("src", "Arvrel.App", "TransformerIedWindow.xaml.cs");

        StringAssert.Contains(xaml, "Idiff pu");
        StringAssert.Contains(xaml, "Ibias pu");
        StringAssert.Contains(xaml, "Threshold");
        StringAssert.Contains(xaml, "SV pairing / trust");
        StringAssert.Contains(xaml, "smpCnt");
        StringAssert.Contains(xaml, "smpSynch");
        StringAssert.Contains(xaml, "Engineering evidence");

        StringAssert.Contains(codeBehind, "phase.HarmonicBlocked");
        StringAssert.Contains(codeBehind, "phase.RestrainedPickup");
        StringAssert.Contains(codeBehind, "phase.RestrainedOperated");
        StringAssert.Contains(codeBehind, "pairing.PhaseCorrectionDegrees");
        StringAssert.Contains(codeBehind, "plan.LowVoltageCompensation.PhaseShiftDegrees");
    }

    private static string Read(params string[] segments)
        => File.ReadAllText(Locate(segments));

    private static string Locate(params string[] segments)
    {
        var starts = new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory)
        };

        foreach (var start in starts)
        {
            for (var current = start; current is not null; current = current.Parent)
            {
                var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException($"Unable to locate {Path.Combine(segments)} from the test workspace.");
    }
}
