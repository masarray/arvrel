using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerOcrWorkspaceReuseP17SourceTests
{
    [TestMethod]
    public void TransformerSelection_ReusesCompleteOcrOperatorWorkspace()
    {
        var main = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");
        var reuse = Read("src", "Arvrel.App", "MainWindow.TransformerOcrWorkspace.cs");

        StringAssert.Contains(main, "_protectionWorkspace.Visibility = Visibility.Visible;");
        StringAssert.Contains(main, "MountTransformerFaceplateIntoOcrWorkspace()");
        StringAssert.Contains(main, "OperatingModeCombo.Visibility = Visibility.Visible;");
        StringAssert.Contains(main, "OCR waveform / injection / phasor workspace is reused");

        StringAssert.Contains(reuse, "MultiIedVisualDescendants<VirtualRelayControl>(_protectionWorkspace)");
        StringAssert.Contains(reuse, "_transformerSharedRelayHost.Child = _transformerFaceplate;");
        StringAssert.Contains(reuse, "_protectionWorkspace.Visibility = Visibility.Visible;");
        StringAssert.Contains(reuse, "waveform, injection, phasor, source toolbar and lower");
    }

    [TestMethod]
    public void TransformerWorkspace_ProjectsAuthoritative87TElementsIntoExistingOperationPanel()
    {
        var reuse = Read("src", "Arvrel.App", "MainWindow.TransformerOcrWorkspace.cs");

        StringAssert.Contains(reuse, "var snapshot = _transformerLastSnapshot;");
        StringAssert.Contains(reuse, "var protection = snapshot?.Protection;");
        StringAssert.Contains(reuse, "Phase50SettingText.Text = $\"87T · Is1");
        StringAssert.Contains(reuse, "Phase51SettingText.Text = $\"87T-HS");
        StringAssert.Contains(reuse, "Earth50SettingText.Text = $\"REF HV");
        StringAssert.Contains(reuse, "Earth51SettingText.Text = $\"REF LV");
        StringAssert.Contains(reuse, "protection?.Differential.Restrained87T");
        StringAssert.Contains(reuse, "protection?.Differential.HighSet87T");
        StringAssert.Contains(reuse, "protection?.RefHighVoltage.Element");
        StringAssert.Contains(reuse, "protection?.RefLowVoltage.Element");

        Assert.IsFalse(reuse.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(reuse.Contains("StandardSlopeThresholdPu", StringComparison.Ordinal));
        Assert.IsFalse(reuse.Contains("MinimumBiasIncreasePu", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TransformerProjection_RunsAfterExistingOcrWaveformTimerAndRestoresOcrRelay()
    {
        var reuse = Read("src", "Arvrel.App", "MainWindow.TransformerOcrWorkspace.cs");

        StringAssert.Contains(reuse, "_timer.Tick += TransformerOcrWorkspace_Tick;");
        StringAssert.Contains(reuse, "RenderTransformerOperationWorkspace();");
        StringAssert.Contains(reuse, "_transformerSharedRelayHost.Child = _ocrWorkspaceRelay;");
        StringAssert.Contains(reuse, "UpdateSettingSummaries();");
        StringAssert.Contains(reuse, "RenderInitialFrame();");
        StringAssert.Contains(reuse, "RenderSelectedProcessBusStream();");
        StringAssert.Contains(reuse, "RefreshRelayAnnunciation();");
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
