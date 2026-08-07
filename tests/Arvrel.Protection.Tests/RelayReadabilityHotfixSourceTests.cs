using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayReadabilityHotfixSourceTests
{
    [TestMethod]
    public void P01RelayBay_PrioritizesReadableP6WidthAfterP0()
    {
        var hotfix = Read("src", "Arvrel.App", "MainWindow.RelayReadabilityHotfix.cs");
        var app = Read("src", "Arvrel.App", "App.xaml.cs");

        StringAssert.Contains(app, "window.InitializeGlobalUxFoundation()");
        StringAssert.Contains(app, "window.InitializeRelayReadabilityHotfix()");

        StringAssert.Contains(hotfix, "_p0GlobalUxInitialized");
        StringAssert.Contains(hotfix, "SizeChanged += RelayReadabilityHotfix_SizeChanged");
        StringAssert.Contains(hotfix, "new GridLength(compact ? 1.55 : 1.65, GridUnitType.Star)");
        StringAssert.Contains(hotfix, "MinWidth = compact ? 490 : 540");
        StringAssert.Contains(hotfix, "MaxWidth = compact ? 545 : 590");
        StringAssert.Contains(hotfix, "relayBay.Padding = new Thickness(4)");
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
