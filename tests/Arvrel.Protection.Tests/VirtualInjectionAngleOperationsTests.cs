using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualInjectionAngleOperationsTests
{
    [TestMethod]
    public void StandardLineAngle_ReturnsCanonicalPositiveSequenceAngles()
    {
        Assert.AreEqual(0, VirtualInjectionAngleOperations.StandardLineAngle(VirtualInjectionSignal.PhaseAVoltage), 1e-12);
        Assert.AreEqual(-120, VirtualInjectionAngleOperations.StandardLineAngle(VirtualInjectionSignal.PhaseBVoltage), 1e-12);
        Assert.AreEqual(120, VirtualInjectionAngleOperations.StandardLineAngle(VirtualInjectionSignal.PhaseCVoltage), 1e-12);
        Assert.AreEqual(0, VirtualInjectionAngleOperations.StandardLineAngle(VirtualInjectionSignal.NeutralCurrent), 1e-12);
    }

    [TestMethod]
    public void BalancedAngles_PreservesSelectedPhaseAsAnchor()
    {
        var angles = VirtualInjectionAngleOperations.BalancedAngles(
            VirtualInjectionSignal.PhaseBCurrent,
            -30);

        Assert.AreEqual(90, angles[VirtualInjectionSignal.PhaseACurrent], 1e-12);
        Assert.AreEqual(-30, angles[VirtualInjectionSignal.PhaseBCurrent], 1e-12);
        Assert.AreEqual(-150, angles[VirtualInjectionSignal.PhaseCCurrent], 1e-12);
    }

    [TestMethod]
    public void BalancedAngles_NormalizesAcrossPositive180Boundary()
    {
        var angles = VirtualInjectionAngleOperations.BalancedAngles(
            VirtualInjectionSignal.PhaseAVoltage,
            170);

        Assert.AreEqual(170, angles[VirtualInjectionSignal.PhaseAVoltage], 1e-12);
        Assert.AreEqual(50, angles[VirtualInjectionSignal.PhaseBVoltage], 1e-12);
        Assert.AreEqual(-70, angles[VirtualInjectionSignal.PhaseCVoltage], 1e-12);
    }

    [TestMethod]
    public void ReverseRotation_SwapsPhaseBAndPhaseCWithoutMovingPhaseA()
    {
        var current = new Dictionary<VirtualInjectionSignal, double>
        {
            [VirtualInjectionSignal.PhaseAVoltage] = 45,
            [VirtualInjectionSignal.PhaseBVoltage] = -75,
            [VirtualInjectionSignal.PhaseCVoltage] = 165
        };

        var reversed = VirtualInjectionAngleOperations.ReverseRotation(
            VirtualInjectionSignal.PhaseCVoltage,
            current);

        Assert.AreEqual(45, reversed[VirtualInjectionSignal.PhaseAVoltage], 1e-12);
        Assert.AreEqual(165, reversed[VirtualInjectionSignal.PhaseBVoltage], 1e-12);
        Assert.AreEqual(-75, reversed[VirtualInjectionSignal.PhaseCVoltage], 1e-12);
    }

    [TestMethod]
    public void ThreePhaseOperations_RejectNeutralSignals()
    {
        Assert.IsFalse(VirtualInjectionAngleOperations.IsThreePhaseSignal(VirtualInjectionSignal.NeutralVoltage));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            VirtualInjectionAngleOperations.BalancedAngles(VirtualInjectionSignal.NeutralVoltage, 0));
    }
}
