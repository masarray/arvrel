using System.Numerics;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class PhasorDisplayProjectorTests
{
    [TestMethod]
    public void VoltageDisplay_RotatesVaToZeroAndPreservesPhaseSeparation()
    {
        var source = BalancedSet(voltageAngleDegrees: 30, currentAngleDegrees: 10);

        var frame = PhasorDisplayProjector.Project(source, PhasorDisplayMode.Voltage);

        Assert.IsTrue(frame.IsAvailable);
        Assert.AreEqual("VA = 0°", frame.ReferenceLabel);
        AssertAngle(frame, "VA", 0);
        AssertAngle(frame, "VB", -120);
        AssertAngle(frame, "VC", 120);
    }

    [TestMethod]
    public void CurrentDisplay_UsesCommonVoltageReferenceWithoutChangingDirectionalAngle()
    {
        var source = BalancedSet(voltageAngleDegrees: 30, currentAngleDegrees: 10);

        var frame = PhasorDisplayProjector.Project(source, PhasorDisplayMode.Current);

        AssertAngle(frame, "IA", -20);
        AssertAngle(frame, "IB", -140);
        AssertAngle(frame, "IC", 100);
        Assert.AreEqual(-20, frame.PositiveSequenceAngleDifferenceDegrees, 0.000001);
    }

    [TestMethod]
    public void SequenceDisplay_ContainsPositiveNegativeAndResidualQuantities()
    {
        var source = BalancedSet(voltageAngleDegrees: 0, currentAngleDegrees: -25) with
        {
            PhaseACurrent = Polar(5.2, -25),
            PhaseBCurrent = Polar(4.8, -145),
            PhaseCCurrent = Polar(5.0, 95)
        };

        var frame = PhasorDisplayProjector.Project(source, PhasorDisplayMode.Sequence);

        CollectionAssert.AreEquivalent(
            new[] { "I1", "I2", "3I0", "V1", "V2", "3V0" },
            frame.Vectors.Select(vector => vector.Key).ToArray());
        Assert.IsTrue(frame.Vectors.Single(vector => vector.Key == "I2").IsNegativeSequence);
        Assert.IsTrue(frame.Vectors.Single(vector => vector.Key == "3I0").IsResidual);
    }

    [TestMethod]
    public void MissingPhasors_ProducesExplicitUnavailableFrame()
    {
        var frame = PhasorDisplayProjector.Project(null, PhasorDisplayMode.Current);

        Assert.IsFalse(frame.IsAvailable);
        Assert.AreEqual(0, frame.Vectors.Count);
        StringAssert.Contains(frame.Status, "one-cycle");
    }

    private static PhasorMeasurementSet BalancedSet(double voltageAngleDegrees, double currentAngleDegrees)
        => new(
            Polar(5, currentAngleDegrees),
            Polar(5, currentAngleDegrees - 120),
            Polar(5, currentAngleDegrees + 120),
            Complex.Zero,
            Polar(100, voltageAngleDegrees),
            Polar(100, voltageAngleDegrees - 120),
            Polar(100, voltageAngleDegrees + 120),
            Complex.Zero,
            50);

    private static Complex Polar(double magnitude, double degrees)
        => Complex.FromPolarCoordinates(magnitude, degrees * Math.PI / 180);

    private static void AssertAngle(PhasorDisplayFrame frame, string key, double expected)
    {
        var actual = frame.Vectors.Single(vector => vector.Key == key).AngleDegrees;
        Assert.AreEqual(expected, actual, 0.000001, $"Unexpected angle for {key}.");
    }
}
