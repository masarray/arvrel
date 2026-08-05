using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayFaceplateFinalRefinementSourceTests
{
    [TestMethod]
    public void FinalGloss_IsDedicatedBroadOverlayAcrossCompleteFascia()
    {
        var source = File.ReadAllText(AppSourcePath("MainWindow.RelayFaceplateFinalRefinement.cs"));

        StringAssert.Contains(source, "ARVREL_RELAY_FINAL_FULL_FACE_GLOSS");
        StringAssert.Contains(source, "bodyGrid.Children.Insert(0, gloss)");
        StringAssert.Contains(source, "Grid.SetRowSpan(gloss, Math.Max(1, bodyGrid.RowDefinitions.Count))");
        StringAssert.Contains(source, "Grid.SetColumnSpan(gloss, Math.Max(1, bodyGrid.ColumnDefinitions.Count))");
        StringAssert.Contains(source, "gloss.Margin = new Thickness(1.1)");
        StringAssert.Contains(source, "gloss.CornerRadius = new CornerRadius(9.7)");
        StringAssert.Contains(source, "M 0,0 L 0.72,0 L 0.24,1 L 0,1 Z");
        StringAssert.Contains(source, "gloss.IsHitTestVisible = false");
        Assert.IsFalse(source.Contains("Height = 62", StringComparison.Ordinal));
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
    public void FinalRefinement_IsAppliedAfterEarlierPresentationLayers()
    {
        var source = File.ReadAllText(AppSourcePath("MainWindow.RelayFaceplateFinalRefinement.cs"));

        StringAssert.Contains(source, "if (!_relayHardwarePresentationInitialized)");
        StringAssert.Contains(source, "DispatcherPriority.SystemIdle");
        StringAssert.Contains(source, "MaximumRelayFinalRefinementAttempts");
        StringAssert.Contains(source, "button.Template = RelayFinalKeyTemplate");
        StringAssert.Contains(source, "button.Template = RelayFinalResetTemplate");
        Assert.IsFalse(source.Contains("DispatcherPriority.Inactive", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RelayLamps_UseOneMetalBezelAndAmberSmvBlock()
    {
        var behavior = File.ReadAllText(ControlSourcePath("RelayIndicatorLampBehavior.cs"));
        var presentation = File.ReadAllText(AppSourcePath("MainWindow.RelayLedPresentation.cs"));
        var finalLayer = File.ReadAllText(AppSourcePath("MainWindow.RelayFaceplateFinalRefinement.cs"));

        StringAssert.Contains(behavior, "RelayLampBezelBrush");
        StringAssert.Contains(behavior, "ActiveLampGlowBlurRadius");
        StringAssert.Contains(behavior, "ActiveLampGlowOpacity");
        StringAssert.Contains(behavior, "\"BlockLed\" => RelayIndicatorState.Orange");
        StringAssert.Contains(behavior, "ellipse.Stroke = RelayLampBezelBrush");
        StringAssert.Contains(behavior, "RenderingBias = RenderingBias.Quality");

        StringAssert.Contains(presentation, "var diameter = compact ? 8d : 14d");
        StringAssert.Contains(presentation, "led.OpacityMask = compact ? RelayLedLensOpacityMask : null");
        StringAssert.Contains(presentation, "if (led.Fill is RadialGradientBrush)");

        foreach (var indicator in new[]
                 {
                     "HealthyLed", "PickupLed", "TripLed", "PhaseALed",
                     "PhaseBLed", "PhaseCLed", "EarthLed", "BlockLed"
                 })
        {
            StringAssert.Contains(finalLayer, indicator);
        }
        StringAssert.Contains(finalLayer, "led.Width = 14");
        StringAssert.Contains(finalLayer, "led.StrokeThickness = 2");
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
