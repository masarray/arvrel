using System.Numerics;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayMeasurementOverviewProjectorTests
{
    [TestMethod]
    public void Project_MapsCurrentAndVoltageRstnAndBuildsStableCurrentPhasor()
    {
        var phasors = new PhasorMeasurementSet(
            Polar(8.4, 36),
            Polar(1.02, -84),
            Polar(0.98, 156),
            Polar(7.34, 36),
            Polar(63.5, 0),
            Polar(63.4, -120),
            Polar(63.6, 120),
            Complex.Zero,
            50);
        var frame = new MeasurementFrame(
            DateTimeOffset.UtcNow,
            8.4,
            1.02,
            0.98,
            7.34,
            SmvTrustState.Healthy,
            phasors);

        var overview = RelayMeasurementOverviewProjector.Project(frame);

        CollectionAssert.AreEqual(new[] { 8.4, 1.02, 0.98, 7.34 }, overview.CurrentsRstn.ToArray());
        CollectionAssert.AreEqual(new[] { 63.5, 63.4, 63.6, 0.0 }, overview.VoltagesRstn.ToArray());
        Assert.IsTrue(overview.VoltageAvailable);
        Assert.AreEqual(50, overview.FrequencyHz, 0.001);
        Assert.IsTrue(overview.CurrentPhasor.IsAvailable);
        Assert.AreEqual("VA = 0°", overview.CurrentPhasor.ReferenceLabel);
        CollectionAssert.AreEquivalent(
            new[] { "IA", "IB", "IC", "3I0" },
            overview.CurrentPhasor.Vectors.Select(vector => vector.Key).ToArray());
    }

    [TestMethod]
    public void Project_WithoutPhasorsPreservesCurrentAndMarksVoltageUnavailable()
    {
        var frame = new MeasurementFrame(
            DateTimeOffset.UtcNow,
            1,
            2,
            3,
            4,
            SmvTrustState.Healthy);

        var overview = RelayMeasurementOverviewProjector.Project(frame);

        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0, 4.0 }, overview.CurrentsRstn.ToArray());
        Assert.IsFalse(overview.VoltageAvailable);
        Assert.IsTrue(overview.VoltagesRstn.All(double.IsNaN));
        Assert.IsFalse(overview.CurrentPhasor.IsAvailable);
    }

    private static Complex Polar(double magnitude, double degrees)
        => Complex.FromPolarCoordinates(magnitude, degrees * Math.PI / 180.0);
}
