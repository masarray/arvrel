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
