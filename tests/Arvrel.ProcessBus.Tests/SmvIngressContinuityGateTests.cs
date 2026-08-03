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
    public void ForwardGapRequestsTrustRecoveryWithoutLosingNewAnchor()
    {
        var gate = new SmvIngressContinuityGate();

        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(10, 4000));
        Assert.AreEqual(SmvIngressDecision.RestartWindow, gate.Observe(15, 4000));
        Assert.AreEqual(SmvIngressDecision.Accept, gate.Observe(16, 4000));
    }

    [TestMethod]
    public void MultipleAsdusInOneEthernetFrameAdvanceAsOneTransaction()
    {
        var gate = new SmvIngressContinuityGate();

        var first = gate.ObserveFrame(new ushort[] { 200, 201, 202, 203 }, 4000);
        var second = gate.ObserveFrame(new ushort[] { 204, 205, 206, 207 }, 4000);

        Assert.AreEqual(SmvIngressDecision.Accept, first.Decision);
        Assert.AreEqual((ushort)203, first.SampleCount);
        Assert.AreEqual(SmvIngressDecision.Accept, second.Decision);
        Assert.AreEqual((ushort)207, second.SampleCount);
    }

    [TestMethod]
    public void RejectedMultiAsduFrameDoesNotPartiallyMoveAcceptedAnchor()
    {
        var gate = new SmvIngressContinuityGate();

        Assert.AreEqual(
            SmvIngressDecision.Accept,
            gate.ObserveFrame(new ushort[] { 300, 301 }, 4000).Decision);

        var rejected = gate.ObserveFrame(new ushort[] { 302, 302 }, 4000);
        Assert.AreEqual(SmvIngressDecision.DropDuplicate, rejected.Decision);

        var recovery = gate.ObserveFrame(new ushort[] { 302, 303 }, 4000);
        Assert.AreEqual(SmvIngressDecision.Accept, recovery.Decision);
        Assert.AreEqual((ushort)303, recovery.SampleCount);
    }
}
