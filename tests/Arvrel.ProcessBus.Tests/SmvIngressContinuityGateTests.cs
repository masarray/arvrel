using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class SmvIngressContinuityGateTests
{
    [TestMethod]
    public void ContiguousSequenceAndCounterWrapAreAccepted()
    {
        var gate = new SmvIngressContinuityGate();

        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(3998, 4000));
        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(3999, 4000));
        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(0, 4000));
        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(1, 4000));
    }

    [TestMethod]
    public void DuplicateAndOutOfOrderSamplesAreDroppedWithoutMovingAnchor()
    {
        var gate = new SmvIngressContinuityGate();

        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(100, 4000));
        Assert.AreEqual(SmvIngressDecision.DropDuplicate, gate.Observe(100, 4000));
        Assert.AreEqual(SmvIngressDecision.DropOutOfOrder, gate.Observe(99, 4000));
        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(101, 4000));
    }

    [TestMethod]
    public void ForwardGapRequestsCleanMeasurementWindowRestart()
    {
        var gate = new SmvIngressContinuityGate();

        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(10, 4000));
        Assert.AreEqual(SmvIngressDecision.RestartWindow, gate.Observe(15, 4000));
        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(16, 4000));
    }
}
