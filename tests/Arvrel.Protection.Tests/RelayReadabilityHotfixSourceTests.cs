using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayReadabilityHotfixSourceTests
{
    [TestMethod]
    public void P02RelayBay_TracksWorkspaceHeightAndAllowsP6Upscale()
    {
        var hotfix = Read("src", "Arvrel.App", "MainWindow.RelayReadabilityHotfix.cs");
        var relay = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml.cs");
        var app = Read("src", "Arvrel.App", "App.xaml.cs");

        StringAssert.Contains(app, "window.InitializeGlobalUxFoundation()");
        StringAssert.Contains(app, "window.InitializeRelayReadabilityHotfix()");

        StringAssert.Contains(hotfix, "RelayNativeViewboxWidth = 576.0");
        StringAssert.Contains(hotfix, "RelayNativeViewboxHeight = 726.0");
        StringAssert.Contains(hotfix, "_relayReadabilityWorkspace.SizeChanged += RelayReadabilityWorkspace_SizeChanged");
        StringAssert.Contains(hotfix, "DispatcherPriority.Render");
        StringAssert.Contains(hotfix, "workspaceHeight - 4");
        StringAssert.Contains(hotfix, "new GridLength(relayWidth, GridUnitType.Pixel)");
        StringAssert.Contains(hotfix, "MaxWidth = double.PositiveInfinity");
        StringAssert.Contains(hotfix, "relayBay.Padding = new Thickness(2)");

        StringAssert.Contains(relay, "scaler.Stretch = Stretch.Uniform");
        StringAssert.Contains(relay, "scaler.StretchDirection = StretchDirection.Both");
        StringAssert.Contains(relay, "StatusLedLabels");
        StringAssert.Contains(relay, "Math.Max(textBlock.FontSize, 11.6)");
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
