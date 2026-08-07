using Arvrel.Application.Ied;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class AvrSimulationEngineTests
{
    [TestMethod]
    public void InBandVoltage_DoesNotOperateTapChanger()
    {
        var engine = new AvrSimulationEngine(new AvrSettings { T1Seconds = 1 });

        var snapshot = engine.Advance(TimeSpan.FromSeconds(5), 100.0);

        Assert.AreEqual(AvrControlState.InBand, snapshot.State);
        Assert.AreEqual(0, snapshot.TapPosition);
        Assert.AreEqual(0, snapshot.OperationCount);
        Assert.IsFalse(snapshot.RaiseOutput);
        Assert.IsFalse(snapshot.LowerOutput);
    }

    [TestMethod]
    public void LowVoltage_AfterT1_RaisesTap()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            TolerancePercent = 1,
            T1Seconds = 1,
            TapStepPercent = 1.25
        });

        engine.Advance(TimeSpan.FromMilliseconds(500), 96.0);
        var snapshot = engine.Advance(TimeSpan.FromMilliseconds(500), 96.0);

        Assert.IsTrue(snapshot.RaiseOutput);
        Assert.AreEqual(1, snapshot.TapPosition);
        Assert.AreEqual(1, snapshot.OperationCount);
    }

    [TestMethod]
    public void PersistentDeviation_UsesFastT2AfterFirstOperation()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            TolerancePercent = 1,
            T1Seconds = 2,
            FastT2Enabled = true,
            T2Seconds = 0.5,
            TapStepPercent = 1
        });

        var first = engine.Advance(TimeSpan.FromSeconds(2), 90.0);
        var second = engine.Advance(TimeSpan.FromSeconds(0.5), 90.0);

        Assert.IsTrue(first.RaiseOutput);
        Assert.IsTrue(second.RaiseOutput);
        Assert.AreEqual(2, second.TapPosition);
        Assert.AreEqual(2, second.OperationCount);
    }

    [TestMethod]
    public void BenchInjection_DoesNotArtificiallyChangeInjectedVoltageAfterTap()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            VoltageInputMode = AvrVoltageInputMode.BenchInjection,
            TolerancePercent = 1,
            T1Seconds = 0,
            TapStepPercent = 1.25
        });

        var first = engine.Advance(TimeSpan.Zero, 95.0);
        var second = engine.Advance(TimeSpan.Zero, 95.0);

        Assert.AreEqual(95.0, first.MeasuredVoltageV, 0.0001);
        Assert.AreEqual(95.0, second.MeasuredVoltageV, 0.0001);
        Assert.IsTrue(first.RaiseOutput);
        Assert.IsTrue(second.RaiseOutput);
        Assert.AreEqual(2, second.TapPosition);
    }

    [TestMethod]
    public void ClosedLoopPlantMode_AppliesTapFeedbackToMeasuredVoltage()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            VoltageInputMode = AvrVoltageInputMode.ClosedLoopPlant,
            TolerancePercent = 1,
            T1Seconds = 0,
            TapStepPercent = 1.25
        });

        var first = engine.Advance(TimeSpan.Zero, 95.0);
        var second = engine.Advance(TimeSpan.Zero, 95.0);

        Assert.IsTrue(first.RaiseOutput);
        Assert.IsTrue(second.MeasuredVoltageV > first.MeasuredVoltageV);
    }

    [TestMethod]
    public void ExtremeUndervoltage_BlocksAutomaticOperation()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            T1Seconds = 0,
            UndervoltageBlockPercent = 80
        });

        var snapshot = engine.Advance(TimeSpan.FromSeconds(1), 70.0);

        Assert.AreEqual(AvrControlState.Blocked, snapshot.State);
        Assert.IsTrue(snapshot.Blocked);
        Assert.AreEqual(0, snapshot.TapPosition);
        StringAssert.Contains(snapshot.Reason, "Undervoltage");
    }

    [TestMethod]
    public void ManualMode_AllowsDirectTapCommandsWithoutAutomaticMovement()
    {
        var engine = new AvrSimulationEngine(new AvrSettings { T1Seconds = 0 });
        engine.SetMode(AvrOperatingMode.Manual);

        Assert.IsTrue(engine.RaiseTap());
        var snapshot = engine.Advance(TimeSpan.FromSeconds(10), 90.0);

        Assert.AreEqual(AvrOperatingMode.Manual, snapshot.Mode);
        Assert.AreEqual(AvrControlState.Manual, snapshot.State);
        Assert.AreEqual(1, snapshot.TapPosition);
        Assert.AreEqual(1, snapshot.OperationCount);
    }
}
