using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayLedPresentationSourceTests
{
    [TestMethod]
    public void FaceplateIndicators_AreOwnedOnlyByCompositeLampBehavior()
    {
        var behavior = File.ReadAllText(ControlSourcePath());
        var presentation = File.ReadAllText(PresentationSourcePath());

        foreach (var indicator in new[]
                 {
                     "HealthyLed", "PickupLed", "TripLed", "BlockLed",
                     "PhaseALed", "PhaseBLed", "PhaseCLed", "EarthLed"
                 })
        {
            StringAssert.Contains(behavior, indicator);
            Assert.IsFalse(presentation.Contains(indicator, StringComparison.Ordinal));
        }

        StringAssert.Contains(behavior, "LampVisualParts");
        StringAssert.Contains(behavior, "RelayLampBezelBrush");
        StringAssert.Contains(behavior, "RelayLampCavityBrush");
        StringAssert.Contains(behavior, "parts.Halo.Effect = visual.Glow");
        StringAssert.Contains(behavior, "FillDescriptor.AddValueChanged");

        StringAssert.Contains(presentation, "ConfigureToolbarHealthLed(TopHealthLed)");
        Assert.IsFalse(presentation.Contains("DependencyPropertyDescriptor", StringComparison.Ordinal));
        Assert.IsFalse(presentation.Contains("FillDescriptor", StringComparison.Ordinal));
        Assert.IsFalse(presentation.Contains("RelayLedHealthyEffect", StringComparison.Ordinal));
        Assert.IsFalse(presentation.Contains("RelayLedTripEffect", StringComparison.Ordinal));
    }

    private static string ControlSourcePath()
        => Locate("src", "Arvrel.App", "Controls", "RelayIndicatorLampBehavior.cs");

    private static string PresentationSourcePath()
        => Locate("src", "Arvrel.App", "MainWindow.RelayLedPresentation.cs");

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
