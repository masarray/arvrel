using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class TransformerProtectionRuntimeTests
{
    private const int SamplesPerCycle = 80;

    [TestMethod]
    public void ThroughCurrent_RemainsStableAfterEngineeringCompensation()
    {
        var runtime = new TransformerProtectionRuntime(Configuration());
        var timestamp = DateTimeOffset.UtcNow;

        var result = runtime.EvaluateSnapshots(
            Snapshot("hv", 100, timestamp, phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 100, timestamp, phaseA: Waveform(1, 0, 0, 180)),
            ProcessBusSourceMode.PcapReplay);

        Assert.AreEqual(TransformerRuntimeState.Ready, result.State);
        Assert.IsNotNull(result.Protection);
        Assert.IsFalse(result.Protection.TripRequested);
        Assert.IsFalse(result.Protection.TripLatched);
        var phaseA = result.Protection.Differential.Phases.Single(phase => phase.Phase == TransformerPhase.A);
        Assert.AreEqual(0, phaseA.OperatingCurrentPu, 0.02);
        Assert.AreEqual(1, phaseA.RestraintCurrentPu, 0.02);
    }

    [TestMethod]
    public void SustainedInternalFault_OperatesAndLatchesVirtualTrip()
    {
        var runtime = new TransformerProtectionRuntime(Configuration(operateDelay: TimeSpan.FromMilliseconds(20)));
        var start = DateTimeOffset.UtcNow;

        var first = runtime.EvaluateSnapshots(
            Snapshot("hv", 100, start, phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 100, start, phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.LiveCapture);
        var second = runtime.EvaluateSnapshots(
            Snapshot("hv", 101, start.AddMilliseconds(25), phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 101, start.AddMilliseconds(25), phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.LiveCapture);

        Assert.AreEqual(TransformerRuntimeState.Pickup, first.State);
        Assert.IsFalse(first.TripLatched);
        Assert.AreEqual(TransformerRuntimeState.TripLatched, second.State);
        Assert.IsTrue(second.TripRequested);
        Assert.IsTrue(second.TripLatched);
        Assert.AreEqual("87T", second.Protection?.ActiveElement);
    }

    [TestMethod]
    public void ReReadingSamePair_DoesNotAdvanceDefiniteTimePickup()
    {
        var runtime = new TransformerProtectionRuntime(Configuration(operateDelay: TimeSpan.FromMilliseconds(20)));
        var timestamp = DateTimeOffset.UtcNow;
        var hv = Snapshot("hv", 100, timestamp, phaseA: Waveform(1, 0, 0, 0));
        var lv = Snapshot("lv", 100, timestamp, phaseA: Waveform(1, 0, 0, 0));

        var first = runtime.EvaluateSnapshots(hv, lv, ProcessBusSourceMode.PcapReplay);
        var repeated = runtime.EvaluateSnapshots(hv, lv, ProcessBusSourceMode.PcapReplay);

        Assert.IsTrue(first.EvaluatedNewPair);
        Assert.IsFalse(repeated.EvaluatedNewPair);
        Assert.IsNotNull(first.Protection);
        Assert.IsNotNull(repeated.Protection);
        Assert.AreEqual(
            first.Protection.Differential.Restrained87T.Progress,
            repeated.Protection.Differential.Restrained87T.Progress,
            1e-12);
        Assert.IsFalse(repeated.TripLatched);
    }

    [TestMethod]
    public void InvalidPair_ResetsPreTripTimingSoGapCannotBridgeToOperate()
    {
        var runtime = new TransformerProtectionRuntime(Configuration(operateDelay: TimeSpan.FromMilliseconds(20)));
        var start = DateTimeOffset.UtcNow;

        _ = runtime.EvaluateSnapshots(
            Snapshot("hv", 100, start, phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 100, start, phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.PcapReplay);
        var timing = runtime.EvaluateSnapshots(
            Snapshot("hv", 101, start.AddMilliseconds(10), phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 101, start.AddMilliseconds(10), phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.PcapReplay);
        var blocked = runtime.EvaluateSnapshots(
            Snapshot("hv", 102, start.AddMilliseconds(12), phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 110, start.AddMilliseconds(12), phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.PcapReplay);
        var recovered = runtime.EvaluateSnapshots(
            Snapshot("hv", 103, start.AddMilliseconds(40), phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 103, start.AddMilliseconds(40), phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.PcapReplay);
        var operated = runtime.EvaluateSnapshots(
            Snapshot("hv", 104, start.AddMilliseconds(65), phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 104, start.AddMilliseconds(65), phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.PcapReplay);

        Assert.AreEqual(TransformerRuntimeState.Pickup, timing.State);
        Assert.AreEqual(TransformerRuntimeState.PairBlocked, blocked.State);
        Assert.AreEqual("PAIR_SMPCNT_SKEW", blocked.Pairing.Code);
        Assert.AreEqual(TransformerRuntimeState.Pickup, recovered.State);
        Assert.IsNotNull(recovered.Protection);
        Assert.AreEqual(0, recovered.Protection.Differential.Restrained87T.Progress, 1e-12);
        Assert.AreEqual(TransformerRuntimeState.TripLatched, operated.State);
    }

    [TestMethod]
    public void HarmonicEvidenceFromWaveform_BlocksInrushLikeDifferentialCurrent()
    {
        var runtime = new TransformerProtectionRuntime(Configuration(operateDelay: TimeSpan.Zero));
        var timestamp = DateTimeOffset.UtcNow;

        var result = runtime.EvaluateSnapshots(
            Snapshot("hv", 100, timestamp, phaseA: Waveform(1, 0.20, 0, 0)),
            Snapshot("lv", 100, timestamp, phaseA: Waveform(0, 0, 0, 0)),
            ProcessBusSourceMode.PcapReplay);

        Assert.IsNotNull(result.Protection);
        var phaseA = result.Protection.Differential.Phases.Single(phase => phase.Phase == TransformerPhase.A);
        Assert.AreEqual(0.20, result.Measurement?.HighVoltage.SecondHarmonicRatio.PhaseA ?? 0, 1e-9);
        Assert.IsTrue(phaseA.HarmonicBlocked);
        Assert.IsFalse(result.TripRequested);
        Assert.IsFalse(result.TripLatched);
    }

    [TestMethod]
    public void TripBlockedTrust_PreservesOperateEvidenceButSuppressesVirtualTripRequest()
    {
        var runtime = new TransformerProtectionRuntime(Configuration(operateDelay: TimeSpan.Zero));
        var timestamp = DateTimeOffset.UtcNow;
        var trust = SmvTrustState.TripBlocked("QUALITY_INVALID", "test quality block");

        var result = runtime.EvaluateSnapshots(
            Snapshot("hv", 100, timestamp, phaseA: Waveform(1, 0, 0, 0), trust: trust),
            Snapshot("lv", 100, timestamp, phaseA: Waveform(1, 0, 0, 0), trust: trust),
            ProcessBusSourceMode.LiveCapture);

        Assert.AreEqual(TransformerRuntimeState.ProtectionBlocked, result.State);
        Assert.IsNotNull(result.Protection);
        Assert.IsTrue(result.Protection.Differential.Restrained87T.Operated);
        Assert.IsTrue(result.Protection.Blocked);
        Assert.IsFalse(result.TripRequested);
        Assert.IsFalse(result.TripLatched);
    }

    [TestMethod]
    public void LatchedTrip_RemainsFrozenAcrossLaterValidAndInvalidInputUntilReset()
    {
        var runtime = new TransformerProtectionRuntime(Configuration(operateDelay: TimeSpan.Zero));
        var start = DateTimeOffset.UtcNow;

        var tripped = runtime.EvaluateSnapshots(
            Snapshot("hv", 100, start, phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 100, start, phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.LiveCapture);
        var throughAfterTrip = runtime.EvaluateSnapshots(
            Snapshot("hv", 101, start.AddMilliseconds(1), phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 101, start.AddMilliseconds(1), phaseA: Waveform(1, 0, 0, 180)),
            ProcessBusSourceMode.LiveCapture);
        var invalidAfterTrip = runtime.EvaluateSnapshots(
            Snapshot("hv", 102, start.AddMilliseconds(2), phaseA: Waveform(1, 0, 0, 0)),
            Snapshot("lv", 120, start.AddMilliseconds(2), phaseA: Waveform(1, 0, 0, 180)),
            ProcessBusSourceMode.LiveCapture);

        Assert.IsTrue(tripped.TripLatched);
        Assert.AreEqual(TransformerRuntimeState.TripLatched, throughAfterTrip.State);
        Assert.IsTrue(throughAfterTrip.TripLatched);
        Assert.AreEqual(tripped.Protection?.Timestamp, throughAfterTrip.Protection?.Timestamp);
        Assert.AreEqual(TransformerRuntimeState.TripLatched, invalidAfterTrip.State);
        Assert.IsTrue(invalidAfterTrip.TripLatched);

        runtime.Reset();
        Assert.IsFalse(runtime.CurrentSnapshot.TripLatched);
        Assert.AreEqual(TransformerRuntimeState.WaitingForPair, runtime.CurrentSnapshot.State);
    }

    [TestMethod]
    public void EngineeringPlan_IsAppliedAndCapturedInEvidence()
    {
        var configuration = Configuration(operateDelay: TimeSpan.Zero);
        var runtime = new TransformerProtectionRuntime(configuration);
        var timestamp = DateTimeOffset.UtcNow;
        var result = runtime.EvaluateSnapshots(
            Snapshot("hv", 100, timestamp, phaseA: Waveform(1, 0, 0, 180)),
            Snapshot("lv", 100, timestamp, phaseA: Waveform(1, 0, 0, 0)),
            ProcessBusSourceMode.PcapReplay);

        var evidence = runtime.CaptureEvidence();

        Assert.AreEqual("Yyn0", evidence.VectorGroup.Name);
        Assert.AreEqual(ProcessBusSourceMode.PcapReplay, evidence.SourceMode);
        Assert.AreEqual(result.EffectiveSettingsFingerprint, evidence.EffectiveSettingsFingerprint);
        Assert.AreEqual(1, evidence.EffectiveSettings.Differential87T.HighVoltageCompensation.CurrentScaleToPu, 1e-12);
        Assert.AreEqual(1, evidence.EffectiveSettings.Differential87T.LowVoltageCompensation.CurrentScaleToPu, 1e-12);
        Assert.IsNotNull(evidence.PairIdentity);
        Assert.IsNotNull(evidence.Measurement);
        Assert.IsNotNull(evidence.Protection);
    }

    [TestMethod]
    public void SameStreamConfiguration_IsRejectedBeforeRuntimeStarts()
    {
        var configuration = Configuration() with { LowVoltageStreamKey = "hv" };

        Assert.ThrowsExactly<ArgumentException>(() => new TransformerProtectionRuntime(configuration));
    }

    private static TransformerProtectionRuntimeConfiguration Configuration(TimeSpan? operateDelay = null)
    {
        const double ratedPowerMva = 10;
        const double highVoltageKv = 100;
        const double lowVoltageKv = 10;
        var highVoltageRatedCurrent = RatedCurrent(ratedPowerMva, highVoltageKv);
        var lowVoltageRatedCurrent = RatedCurrent(ratedPowerMva, lowVoltageKv);

        return new TransformerProtectionRuntimeConfiguration(
            "hv",
            "lv",
            new TransformerNameplate(ratedPowerMva, highVoltageKv, lowVoltageKv, "Yyn0"),
            new TransformerWindingEngineering(new TransformerCtRatio(highVoltageRatedCurrent, 1)),
            new TransformerWindingEngineering(new TransformerCtRatio(lowVoltageRatedCurrent, 1)),
            new TransformerProtectionSettings
            {
                Differential87T = new TransformerDifferentialSettings
                {
                    Enabled = true,
                    MinimumPickupPu = 0.20,
                    Slope1 = 0.20,
                    Slope2 = 0.60,
                    SlopeBreakpointPu = 2,
                    OperateDelay = operateDelay ?? TimeSpan.FromMilliseconds(20),
                    HarmonicSecurityMode = TransformerHarmonicSecurityMode.Blocking,
                    SecondHarmonicThreshold = 0.15,
                    FifthHarmonicThreshold = 0.35
                }
            });
    }

    private static SmvRuntimeSnapshot Snapshot(
        string key,
        ushort sampleCounter,
        DateTimeOffset timestamp,
        double[] phaseA,
        double[]? phaseB = null,
        double[]? phaseC = null,
        SmvTrustState? trust = null)
    {
        phaseB ??= Waveform(0, 0, 0, -120);
        phaseC ??= Waveform(0, 0, 0, 120);
        var residual = new double[phaseA.Length];
        for (var index = 0; index < residual.Length; index++)
            residual[index] = phaseA[index] + phaseB[index] + phaseC[index];

        var sourceTrust = trust ?? SmvTrustState.Healthy;
        var measurement = new MeasurementFrame(timestamp, 0, 0, 0, 0, sourceTrust);
        return new SmvRuntimeSnapshot(
            new SmvStreamInfo(key, 0x4000, key, "01-0C-CD-04-00-01", null, "GOOD", true),
            timestamp,
            measurement,
            ProtectionSnapshot.Ready(timestamp),
            new SmvWaveformWindow(phaseA, phaseB, phaseC, residual, 50, SamplesPerCycle),
            sampleCounter,
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
            $"{sourceTrust.Code} · {sourceTrust.Detail}",
            "SCL",
            "test",
            Array.Empty<string>());
    }

    private static double[] Waveform(
        double fundamentalRms,
        double secondRatio,
        double fifthRatio,
        double fundamentalDegrees)
    {
        var result = new double[SamplesPerCycle * 2];
        var fundamentalPhase = fundamentalDegrees * Math.PI / 180;
        var secondPhase = (fundamentalDegrees + 23) * Math.PI / 180;
        var fifthPhase = (fundamentalDegrees - 41) * Math.PI / 180;
        for (var index = 0; index < result.Length; index++)
        {
            var angle = 2 * Math.PI * index / SamplesPerCycle;
            result[index] = Math.Sqrt(2) * (
                fundamentalRms * Math.Sin(angle + fundamentalPhase) +
                fundamentalRms * secondRatio * Math.Sin(2 * angle + secondPhase) +
                fundamentalRms * fifthRatio * Math.Sin(5 * angle + fifthPhase));
        }
        return result;
    }

    private static double RatedCurrent(double ratedPowerMva, double ratedVoltageKv)
        => ratedPowerMva * 1_000_000 / (Math.Sqrt(3) * ratedVoltageKv * 1_000);
}
