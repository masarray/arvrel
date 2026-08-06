using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class MeasurementContextTests
{
    [TestMethod]
    public void CalculatesPrimaryCurrentAndVoltageRatios()
    {
        var context = new SmvMeasurementContext
        {
            CtPrimaryA = 1000,
            CtSecondaryA = 1,
            VtPrimaryV = 20_000,
            VtSecondaryV = 100,
            NominalFrequencyHz = 50
        };

        context.Validate();
        Assert.AreEqual(1000, context.PrimaryRatio, 0.000001);
        Assert.AreEqual(200, context.VoltagePrimaryRatio, 0.000001);
    }

    [TestMethod]
    public void RejectsInvalidFrequency()
    {
        var context = new SmvMeasurementContext { NominalFrequencyHz = 70 };
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(context.Validate);
    }

    [TestMethod]
    public void RejectsInvalidVtSecondary()
    {
        var context = new SmvMeasurementContext { VtSecondaryV = 0 };
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(context.Validate);
    }
}
