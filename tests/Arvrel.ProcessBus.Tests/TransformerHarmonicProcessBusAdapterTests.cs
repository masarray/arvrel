using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class TransformerHarmonicProcessBusAdapterTests
{
    private const int SamplesPerCycle = 80;

    [TestMethod]
    public void AlignedPair_IsEnrichedWithMeasuredH2AndH5Ratios()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot(
            "hv",
            timestamp,
            BuildWaveform(2, 1.0, 0.20, 0.35, 0),
            BuildWaveform(2, 1.0, 0.10, 0.05, -120),
            BuildWaveform(2, 1.0, 0.15, 0.12, 120));
        var lv = Snapshot(
            "lv",
            timestamp.AddMilliseconds(0.2),
            BuildWaveform(2, 1.0, 0, 0, 0),
            BuildWaveform(2, 1.0, 0, 0, -120),
            BuildWaveform(2, 1.0, 0, 0, 120));

        var pair = TransformerHarmonicProcessBusAdapter.AlignAndEstimate(hv, lv);

        Assert.IsTrue(pair.IsAligned, pair.Diagnostics.Detail);
        Assert.IsNotNull(pair.Measurement);
        Assert.AreEqual(0.20, pair.Measurement.HighVoltage.SecondHarmonicRatio.PhaseA, 1e-9);
        Assert.AreEqual(0.35, pair.Measurement.HighVoltage.FifthHarmonicRatio.PhaseA, 1e-9);
        Assert.AreEqual(0.10, pair.Measurement.HighVoltage.SecondHarmonicRatio.PhaseB, 1e-9);
        Assert.AreEqual(0.12, pair.Measurement.HighVoltage.FifthHarmonicRatio.PhaseC, 1e-9);
        Assert.AreEqual(0.0, pair.Measurement.LowVoltage.SecondHarmonicRatio.PhaseA, 1e-9);
        StringAssert.Contains(pair.Diagnostics.Detail, "H2/H5 coherent DFT ratios available");
    }

    [TestMethod]
    public void TwoCyclePolicy_WithOnlyOneCycleOfWaveform_BlocksEnrichedPair()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot(
            "hv",
            timestamp,
            BuildWaveform(1, 1.0, 0.20, 0.35, 0),
            BuildWaveform(1, 1.0, 0.20, 0.35, -120),
            BuildWaveform(1, 1.0, 0.20, 0.35, 120));
        var lv = Snapshot(
            "lv",
            timestamp,
            BuildWaveform(1, 1.0, 0, 0, 0),
            BuildWaveform(1, 1.0, 0, 0, -120),
            BuildWaveform(1, 1.0, 0, 0, 120));
        var harmonicSettings = new TransformerHarmonicEstimatorSettings { WindowCycles = 2 };

        var pair = TransformerHarmonicProcessBusAdapter.AlignAndEstimate(
            hv,
            lv,
            harmonicSettings: harmonicSettings);

        Assert.IsFalse(pair.IsAligned);
        Assert.IsNull(pair.Measurement);
        Assert.AreEqual("PAIR_HARMONIC_ESTIMATE_INVALID", pair.Diagnostics.Code);
    }

    [TestMethod]
    public void MeasuredSecondHarmonic_DrivesExisting87THarmonicBlock()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot(
            "hv",
            timestamp,
            BuildWaveform(2, 1.0, 0.20, 0, 0),
            BuildWaveform(2, 0.0, 0, 0, -120),
            BuildWaveform(2, 0.0, 0, 0, 120));
        var lv = Snapshot(
            "lv",
            timestamp,
            BuildWaveform(2, 0.0, 0, 0, 0),
            BuildWaveform(2, 0.0, 0, 0, -120),
            BuildWaveform(2, 0.0, 0, 0, 120));
        var pair = TransformerHarmonicProcessBusAdapter.AlignAndEstimate(hv, lv);
        Assert.IsTrue(pair.IsAligned, pair.Diagnostics.Detail);
        Assert.IsNotNull(pair.Measurement);

        var protectionSettings = new TransformerProtectionSettings
        {
            Differential87T = new TransformerDifferentialSettings
            {
                Enabled = true,
                MinimumPickupPu = 0.20,
                Slope1 = 0.20,
                Slope2 = 0.60,
                SlopeBreakpointPu = 2.0,
                OperateDelay = TimeSpan.Zero,
                HarmonicSecurityMode = TransformerHarmonicSecurityMode.Blocking,
                SecondHarmonicThreshold = 0.15,
                FifthHarmonicThreshold = 0.35
            }
        };
        var engine = new TransformerProtectionEngine(protectionSettings);

        var result = engine.Evaluate(pair.Measurement);

        var phaseA = result.Differential.Phases.Single(phase => phase.Phase == TransformerPhase.A);
        Assert.IsTrue(phaseA.HarmonicBlocked);
        Assert.IsFalse(phaseA.RestrainedOperated);
        Assert.IsFalse(result.TripRequested);
    }

    [TestMethod]
    public void MeasuredFifthHarmonic_DrivesExisting87TOverexcitationBlock()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot(
            "hv",
            timestamp,
            BuildWaveform(2, 1.0, 0, 0.40, 0),
            BuildWaveform(2, 0.0, 0, 0, -120),
            BuildWaveform(2, 0.0, 0, 0, 120));
        var lv = Snapshot(
            "lv",
            timestamp,
            BuildWaveform(2, 0.0, 0, 0, 0),
            BuildWaveform(2, 0.0, 0, 0, -120),
            BuildWaveform(2, 0.0, 0, 0, 120));
        var pair = TransformerHarmonicProcessBusAdapter.AlignAndEstimate(hv, lv);
        Assert.IsTrue(pair.IsAligned, pair.Diagnostics.Detail);
        Assert.IsNotNull(pair.Measurement);

        var protectionSettings = new TransformerProtectionSettings
        {
            Differential87T = new TransformerDifferentialSettings
            {
                Enabled = true,
                MinimumPickupPu = 0.20,
                Slope1 = 0.20,
                Slope2 = 0.60,
                SlopeBreakpointPu = 2.0,
                OperateDelay = TimeSpan.Zero,
                HarmonicSecurityMode = TransformerHarmonicSecurityMode.Blocking,
                SecondHarmonicThreshold = 0.15,
                FifthHarmonicThreshold = 0.35
            }
        };
        var engine = new TransformerProtectionEngine(protectionSettings);

        var result = engine.Evaluate(pair.Measurement);

        var phaseA = result.Differential.Phases.Single(phase => phase.Phase == TransformerPhase.A);
        Assert.IsTrue(phaseA.HarmonicBlocked);
        Assert.IsFalse(result.TripRequested);
    }

    private static SmvRuntimeSnapshot Snapshot(
        string key,
        DateTimeOffset timestamp,
        double[] phaseA,
        double[] phaseB,
        double[] phaseC)
    {
        var residual = new double[phaseA.Length];
        for (var index = 0; index < residual.Length; index++)
            residual[index] = phaseA[index] + phaseB[index] + phaseC[index];

        var trust = SmvTrustState.Healthy;
        var measurement = new MeasurementFrame(timestamp, 1, 1, 1, 0, trust);
        return new SmvRuntimeSnapshot(
            new SmvStreamInfo(key, 0x4000, key, "01-0C-CD-04-00-01", null, "GOOD", true),
            timestamp,
            measurement,
            ProtectionSnapshot.Ready(timestamp),
            new SmvWaveformWindow(phaseA, phaseB, phaseC, residual, 50, SamplesPerCycle),
            100,
            2,
            SamplesPerCycle,
            SamplesPerCycle * 50,
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

    private static double[] BuildWaveform(
        int cycles,
        double fundamentalRms,
        double secondRms,
        double fifthRms,
        double fundamentalDegrees)
    {
        var count = cycles * SamplesPerCycle;
        var result = new double[count];
        var fundamentalPhase = fundamentalDegrees * Math.PI / 180;
        var secondPhase = (fundamentalDegrees + 23) * Math.PI / 180;
        var fifthPhase = (fundamentalDegrees - 41) * Math.PI / 180;

        for (var index = 0; index < count; index++)
        {
            var fundamentalAngle = 2 * Math.PI * index / SamplesPerCycle;
            result[index] = Math.Sqrt(2) * (
                fundamentalRms * Math.Sin(fundamentalAngle + fundamentalPhase) +
                secondRms * Math.Sin(2 * fundamentalAngle + secondPhase) +
                fifthRms * Math.Sin(5 * fundamentalAngle + fifthPhase));
        }

        return result;
    }
}
