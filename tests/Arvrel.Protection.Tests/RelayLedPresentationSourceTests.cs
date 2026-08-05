using System.Runtime.CompilerServices;
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

    private static string SourcePath([CallerFilePath] string testFile = "")
    {
        var testsDirectory = Path.GetDirectoryName(testFile)
            ?? throw new InvalidOperationException("Unable to resolve test source directory.");
        var repositoryRoot = Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));
        return Path.Combine(repositoryRoot, "src", "Arvrel.App", "MainWindow.RelayLedPresentation.cs");
    }
}
