using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayVisualConsistencySourceTests
{
    [TestMethod]
    public void ActiveRelayLamps_ShareOnePhysicalBezelAndGlowGeometry()
    {
        var source = File.ReadAllText(ControlSourcePath("RelayIndicatorLampBehavior.cs"));

        StringAssert.Contains(source, "RelayLampBezelDiameter = 16.0");
        StringAssert.Contains(source, "RelayLampCavityDiameter = 12.4");
        StringAssert.Contains(source, "RelayLampHaloDiameter = 10.8");
        StringAssert.Contains(source, "RelayLampLensDiameter = 9.6");
        StringAssert.Contains(source, "ActiveLampGlowBlurRadius = 12.0");
        StringAssert.Contains(source, "ActiveLampGlowOpacity = 0.74");
        StringAssert.Contains(source, "ConditionalWeakTable<Ellipse, LampVisualParts>");
        StringAssert.Contains(source, "PhysicalEllipse(RelayLampHaloDiameter, TransparentHaloBrush, 19)");
        StringAssert.Contains(source, "PhysicalEllipse(RelayLampBezelDiameter, RelayLampBezelBrush, 20)");
        StringAssert.Contains(source, "Panel.SetZIndex(lens, 22)");
        StringAssert.Contains(source, "parts.Halo.Effect = visual.Glow");
        StringAssert.Contains(source, "lens.Effect = null");
        StringAssert.Contains(source, "\"BlockLed\" => RelayIndicatorState.Orange");

        Assert.IsFalse(source.Contains("RelayLedHealthyStroke", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("RelayLedTripStroke", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("ellipse.Stroke = RelayLampBezelBrush", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RelayGloss_StretchesAcrossFaceWithoutASecondReflectionPatch()
    {
        var gloss = File.ReadAllText(AppSourcePath("MainWindow.RelayFullFaceGloss.cs"));
        var finalLayer = File.ReadAllText(AppSourcePath("MainWindow.RelayFaceplateFinalRefinement.cs"));

        StringAssert.Contains(gloss, "gloss.Height = double.NaN");
        StringAssert.Contains(gloss, "gloss.HorizontalAlignment = HorizontalAlignment.Stretch");
        StringAssert.Contains(gloss, "gloss.VerticalAlignment = VerticalAlignment.Stretch");
        StringAssert.Contains(gloss, "Panel.SetZIndex(gloss, 0)");
        StringAssert.Contains(gloss, "RelayBodyFullFaceGloss");
        StringAssert.Contains(gloss, "gloss.IsHitTestVisible = false");
        StringAssert.Contains(gloss, "ReferenceEquals(border.Background, RelayBodyTopSheen)");

        Assert.IsFalse(gloss.Contains("Geometry.Parse", StringComparison.Ordinal));
        Assert.IsFalse(gloss.Contains("DrawingBrush", StringComparison.Ordinal));
        Assert.IsFalse(finalLayer.Contains("RelayFinalFasciaGloss", StringComparison.Ordinal));
        Assert.IsFalse(finalLayer.Contains("ApplyFinalFullFasciaGloss", StringComparison.Ordinal));
    }

    private static string AppSourcePath(string fileName)
        => SourcePath("src", "Arvrel.App", fileName);

    private static string ControlSourcePath(string fileName)
        => SourcePath("src", "Arvrel.App", "Controls", fileName);

    private static string SourcePath(params string[] segments)
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
