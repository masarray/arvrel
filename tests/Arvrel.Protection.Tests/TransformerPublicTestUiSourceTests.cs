using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerPublicTestUiSourceTests
{
    [TestMethod]
    public void P15_ExposesNoSvDeterministicSelfTestAndEvidenceCopy()
    {
        var code = Read("src", "Arvrel.App", "TransformerIedWindow.P15.cs");

        StringAssert.Contains(code, "Public test / deterministic self-test");
        StringAssert.Contains(code, "NO SV REQUIRED");
        StringAssert.Contains(code, "RUN 10-SCENARIO SELF-TEST");
        StringAssert.Contains(code, "VIEW RESULT");
        StringAssert.Contains(code, "COPY EVIDENCE");
        StringAssert.Contains(code, "TransformerPublicSelfTest.RunAll()");
        StringAssert.Contains(code, "Clipboard.SetText");
        StringAssert.Contains(code, "not validate packet capture, calibration, IEC conformance or protection-grade timing");
    }

    [TestMethod]
    public void P15_UiUsesProtectionCoreWithoutReimplementingTransformerAlgorithm()
    {
        var code = Read("src", "Arvrel.App", "TransformerIedWindow.P15.cs");

        Assert.IsFalse(code.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("StandardSlopeThresholdPu", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("MinimumBiasIncreasePu", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("HighVoltageCtSaturationSuspected &&", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("SecondHarmonicThreshold", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P15_InitializesAfterP14AndBeforeNonModalPractitionerInteraction()
    {
        var entryPoint = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(entryPoint, "window.InitializeP14PractitionerUi();");
        StringAssert.Contains(entryPoint, "window.InitializeP15PublicTestUi();");
        StringAssert.Contains(entryPoint, "window.Show();");

        var p14Index = entryPoint.IndexOf("window.InitializeP14PractitionerUi();", StringComparison.Ordinal);
        var p15Index = entryPoint.IndexOf("window.InitializeP15PublicTestUi();", StringComparison.Ordinal);
        var showIndex = entryPoint.IndexOf("window.Show();", StringComparison.Ordinal);
        Assert.IsTrue(p14Index >= 0 && p15Index > p14Index && showIndex > p15Index);
    }

    [TestMethod]
    public void P15_DoesNotRequireLiveOrReplayBeforeOpeningWorkspace()
    {
        var entryPoint = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        Assert.IsFalse(entryPoint.Contains("if (SourceCombo.SelectedIndex == 0)", StringComparison.Ordinal));
        Assert.IsFalse(entryPoint.Contains("Start Live Npcap capture first", StringComparison.Ordinal));
        Assert.IsFalse(entryPoint.Contains("Replay a PCAP/PCAPNG containing at least two", StringComparison.Ordinal));
        StringAssert.Contains(entryPoint, "still requires two distinct HV/LV streams");
    }

    [TestMethod]
    public void P17_SharedOcrPresenterCanInvokeSameP15CoreSelfTestWithoutCreatingAnotherAlgorithm()
    {
        var entryPoint = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");
        var presenter = Read("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayPresenter.cs");

        StringAssert.Contains(entryPoint, "TransformerPublicSelfTest.RunAll()");
        StringAssert.Contains(presenter, "10 DETERMINISTIC CASES");
        Assert.IsFalse(presenter.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
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
