using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class CtIndependentValidationTests
{
    [TestMethod]
    public void ProductionSolverMatchesIndependentGoldenVectors()
        => CtReferenceGoldenSet.ValidateAll();
}
