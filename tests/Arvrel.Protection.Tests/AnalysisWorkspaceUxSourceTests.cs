using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class AnalysisWorkspaceUxSourceTests
{
    [TestMethod]
    public void ProductHeader_ExposesOnlyWaveformAndInjectionModes()
    {
        var source = File.ReadAllText(SourcePath("MainWindow.PhasorWorkspace.cs"));

        StringAssert.Contains(source, "AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Waveform, \"WAVEFORM\")");
        StringAssert.Contains(source, "AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Injection, \"INJECTION\")");
        Assert.IsFalse(source.Contains("AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Dual", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Phasor", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Phasor_RemainsVisibleWhileLeftPaneSwitchesBySlidingDrawer()
    {
        var source = File.ReadAllText(SourcePath("MainWindow.PhasorWorkspace.cs"));

        StringAssert.Contains(source, "_phasorScope.Visibility = Visibility.Visible");
        StringAssert.Contains(source, "SmvScope.Visibility = Visibility.Visible");
        StringAssert.Contains(source, "SetInjectionDrawerVisible(");
        StringAssert.Contains(source, "new DoubleAnimation");
        StringAssert.Contains(source, "TranslateTransform.XProperty");
        StringAssert.Contains(source, "Panel.SetZIndex(_virtualInjectionView, 4)");
    }

    [TestMethod]
    public void InjectionLifecycle_IsInAnalysisHeaderAndSteadyRunningTextIsHidden()
    {
        var source = File.ReadAllText(SourcePath("MainWindow.ProductReadyInjectionUx.cs"));

        StringAssert.Contains(source, "TryGetProductAnalysisControlsLine");
        StringAssert.Contains(source, "controlsLine.Children.Insert");
        StringAssert.Contains(source, "StreamHealthText.Visibility = Visibility.Collapsed");
        Assert.IsFalse(source.Contains("_productInjectionToolbar.Children.Add(RunButton)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CtState_ReplacesProvenanceNoiseInInjectionFooter()
    {
        var product = File.ReadAllText(SourcePath("MainWindow.ProductReadyInjectionUx.cs"));
        var ct = File.ReadAllText(SourcePath("MainWindow.CtObservability.cs"));

        StringAssert.Contains(product, "_virtualInjectionProvenanceText.Visibility = Visibility.Collapsed");
        StringAssert.Contains(product, "ProductSetButtonText(DegradeSmvButton, \"SMV quality…\")");
        StringAssert.Contains(ct, "InstallCtFooterBadge");
        StringAssert.Contains(ct, "Content = \"CT · IDEAL\"");
        StringAssert.Contains(ct, "Grid.SetColumn(_ctObservabilityButton, 0)");
        Assert.IsFalse(ct.Contains("controlsLine.Children.Add(_ctObservabilityButton)", StringComparison.Ordinal));
    }

    private static string SourcePath(string fileName)
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
                var candidate = Path.Combine(current.FullName, "src", "Arvrel.App", fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException($"Unable to locate src/Arvrel.App/{fileName} from the test workspace.");
    }
}