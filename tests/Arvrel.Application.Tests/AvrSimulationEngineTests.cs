using Arvrel.Application.Ied;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class AvrSimulationEngineTests
{
    [TestMethod]
    public void DeenergizedBenchSource_ShowsZeroMeasurementWithoutRegulation()
    {
        var engine = new AvrSimulationEngine(new AvrSettings { T1Seconds = 0 });

        var snapshot = engine.Advance(TimeSpan.FromSeconds(5), 100.0, 1.0, 0, sourceEnergized: false);

        Assert.AreEqual(AvrControlState.SourceOff, snapshot.State);
        Assert.IsFalse(snapshot.SourceEnergized);
        Assert.AreEqual(0.0, snapshot.SourceVoltageV, 0.0001);
        Assert.AreEqual(0.0, snapshot.SourceCurrentA, 0.0001);
        Assert.AreEqual(0.0, snapshot.MeasuredVoltageV, 0.0001);
        Assert.AreEqual(0, snapshot.OperationCount);
        Assert.IsFalse(snapshot.Blocked);
    }

    [TestMethod]
    public void InBandVoltage_DoesNotOperateTapChanger()
    {
        var engine = new AvrSimulationEngine(new AvrSettings { T1Seconds = 1 });
        var snapshot = engine.Advance(TimeSpan.FromSeconds(5), 100.0, 1.0, -30.0);
        Assert.AreEqual(AvrControlState.InBand, snapshot.State);
        Assert.AreEqual(0, snapshot.TapPosition);
        Assert.AreEqual(0, snapshot.OperationCount);
        Assert.AreEqual(1.0, snapshot.SourceCurrentA, 0.0001);
        Assert.AreEqual(Math.Cos(Math.PI / 6), snapshot.PowerFactor, 0.0001);
    }

    [TestMethod]
    public void LowVoltage_AfterT1_IssuesRaiseBeforeTapFeedbackChanges()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            TolerancePercent = 1,
            T1Seconds = 1,
            CommandPulseSeconds = 0.5,
            MotorDriveTravelSeconds = 3
        });
        engine.Advance(TimeSpan.FromMilliseconds(500), 96.0);
        var command = engine.Advance(TimeSpan.FromMilliseconds(500), 96.0);
        Assert.AreEqual(AvrControlState.TapMoving, command.State);
        Assert.IsTrue(command.RaiseOutput);
        Assert.AreEqual(0, command.TapPosition, "Tap feedback must not jump when the command contact operates.");
        Assert.AreEqual(1, command.PendingTapPosition);
        Assert.AreEqual(1, command.OperationCount);
        var feedback = engine.Advance(TimeSpan.FromSeconds(3), 96.0);
        Assert.AreEqual(1, feedback.TapPosition);
        Assert.IsNull(feedback.PendingTapPosition);
    }

    [TestMethod]
    public void PersistentDeviation_UsesFastT2AfterTapFeedback()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            TolerancePercent = 1,
            T1Seconds = 2,
            FastT2Enabled = true,
            T2Seconds = 0.5,
            MotorDriveTravelSeconds = 1,
            CommandPulseSeconds = 0.2
        });
        var firstCommand = engine.Advance(TimeSpan.FromSeconds(2), 90.0);
        Assert.IsTrue(firstCommand.RaiseOutput);
        Assert.AreEqual(0, firstCommand.TapPosition);
        var firstFeedback = engine.Advance(TimeSpan.FromSeconds(1), 90.0);
        Assert.AreEqual(1, firstFeedback.TapPosition);
        Assert.IsNull(firstFeedback.PendingTapPosition);
        var timing = engine.Advance(TimeSpan.FromSeconds(0.49), 90.0);
        Assert.AreEqual(AvrControlState.TimingRaise, timing.State);
        var secondCommand = engine.Advance(TimeSpan.FromSeconds(0.01), 90.0);
        Assert.IsTrue(secondCommand.RaiseOutput);
        Assert.AreEqual(2, secondCommand.PendingTapPosition);
        Assert.AreEqual(2, secondCommand.OperationCount);
    }

    [TestMethod]
    public void BenchInjection_DoesNotArtificiallyChangeMeasuredVoltageAfterTapFeedback()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            VoltageInputMode = AvrVoltageInputMode.BenchInjection,
            TolerancePercent = 1,
            T1Seconds = 0,
            T2Seconds = 10,
            MotorDriveTravelSeconds = 0.1
        });
        var command = engine.Advance(TimeSpan.Zero, 95.0);
        var feedback = engine.Advance(TimeSpan.FromSeconds(0.1), 95.0);
        Assert.IsTrue(command.RaiseOutput);
        Assert.AreEqual(95.0, command.MeasuredVoltageV, 0.0001);
        Assert.AreEqual(95.0, feedback.MeasuredVoltageV, 0.0001);
        Assert.AreEqual(1, feedback.TapPosition);
    }

    [TestMethod]
    public void ClosedLoopPlantMode_AppliesCompletedTapFeedbackToMeasuredVoltage()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            VoltageInputMode = AvrVoltageInputMode.ClosedLoopPlant,
            TolerancePercent = 1,
            T1Seconds = 0,
            T2Seconds = 10,
            TapStepPercent = 1.25,
            MotorDriveTravelSeconds = 0.1
        });
        var command = engine.Advance(TimeSpan.Zero, 95.0);
        var feedback = engine.Advance(TimeSpan.FromSeconds(0.1), 95.0);
        Assert.AreEqual(0, command.TapPosition);
        Assert.AreEqual(1, feedback.TapPosition);
        Assert.IsTrue(feedback.MeasuredVoltageV > command.MeasuredVoltageV);
    }

    [TestMethod]
    public void ExtremeUndervoltage_BlocksAutomaticOperation()
    {
        var engine = new AvrSimulationEngine(new AvrSettings { T1Seconds = 0, UndervoltageBlockPercent = 80 });
        var snapshot = engine.Advance(TimeSpan.FromSeconds(1), 70.0);
        Assert.AreEqual(AvrControlState.Blocked, snapshot.State);
        Assert.IsTrue(snapshot.Blocked);
        Assert.AreEqual(0, snapshot.OperationCount);
        StringAssert.Contains(snapshot.Reason, "Undervoltage");
    }

    [TestMethod]
    public void UndercurrentBlocking_InhibitsVoltageRegulation()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            T1Seconds = 0,
            NominalCurrentA = 1,
            UndercurrentBlockingEnabled = true,
            UndercurrentBlockPercent = 20
        });
        var snapshot = engine.Advance(TimeSpan.FromSeconds(1), 95.0, 0.1, 0);
        Assert.AreEqual(AvrControlState.Blocked, snapshot.State);
        Assert.IsTrue(snapshot.Blocked);
        Assert.AreEqual(0, snapshot.OperationCount);
        StringAssert.Contains(snapshot.Reason, "Undercurrent");
    }

    [TestMethod]
    public void OvercurrentBlocking_InhibitsVoltageRegulation()
    {
        var engine = new AvrSimulationEngine(new AvrSettings
        {
            T1Seconds = 0,
            NominalCurrentA = 1,
            OvercurrentBlockingEnabled = true,
            OvercurrentBlockPercent = 150
        });
        var snapshot = engine.Advance(TimeSpan.FromSeconds(1), 95.0, 2.0, 0);
        Assert.AreEqual(AvrControlState.Blocked, snapshot.State);
        Assert.IsTrue(snapshot.Blocked);
        StringAssert.Contains(snapshot.Reason, "Overcurrent");
    }

    [TestMethod]
    public void RemoteManualMode_InhibitsLocalFrontPanelTapCommand()
    {
        var engine = new AvrSimulationEngine(new AvrSettings());
        engine.SetMode(AvrOperatingMode.Manual);
        engine.SetAuthority(AvrControlAuthority.Remote);
        Assert.IsFalse(engine.RaiseTap());
        var snapshot = engine.Advance(TimeSpan.Zero, 95.0);
        Assert.AreEqual(AvrOperatingMode.Manual, snapshot.Mode);
        Assert.AreEqual(AvrControlAuthority.Remote, snapshot.Authority);
        Assert.AreEqual(0, snapshot.OperationCount);
        StringAssert.Contains(snapshot.Reason, "local tap commands inhibited");
    }

    [TestMethod]
    public void LocalManualMode_IssuesCommandThenWaitsForMotorFeedback()
    {
        var engine = new AvrSimulationEngine(new AvrSettings { MotorDriveTravelSeconds = 2, CommandPulseSeconds = 0.5 });
        engine.SetMode(AvrOperatingMode.Manual);
        engine.SetAuthority(AvrControlAuthority.Local);
        Assert.IsTrue(engine.RaiseTap());
        var command = engine.Advance(TimeSpan.Zero, 100.0);
        Assert.AreEqual(0, command.TapPosition);
        Assert.AreEqual(1, command.PendingTapPosition);
        Assert.IsTrue(command.RaiseOutput);
        var feedback = engine.Advance(TimeSpan.FromSeconds(2), 100.0);
        Assert.AreEqual(1, feedback.TapPosition);
        Assert.IsNull(feedback.PendingTapPosition);
        Assert.AreEqual(1, feedback.OperationCount);
    }

    [TestMethod]
    public void ManualTapActuator_CompletesEvenWhenMeasurementSourceIsOff()
    {
        var engine = new AvrSimulationEngine(new AvrSettings { MotorDriveTravelSeconds = 1, CommandPulseSeconds = 0.2 });
        engine.SetMode(AvrOperatingMode.Manual);
        engine.SetAuthority(AvrControlAuthority.Local);
        Assert.IsTrue(engine.RaiseTap());
        var moving = engine.Advance(TimeSpan.Zero, 0, 0, 0, sourceEnergized: false);
        Assert.AreEqual(AvrControlState.TapMoving, moving.State);
        var complete = engine.Advance(TimeSpan.FromSeconds(1), 0, 0, 0, sourceEnergized: false);
        Assert.AreEqual(1, complete.TapPosition);
        Assert.AreEqual(AvrControlState.SourceOff, complete.State);
    }
}
