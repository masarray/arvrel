using System.Numerics;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class TransformerSvPairingTests
{
    [TestMethod]
    public void SameGlobalSampleCounter_ProducesAlignedTransformerFrame()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot("hv", 100, 2, timestamp, 0);
        var lv = Snapshot("lv", 100, 2, timestamp.AddMilliseconds(0.2), 0);

        var pair = TransformerProcessBusAdapter.AlignSnapshots(hv, lv);

        Assert.IsTrue(pair.IsAligned, pair.Diagnostics.Detail);
        Assert.AreEqual("PAIR_ALIGNED", pair.Diagnostics.Code);
        Assert.AreEqual(0, pair.Diagnostics.SignedSampleCounterSkew);
        Assert.IsNotNull(pair.Measurement);
        Assert.AreEqual(1, pair.Measurement.HighVoltage.FundamentalCurrentA.PhaseA.Magnitude, 0.01);
        Assert.AreEqual(1, pair.Measurement.LowVoltage.FundamentalCurrentA.PhaseA.Magnitude, 0.01);
    }

    [TestMethod]
    public void OneSampleArrivalOffset_IsPhaseCorrectedToHvReference()
    {
        const int samplesPerCycle = 80;
        var timestamp = DateTimeOffset.UtcNow;
        var oneSampleDegrees = 360.0 / samplesPerCycle;
        var hv = Snapshot("hv", 500, 2, timestamp, 0);
        var lv = Snapshot("lv", 501, 2, timestamp.AddMilliseconds(0.25), oneSampleDegrees);

        var pair = TransformerProcessBusAdapter.AlignSnapshots(hv, lv);

        Assert.IsTrue(pair.IsAligned, pair.Diagnostics.Detail);
        Assert.AreEqual(1, pair.Diagnostics.SignedSampleCounterSkew);
        Assert.AreEqual(-oneSampleDegrees, pair.Diagnostics.PhaseCorrectionDegrees, 1e-9);
        Assert.IsNotNull(pair.Measurement);

        var hvA = pair.Measurement.HighVoltage.FundamentalCurrentA.PhaseA;
        var lvA = pair.Measurement.LowVoltage.FundamentalCurrentA.PhaseA;
        Assert.AreEqual(0, NormalizeDegrees((lvA.Phase - hvA.Phase) * 180 / Math.PI), 0.05);
    }

    [TestMethod]
    public void SampleCounterSkewBeyondPolicy_BlocksPair()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot("hv", 100, 2, timestamp, 0);
        var lv = Snapshot("lv", 103, 2, timestamp.AddMilliseconds(0.5), 13.5);

        var pair = TransformerProcessBusAdapter.AlignSnapshots(hv, lv);

        Assert.IsFalse(pair.IsAligned);
        Assert.AreEqual("PAIR_SMPCNT_SKEW", pair.Diagnostics.Code);
    }

    [TestMethod]
    public void UnsynchronizedStream_BlocksPairByDefault()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot("hv", 100, 2, timestamp, 0);
        var lv = Snapshot("lv", 100, 0, timestamp, 0);

        var pair = TransformerProcessBusAdapter.AlignSnapshots(hv, lv);

        Assert.IsFalse(pair.IsAligned);
        Assert.AreEqual("PAIR_NOT_SYNCHRONIZED", pair.Diagnostics.Code);
    }

    [TestMethod]
    public void ControlledLabPolicy_AllowsMatchingLocalSynchronization()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot("hv", 100, 1, timestamp, 0);
        var lv = Snapshot("lv", 100, 1, timestamp, 0);
        var settings = new TransformerSvPairingSettings { RequireGlobalSynchronization = false };

        var pair = TransformerProcessBusAdapter.AlignSnapshots(hv, lv, settings);

        Assert.IsTrue(pair.IsAligned, pair.Diagnostics.Detail);
    }

    [TestMethod]
    public void RepeatedCounterFromStaleStream_IsRejectedByCaptureSkewGuard()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot("hv", 100, 2, timestamp, 0);
        var lv = Snapshot("lv", 100, 2, timestamp.AddSeconds(-1), 0);

        var pair = TransformerProcessBusAdapter.AlignSnapshots(hv, lv);

        Assert.IsFalse(pair.IsAligned);
        Assert.AreEqual("PAIR_CAPTURE_SKEW", pair.Diagnostics.Code);
    }

    [TestMethod]
    public void CurrentOnlyPair_DoesNotPretendCalculatedResidualIsIndependentNeutralCt()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var pair = TransformerProcessBusAdapter.AlignSnapshots(
            Snapshot("hv", 100, 2, timestamp, 0),
            Snapshot("lv", 100, 2, timestamp, 0));

        Assert.IsTrue(pair.IsAligned);
        Assert.IsNotNull(pair.Measurement);
        Assert.IsFalse(pair.Measurement.HighVoltage.NeutralCurrentAvailable);
        Assert.IsFalse(pair.Measurement.LowVoltage.NeutralCurrentAvailable);
    }

    [TestMethod]
    public void DifferentSamplesPerCycle_BlocksPair()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot("hv", 100, 2, timestamp, 0, samplesPerCycle: 80);
        var lv = Snapshot("lv", 100, 2, timestamp, 0, samplesPerCycle: 96);

        var pair = TransformerProcessBusAdapter.AlignSnapshots(hv, lv);

        Assert.IsFalse(pair.IsAligned);
        Assert.AreEqual("PAIR_SAMPLE_RATE_MISMATCH", pair.Diagnostics.Code);
    }

    private static SmvRuntimeSnapshot Snapshot(
        string key,
        ushort sampleCounter,
        byte sampleSynchronization,
        DateTimeOffset timestamp,
        double phaseAdvanceDegrees,
        int samplesPerCycle = 80)
    {
        var phaseA = BuildSine(samplesPerCycle * 2, 1, phaseAdvanceDegrees);
        var phaseB = BuildSine(samplesPerCycle * 2, 1, phaseAdvanceDegrees - 120);
        var phaseC = BuildSine(samplesPerCycle * 2, 1, phaseAdvanceDegrees + 120);
        var residual = new double[samplesPerCycle * 2];
        var trust = SmvTrustState.Healthy;
        var measurement = new MeasurementFrame(timestamp, 1, 1, 1, 0, trust);

        return new SmvRuntimeSnapshot(
            new SmvStreamInfo(key, 0x4000, key, "01-0C-CD-04-00-01", null, "GOOD", true),
            timestamp,
            measurement,
            ProtectionSnapshot.Ready(timestamp),
            new SmvWaveformWindow(phaseA, phaseB, phaseC, residual, 50, samplesPerCycle),
            sampleCounter,
            sampleSynchronization,
            samplesPerCycle,
            samplesPerCycle * 50,
            1000,
            1000,
            0,
            0,
            0,
            0,
            true,
            "resolved",
            "current-only",
            "HEALTHY",
            "SCL",
            "test",
            Array.Empty<string>());
    }

    private static double[] BuildSine(int count, double rms, double angleDegrees)
    {
        var values = new double[count];
        var phase = angleDegrees * Math.PI / 180;
        for (var index = 0; index < count; index++)
            values[index] = rms * Math.Sqrt(2) * Math.Sin(2 * Math.PI * index / 80 + phase);
        return values;
    }

    private static double NormalizeDegrees(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle <= -180) angle += 360;
        return angle;
    }
}
