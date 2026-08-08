using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class TransformerCtSaturationProcessBusTests
{
    private const int SamplesPerCycle = 80;

    [TestMethod]
    public void AlignedPair_CarriesCtWaveformEvidenceFromRawSecondarySamples()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var highVoltageA = Sine(1.0);
        for (var index = 0; index < highVoltageA.Length; index++)
            highVoltageA[index] = Math.Min(highVoltageA[index], 0.45);
        var lowVoltageA = Sine(1.0);
        var zero = new double[SamplesPerCycle];

        var pair = TransformerHarmonicProcessBusAdapter.AlignAndEstimate(
            Snapshot("hv", timestamp, highVoltageA, zero, zero),
            Snapshot("lv", timestamp.AddMilliseconds(0.2), lowVoltageA, zero, zero));

        Assert.IsTrue(pair.IsAligned, pair.Diagnostics.Detail);
        Assert.IsNotNull(pair.Measurement);
        var highVoltageEvidence = pair.Measurement.HighVoltage.CtSaturationEvidence.PhaseA;
        var lowVoltageEvidence = pair.Measurement.LowVoltage.CtSaturationEvidence.PhaseA;
        Assert.IsTrue(highVoltageEvidence.Reliable);
        Assert.IsTrue(highVoltageEvidence.DistortionRatio > 0.25);
        Assert.IsTrue(highVoltageEvidence.PeakAsymmetry > 0.30);
        Assert.IsTrue(lowVoltageEvidence.DistortionRatio < 1e-10);
        StringAssert.Contains(pair.Diagnostics.Detail, "CT waveform evidence available");
    }

    [TestMethod]
    public void LowCurrentPhase_IsMarkedUnreliableWithoutInvalidatingOtherwiseValidPair()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var sine = Sine(1.0);
        var zero = new double[SamplesPerCycle];

        var pair = TransformerHarmonicProcessBusAdapter.AlignAndEstimate(
            Snapshot("hv", timestamp, sine, zero, zero),
            Snapshot("lv", timestamp, sine, zero, zero));

        Assert.IsTrue(pair.IsAligned, pair.Diagnostics.Detail);
        Assert.IsNotNull(pair.Measurement);
        Assert.IsFalse(pair.Measurement.HighVoltage.CtSaturationEvidence.PhaseB.Reliable);
        Assert.AreEqual(0, pair.Measurement.HighVoltage.CtSaturationEvidence.PhaseB.DistortionRatio);
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

    private static double[] Sine(double rms)
    {
        var samples = new double[SamplesPerCycle];
        for (var index = 0; index < samples.Length; index++)
            samples[index] = Math.Sqrt(2) * rms * Math.Sin(2 * Math.PI * index / SamplesPerCycle);
        return samples;
    }
}
