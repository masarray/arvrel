using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayVisualConsistencySourceTests
{
    [TestMethod]
    public void ActiveRelayLeds_ShareOneBezelAndGlowGeometry()
    {
        var source = File.ReadAllText(SourcePath("MainWindow.RelayLedPresentation.cs"));

        StringAssert.Contains(source, "RelayLedActiveStroke");
        StringAssert.Contains(source, "RelayLedActiveBlurRadius");
        StringAssert.Contains(source, "RelayLedActiveGlowOpacity");
        Assert.AreEqual(4, CountOccurrences(source, "RelayLedActiveStroke, RelayLed"));
        Assert.AreEqual(4, CountOccurrences(source, "RelayLedActiveBlurRadius, RelayLedActiveGlowOpacity"));
        Assert.IsFalse(source.Contains("RelayLedHealthyStroke", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("RelayLedWarningStroke", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("RelayLedTripStroke", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("RelayLedPhaseStroke", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RelayGloss_StretchesAcrossFaceAndStaysBehindContent()
    {
        var source = File.ReadAllText(SourcePath("MainWindow.RelayFullFaceGloss.cs"));

        StringAssert.Contains(source, "gloss.Height = double.NaN");
        StringAssert.Contains(source, "gloss.HorizontalAlignment = HorizontalAlignment.Stretch");
        StringAssert.Contains(source, "gloss.VerticalAlignment = VerticalAlignment.Stretch");
        StringAssert.Contains(source, "Panel.SetZIndex(gloss, -10)");
        StringAssert.Contains(source, "RelayBodyFullFaceGloss");
        StringAssert.Contains(source, "gloss.IsHitTestVisible = false");
        Assert.IsFalse(source.Contains("Height = 62", StringComparison.Ordinal));
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
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
