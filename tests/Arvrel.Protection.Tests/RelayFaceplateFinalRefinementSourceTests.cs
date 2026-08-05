using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayFaceplateFinalRefinementSourceTests
{
    [TestMethod]
    public void Fascia_UsesOneContinuousEdgeMatchedClearCoat()
    {
        var gloss = File.ReadAllText(AppSourcePath("MainWindow.RelayFullFaceGloss.cs"));
        var finalLayer = File.ReadAllText(AppSourcePath("MainWindow.RelayFaceplateFinalRefinement.cs"));

        StringAssert.Contains(gloss, "RelayBodyDepthBackground");
        StringAssert.Contains(gloss, "RelayBodyDepthBorder");
        StringAssert.Contains(gloss, "Grid.SetRowSpan(gloss, Math.Max(1, bodyGrid.RowDefinitions.Count))");
        StringAssert.Contains(gloss, "Grid.SetColumnSpan(gloss, Math.Max(1, bodyGrid.ColumnDefinitions.Count))");
        StringAssert.Contains(gloss, "gloss.Margin = new Thickness(1.6)");
        StringAssert.Contains(gloss, "gloss.CornerRadius = new CornerRadius(9.2)");
        StringAssert.Contains(gloss, "FirstOrDefault(border => ReferenceEquals(border.Background, RelayBodyTopSheen))");
        StringAssert.Contains(gloss, "Panel.SetZIndex(gloss, -10)");

        Assert.IsFalse(gloss.Contains("Geometry.Parse", StringComparison.Ordinal));
        Assert.IsFalse(gloss.Contains("DrawingBrush", StringComparison.Ordinal));
        Assert.IsFalse(finalLayer.Contains("ApplyFinalFullFasciaGloss", StringComparison.Ordinal));
        Assert.IsFalse(finalLayer.Contains("RelayFinalFasciaGloss", StringComparison.Ordinal));
        Assert.IsFalse(finalLayer.Contains("Geometry.Parse", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FinalButtonTemplates_RemoveBlueNoiseAndUseSubPixelTravel()
    {
        var source = File.ReadAllText(AppSourcePath("MainWindow.RelayFaceplateFinalRefinement.cs"));
        var templates = ExtractRawTemplates(source).ToArray();

        Assert.AreEqual(2, templates.Length);
        foreach (var template in templates)
        {
            _ = XDocument.Parse(template);
            StringAssert.Contains(template, "TranslateTransform Y=\"0.5\"");
            StringAssert.Contains(template, "ShadowDepth=\"0.8\"");
            StringAssert.Contains(template, "Property=\"Margin\" Value=\"0,0.5,0,2\"");
            Assert.IsFalse(template.Contains("IsKeyboardFocused", StringComparison.Ordinal));
            Assert.IsFalse(template.Contains("#6EA9CF", StringComparison.Ordinal));
            Assert.IsFalse(template.Contains("#77B7DE", StringComparison.Ordinal));
            Assert.IsFalse(template.Contains("TranslateTransform Y=\"1\"", StringComparison.Ordinal));
            Assert.IsFalse(template.Contains("TranslateTransform Y=\"3\"", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void FaceplateLamps_UseSeparateBezelHaloAndLensLayers()
    {
        var behavior = File.ReadAllText(ControlSourcePath("RelayIndicatorLampBehavior.cs"));
        var presentation = File.ReadAllText(AppSourcePath("MainWindow.RelayLedPresentation.cs"));
        var annunciation = File.ReadAllText(AppSourcePath("MainWindow.RelayAnnunciation.cs"));

        StringAssert.Contains(behavior, "RelayLampBezelDiameter = 16.0");
        StringAssert.Contains(behavior, "RelayLampCavityDiameter = 12.4");
        StringAssert.Contains(behavior, "RelayLampHaloDiameter = 10.8");
        StringAssert.Contains(behavior, "RelayLampLensDiameter = 9.6");
        StringAssert.Contains(behavior, "ConditionalWeakTable<Ellipse, LampVisualParts>");
        StringAssert.Contains(behavior, "var halo = PhysicalEllipse(RelayLampHaloDiameter, TransparentHaloBrush, 19)");
        StringAssert.Contains(behavior, "var bezel = PhysicalEllipse(RelayLampBezelDiameter, RelayLampBezelBrush, 20)");
        StringAssert.Contains(behavior, "Panel.SetZIndex(lens, 22)");
        StringAssert.Contains(behavior, "\"BlockLed\" => RelayIndicatorState.Orange");
        StringAssert.Contains(behavior, "lens.Stroke = null");
        StringAssert.Contains(behavior, "lens.Effect = null");

        Assert.IsFalse(presentation.Contains("DependencyPropertyDescriptor", StringComparison.Ordinal));
        Assert.IsFalse(presentation.Contains("FillDescriptor", StringComparison.Ordinal));
        Assert.IsFalse(presentation.Contains("HealthyLed", StringComparison.Ordinal));
        Assert.IsFalse(presentation.Contains("TripLed", StringComparison.Ordinal));
        Assert.IsFalse(presentation.Contains("BlockLed", StringComparison.Ordinal));

        Assert.IsFalse(annunciation.Contains("TripLed.Width", StringComparison.Ordinal));
        Assert.IsFalse(annunciation.Contains("PhaseALed.Width", StringComparison.Ordinal));
        Assert.IsFalse(annunciation.Contains("EarthLed.Width", StringComparison.Ordinal));
    }

    private static IEnumerable<string> ExtractRawTemplates(string source)
    {
        foreach (var name in new[] { "RelayFinalKeyTemplateXaml", "RelayFinalResetTemplateXaml" })
        {
            var startMarker = $"private const string {name} = \"\"\"";
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, $"Template marker {name} was not found.");
            start += startMarker.Length;

            var end = source.IndexOf("\"\"\";", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start, $"Template terminator {name} was not found.");
            yield return source[start..end].Trim();
        }
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
