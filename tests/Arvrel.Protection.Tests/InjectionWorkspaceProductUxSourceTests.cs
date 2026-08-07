using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class InjectionWorkspaceProductUxSourceTests
{
    [TestMethod]
    public void ProductInjectionUx_SeparatesSourceViewerAndRelayAuthorities()
    {
        var ux = Read("src", "Arvrel.App", "MainWindow.ProductReadyInjectionUx.cs");
        var app = Read("src", "Arvrel.App", "App.xaml.cs");

        StringAssert.Contains(app, "window.InitializeProductReadyInjectionUx()");

        // DPI-safe top chrome: lean, but no longer clipped by the original P0 density pass.
        StringAssert.Contains(ux, "root.RowDefinitions[0].Height = new GridLength(48)");
        StringAssert.Contains(ux, "root.RowDefinitions[1].Height = new GridLength(62)");

        // The injection table is vertically centered with intentional engineering padding.
        StringAssert.Contains(ux, "table.RowHeight = 34");
        StringAssert.Contains(ux, "new Thickness(12, 0, 10, 0)");
        StringAssert.Contains(ux, "table.Columns[5].Header = \"Origin\"");

        // Relay reset is relay equipment authority only. The left side is source + evidence.
        StringAssert.Contains(ux, "RemoveProductButton(_virtualInjectionView, \"Reset relay\")");
        StringAssert.Contains(ux, "RemoveProductButton(waveformFooter, \"Reset\")");

        // Duplicate/noisy quick actions are not allowed to compete with the SOURCE editor.
        StringAssert.Contains(ux, "InjectFaultButton.Visibility = Visibility.Collapsed");
        StringAssert.Contains(ux, "ProductSetButtonText(DegradeSmvButton, \"SMV quality…\")");

        // Internal injection lifecycle belongs to the injection workspace, while external
        // live/replay lifecycle stays in the global source context bar.
        StringAssert.Contains(ux, "_productInjectionToolbar.Children.Add(RunButton)");
        StringAssert.Contains(ux, "_productTopSourceActions.Children.Add(RunButton)");
        StringAssert.Contains(ux, "Start or stop virtual injection. Relay state is not reset.");

        // AUTO APPLY and steady-state status duplication are deliberately quieted.
        StringAssert.Contains(ux, "_analysisWorkspaceMode == AnalysisWorkspaceMode.Injection");
        StringAssert.Contains(ux, "quietSteadyState");
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
