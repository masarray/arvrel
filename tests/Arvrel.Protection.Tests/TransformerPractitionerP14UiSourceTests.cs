using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerPractitionerP14UiSourceTests
{
    [TestMethod]
    public void P14_ExposesExternalFaultAndCtSecurityConfiguration()
    {
        var code = Read("src", "Arvrel.App", "TransformerIedWindow.P14.cs");

        StringAssert.Contains(code, "External-fault / CT security");
        StringAssert.Contains(code, "Enable P13");
        StringAssert.Contains(code, "MIN IBIAS (pu)");
        StringAssert.Contains(code, "MIN ΔIBIAS (pu)");
        StringAssert.Contains(code, "MAX INITIAL IDIFF/IBIAS (%)");
        StringAssert.Contains(code, "ARM WINDOW (ms)");
        StringAssert.Contains(code, "SECURITY HOLD (ms)");
        StringAssert.Contains(code, "DISTORTION (%)");
        StringAssert.Contains(code, "PEAK ASYMMETRY (%)");
        StringAssert.Contains(code, "SEVERE DISTORTION (%)");
        StringAssert.Contains(code, "Supervise 87T-HS");
        StringAssert.Contains(code, "Supervise REF");
    }

    [TestMethod]
    public void P14_InitializesBeforePractitionerInteractionWithoutAddingAnotherWindowLifecycleOverride()
    {
        var code = Read("src", "Arvrel.App", "TransformerIedWindow.P14.cs");
        var entryPoint = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(code, "internal void InitializeP14PractitionerUi()");
        StringAssert.Contains(entryPoint, "var window = new TransformerIedWindow(_processBus) { Owner = this };");
        StringAssert.Contains(entryPoint, "window.InitializeP14PractitionerUi();");
        StringAssert.Contains(entryPoint, "window.Show();");
        Assert.IsFalse(code.Contains("OnContentRendered(", StringComparison.Ordinal),
            "P14 must not add a second TransformerIedWindow.OnContentRendered override.");

        var constructIndex = entryPoint.IndexOf("new TransformerIedWindow", StringComparison.Ordinal);
        var initializeIndex = entryPoint.IndexOf("window.InitializeP14PractitionerUi();", StringComparison.Ordinal);
        var showIndex = entryPoint.IndexOf("window.Show();", StringComparison.Ordinal);
        Assert.IsTrue(constructIndex >= 0 && initializeIndex > constructIndex && showIndex > initializeIndex,
            "P14 must initialize after the window constructor and before practitioner interaction begins.");
    }

    [TestMethod]
    public void P14_AppliesP13SettingsBeforeFirstRuntimeEvaluation()
    {
        var code = Read("src", "Arvrel.App", "TransformerIedWindow.P14.cs");

        StringAssert.Contains(code, "ApplyRuntimeButton.Click -= ApplyRuntime_Click;");
        StringAssert.Contains(code, "ApplyRuntimeButton.Click += ApplyRuntimeWithP14_Click;");
        StringAssert.Contains(code, "var externalFaultSecurity = ReadP14SecuritySettings();");
        StringAssert.Contains(code, "ExternalFaultSecurity = externalFaultSecurity");
        StringAssert.Contains(code, "var configuration = baseConfiguration with { ProtectionSettings = protectionSettings };");

        var settingsIndex = code.IndexOf("var externalFaultSecurity = ReadP14SecuritySettings();", StringComparison.Ordinal);
        var runtimeIndex = code.IndexOf("new TransformerProcessBusProtectionRuntime", StringComparison.Ordinal);
        var evaluateIndex = code.IndexOf("_runtime.EvaluateCurrent()", StringComparison.Ordinal);
        Assert.IsTrue(settingsIndex >= 0 && runtimeIndex > settingsIndex && evaluateIndex > runtimeIndex,
            "P13 settings must be bound before the first runtime evaluation.");
    }

    [TestMethod]
    public void P14_RendersP13EvidenceWithoutDuplicatingProtectionLogic()
    {
        var code = Read("src", "Arvrel.App", "TransformerIedWindow.P14.cs");

        StringAssert.Contains(code, "snapshot.Protection.ExternalFaultSecurity");
        StringAssert.Contains(code, "CtSaturationEvidence");
        StringAssert.Contains(code, "HighVoltageDistortionRatio");
        StringAssert.Contains(code, "LowVoltageDistortionRatio");
        StringAssert.Contains(code, "HighVoltagePeakAsymmetry");
        StringAssert.Contains(code, "LowVoltagePeakAsymmetry");
        StringAssert.Contains(code, "EXT FAULT ARMED");
        StringAssert.Contains(code, "CT DISTORTION · NO BLOCK");
        StringAssert.Contains(code, "SECURITY HOLD ACTIVE");
        StringAssert.Contains(code, "CT SAT HV");

        Assert.IsFalse(code.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("IsCtSaturationSuspected", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("MinimumBiasIncreasePu &&", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P14_PreservesDistortionVersusBlockDistinction()
    {
        var code = Read("src", "Arvrel.App", "TransformerIedWindow.P14.cs");

        StringAssert.Contains(code, "Waveform distortion alone never blocks protection");
        StringAssert.Contains(code, "Restraint-leading external-fault context is required before CT distortion can assert a security hold.");
        StringAssert.Contains(code, "security.AnyBlocked");
        StringAssert.Contains(code, "security.AnyArmed");
        StringAssert.Contains(code, "HighVoltageSaturationSuspected");
        StringAssert.Contains(code, "LowVoltageSaturationSuspected");
        StringAssert.Contains(code, "HOLD ACTIVE");
        StringAssert.Contains(code, "HOLD clear");
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
