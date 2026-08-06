using Arvrel.Protection;
using Arvrel.Protection.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class AlgorithmSourceCatalogTests
{
    [TestMethod]
    [DataRow("67P", "phasor V1", "DirectionalPhase67CharacteristicAngleDeg")]
    [DataRow("67N", "phasor polarizingVoltage = 3V0", "DirectionalEarth67NCharacteristicAngleDeg")]
    [DataRow("27", "element \"27\"", "Undervoltage27PickupV")]
    [DataRow("59", "element \"59\"", "Overvoltage59PickupV")]
    [DataRow("59N", "element \"59N\"", "ResidualOvervoltage59NPickupV")]
    public void BuildsFeederAlgorithmSource(string element, string expectedConcept, string expectedSetting)
    {
        var source = AlgorithmSourceCatalog.Build(element, new ProtectionSettings());

        StringAssert.Contains(source, expectedConcept);
        StringAssert.Contains(source, expectedSetting);
        StringAssert.Contains(source, "smv.allowsTrip");
    }
}
