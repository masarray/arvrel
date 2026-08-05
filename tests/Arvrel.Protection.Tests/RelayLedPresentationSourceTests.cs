using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayLedPresentationSourceTests
{
    [TestMethod]
    public void UnifiedPresentation_CoversEveryRelayFaceplateIndicator()
    {
        var source = File.ReadAllText(SourcePath());
        var expectedIndicators = new[]
        {
            "HealthyLed",
            "PickupLed",
            "TripLed",
            "PhaseALed",
            "PhaseBLed",
            "PhaseCLed",
            "EarthLed",
            "BlockLed",
            "TopHealthLed"
        };

        foreach (var indicator in expectedIndicators)
            StringAssert.Contains(source, $"ConfigureRelayLed({indicator}");

        StringAssert.Contains(source, "RelayLedHealthyEffect");
        StringAssert.Contains(source, "RelayLedWarningEffect");
        StringAssert.Contains(source, "RelayLedTripEffect");
        StringAssert.Contains(source, "RelayLedPhaseEffect");
        StringAssert.Contains(source, "RelayLedLensOpacityMask");
        StringAssert.Contains(source, "DependencyPropertyDescriptor.FromProperty(Shape.FillProperty");
    }

    private static string SourcePath()
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
                var candidate = Path.Combine(
                    current.FullName,
                    "src",
                    "Arvrel.App",
                    "MainWindow.RelayLedPresentation.cs");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException(
            "Unable to locate src/Arvrel.App/MainWindow.RelayLedPresentation.cs from the test workspace.");
    }
}
