using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerEngineeringTests
{
    [TestMethod]
    public void StandardDualSlope_UsesFamiliarIs1K1Is2K2Semantics()
    {
        var settings = new TransformerDifferentialSettings
        {
            MinimumPickupPu = 0.20,
            Slope1 = 0.30,
            SlopeBreakpointPu = 1.0,
            Slope2 = 0.80
        };

        Assert.AreEqual(0.20, settings.Is1Pu, 1e-12);
        Assert.AreEqual(0.30, settings.K1, 1e-12);
        Assert.AreEqual(1.00, settings.Is2Pu, 1e-12);
        Assert.AreEqual(0.80, settings.K2, 1e-12);
        Assert.AreEqual(0.50, settings.StandardSlopeThresholdPu(1.0), 1e-12);
        Assert.AreEqual(1.30, settings.StandardSlopeThresholdPu(2.0), 1e-12);
    }

    [TestMethod]
    public void VectorGroup_Dyn11_MapsToMinusThirtyDegreeLvCompensation()
    {
        var vector = TransformerVectorGroup.Parse("Dyn11");

        Assert.AreEqual(TransformerWindingConnection.Delta, vector.HighVoltageConnection);
        Assert.AreEqual(TransformerWindingConnection.Wye, vector.LowVoltageConnection);
        Assert.IsTrue(vector.LowVoltageNeutralBroughtOut);
        Assert.AreEqual(11, vector.ClockNumber);
        Assert.AreEqual(-30, vector.SignedClockDisplacementDegrees, 1e-12);
    }

    [TestMethod]
    public void VectorGroup_YNd1_MapsToPlusThirtyDegreeLvCompensation()
    {
        var vector = TransformerVectorGroup.Parse("YNd1");

        Assert.AreEqual(TransformerWindingConnection.Wye, vector.HighVoltageConnection);
        Assert.IsTrue(vector.HighVoltageNeutralBroughtOut);
        Assert.AreEqual(TransformerWindingConnection.Delta, vector.LowVoltageConnection);
        Assert.AreEqual(30, vector.SignedClockDisplacementDegrees, 1e-12);
    }

    [TestMethod]
    public void EngineeringPlan_ComputesRatedCurrentsCtScalingAndZeroSequenceRule()
    {
        var plan = TransformerEngineeringAdapter.Build(
            new TransformerNameplate(100, 100, 10, "Dyn11"),
            new TransformerWindingEngineering(new TransformerCtRatio(1000, 1)),
            new TransformerWindingEngineering(new TransformerCtRatio(6000, 1)));

        Assert.AreEqual(577.350269, plan.HighVoltageRatedCurrentA, 0.001);
        Assert.AreEqual(5773.502692, plan.LowVoltageRatedCurrentA, 0.001);
        Assert.AreEqual(1.7320508, plan.HighVoltageCompensation.CurrentScaleToPu, 1e-6);
        Assert.AreEqual(1.0392305, plan.LowVoltageCompensation.CurrentScaleToPu, 1e-6);
        Assert.AreEqual(-30, plan.LowVoltageCompensation.PhaseShiftDegrees, 1e-12);
        Assert.IsFalse(plan.HighVoltageCompensation.RemoveZeroSequence);
        Assert.IsTrue(plan.LowVoltageCompensation.RemoveZeroSequence);
    }

    [TestMethod]
    public void EngineeringPlan_Dyn11ThroughCurrent_RemainsDifferentiallyStable()
    {
        var plan = TransformerEngineeringAdapter.Build(
            new TransformerNameplate(100, 100, 10, "Dyn11"),
            new TransformerWindingEngineering(new TransformerCtRatio(1000, 1)),
            new TransformerWindingEngineering(new TransformerCtRatio(6000, 1)));
        var settings = plan.ApplyTo(new TransformerProtectionSettings
        {
            Differential87T = new TransformerDifferentialSettings
            {
                Enabled = true,
                MinimumPickupPu = 0.20,
                Slope1 = 0.30,
                SlopeBreakpointPu = 1.0,
                Slope2 = 0.80,
                OperateDelay = TimeSpan.Zero,
                HarmonicSecurityMode = TransformerHarmonicSecurityMode.Disabled
            }
        });
        var hvBase = Balanced(1, 0);
        var hvRaw = Scale(hvBase, 1 / plan.HighVoltageCompensation.CurrentScaleToPu);

        var undoLvCompensation = Complex.FromPolarCoordinates(
            1 / plan.LowVoltageCompensation.CurrentScaleToPu,
            -plan.LowVoltageCompensation.PhaseShiftDegrees * Math.PI / 180);
        var lvRaw = Scale(Negate(hvBase), undoLvCompensation);

        var frame = new TransformerMeasurementFrame(
            DateTimeOffset.UtcNow,
            new TransformerWindingMeasurement(hvRaw, SmvTrustState.Healthy),
            new TransformerWindingMeasurement(lvRaw, SmvTrustState.Healthy));

        var snapshot = new TransformerProtectionEngine(settings).Evaluate(frame);

        Assert.IsFalse(snapshot.Differential.Restrained87T.Pickup);
        Assert.IsFalse(snapshot.TripLatched);
        Assert.IsTrue(snapshot.Differential.Phases.All(phase => phase.OperatingCurrentPu < 1e-9));
    }

    [TestMethod]
    public void EngineeringPlan_AppliesIndependentNeutralCtScalingForRef()
    {
        var plan = TransformerEngineeringAdapter.Build(
            new TransformerNameplate(40, 20, 0.4, "Dyn11"),
            new TransformerWindingEngineering(
                new TransformerCtRatio(2000, 1),
                new TransformerCtRatio(400, 1)),
            new TransformerWindingEngineering(
                new TransformerCtRatio(4000, 1),
                new TransformerCtRatio(1000, 1)));

        var applied = plan.ApplyTo(new TransformerProtectionSettings());

        Assert.AreEqual(0.20, applied.RefHighVoltage.NeutralCurrentScale, 1e-12);
        Assert.AreEqual(0.25, applied.RefLowVoltage.NeutralCurrentScale, 1e-12);
    }

    [TestMethod]
    public void EngineeringPlan_RejectsAutomaticZigZagCompensation()
    {
        var nameplate = new TransformerNameplate(10, 20, 6.6, "Zyn11");

        Assert.ThrowsExactly<NotSupportedException>(() => TransformerEngineeringAdapter.Build(
            nameplate,
            new TransformerWindingEngineering(new TransformerCtRatio(400, 1)),
            new TransformerWindingEngineering(new TransformerCtRatio(1000, 1))));
    }

    private static TransformerPhaseCurrents Balanced(double magnitude, double angleDegrees) => new(
        Polar(magnitude, angleDegrees),
        Polar(magnitude, angleDegrees - 120),
        Polar(magnitude, angleDegrees + 120));

    private static TransformerPhaseCurrents Negate(TransformerPhaseCurrents currents) => new(
        -currents.PhaseA,
        -currents.PhaseB,
        -currents.PhaseC);

    private static TransformerPhaseCurrents Scale(TransformerPhaseCurrents currents, double factor) => new(
        currents.PhaseA * factor,
        currents.PhaseB * factor,
        currents.PhaseC * factor);

    private static TransformerPhaseCurrents Scale(TransformerPhaseCurrents currents, Complex factor) => new(
        currents.PhaseA * factor,
        currents.PhaseB * factor,
        currents.PhaseC * factor);

    private static Complex Polar(double magnitude, double angleDegrees)
        => Complex.FromPolarCoordinates(magnitude, angleDegrees * Math.PI / 180);
}
