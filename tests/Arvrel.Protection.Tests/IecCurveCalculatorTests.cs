using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class IecCurveCalculatorTests
{
    [TestMethod]
    public void StandardInverse_MatchesIecEquation()
    {
        var actual = IecCurveCalculator.GetOperateTimeSeconds(
            IecCurveFamily.StandardInverse,
            2,
            0.1,
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero);
        var expected = 0.1 * 0.14 / (Math.Pow(2, 0.02) - 1);
        Assert.AreEqual(expected, actual, 1e-9);
    }

    [TestMethod]
    public void VeryExtremeAndLongTime_HaveExpectedOrderingAtTenTimesPickup()
    {
        var very = Time(IecCurveFamily.VeryInverse, 10);
        var extreme = Time(IecCurveFamily.ExtremelyInverse, 10);
        var longTime = Time(IecCurveFamily.LongTimeInverse, 10);
        Assert.IsTrue(extreme < very, $"Expected EI ({extreme}) to be faster than VI ({very}) at 10x pickup.");
        Assert.IsTrue(longTime > very, $"Expected LTI ({longTime}) to be slower than VI ({very}).");
    }

    [TestMethod]
    public void DefiniteTime_IgnoresCurrentMultiple()
    {
        var at2 = IecCurveCalculator.GetOperateTimeSeconds(
            IecCurveFamily.DefiniteTime, 2, 0.1, TimeSpan.FromMilliseconds(450), TimeSpan.Zero);
        var at20 = IecCurveCalculator.GetOperateTimeSeconds(
            IecCurveFamily.DefiniteTime, 20, 0.1, TimeSpan.FromMilliseconds(450), TimeSpan.Zero);
        Assert.AreEqual(0.45, at2, 1e-9);
        Assert.AreEqual(at2, at20, 1e-9);
    }

    [TestMethod]
    public void MinimumOperateTime_ClampsFastCurve()
    {
        var actual = IecCurveCalculator.GetOperateTimeSeconds(
            IecCurveFamily.ExtremelyInverse,
            100,
            0.01,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(50));
        Assert.AreEqual(0.05, actual, 1e-9);
    }

    [TestMethod]
    public void DisabledElement_RemainsDisabledDuringFault()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousEnabled = false,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        };
        var engine = new ProtectionEngine(settings);
        var timestamp = DateTimeOffset.UtcNow;
        ProtectionSnapshot snapshot = ProtectionSnapshot.Ready(timestamp);
        for (var index = 0; index < 100; index++)
        {
            snapshot = engine.Evaluate(new MeasurementFrame(
                timestamp.AddMilliseconds(index * 5),
                100,
                100,
                100,
                100,
                SmvTrustState.Healthy));
        }

        Assert.AreEqual(ProtectionStageState.Disabled, snapshot.Phase50.State);
        Assert.AreEqual(ProtectionStageState.Disabled, snapshot.Phase51.State);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void UpdateSettings_ResetsTimerAndUsesNewCurve()
    {
        var engine = new ProtectionEngine(new ProtectionSettings
        {
            PhaseInstantaneousEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            PhaseTimeCurve = IecCurveFamily.LongTimeInverse,
            PhaseTimePickupA = 1,
            PhaseTimeMultiplier = 1
        });
        var timestamp = DateTimeOffset.UtcNow;
        for (var index = 0; index < 50; index++)
            engine.Evaluate(new MeasurementFrame(timestamp.AddMilliseconds(index * 5), 10, 0, 0, 0, SmvTrustState.Healthy));

        engine.UpdateSettings(engine.Settings with
        {
            PhaseTimeCurve = IecCurveFamily.DefiniteTime,
            PhaseTimeDefiniteDelay = TimeSpan.FromMilliseconds(20)
        });
        var first = engine.Evaluate(new MeasurementFrame(timestamp.AddSeconds(2), 10, 0, 0, 0, SmvTrustState.Healthy));
        Assert.AreEqual(0, first.Phase51.Progress, 1e-9);
        var operated = engine.Evaluate(new MeasurementFrame(timestamp.AddSeconds(2).AddMilliseconds(25), 10, 0, 0, 0, SmvTrustState.Healthy));
        Assert.IsTrue(operated.Phase51.Operated);
    }

    private static double Time(IecCurveFamily curve, double multiple)
        => IecCurveCalculator.GetOperateTimeSeconds(
            curve,
            multiple,
            0.1,
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero);
}
