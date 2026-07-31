using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class MeasurementContextTests
{
    [TestMethod]
    public void CalculatesPrimaryRatio()
    {
        var context = new SmvMeasurementContext
        {
            CtPrimaryA = 1000,
            CtSecondaryA = 1,
            NominalFrequencyHz = 50
        };

        context.Validate();
        Assert.AreEqual(1000, context.PrimaryRatio, 0.000001);
    }

    [TestMethod]
    public void RejectsInvalidFrequency()
    {
        var context = new SmvMeasurementContext { NominalFrequencyHz = 70 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(context.Validate);
    }
}
