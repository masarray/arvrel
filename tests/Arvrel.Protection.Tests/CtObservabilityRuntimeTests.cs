using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class CtObservabilityRuntimeTests
{
    [TestMethod]
    public void IdealReferenceUsesAbsoluteSourceTimeAndCalculatedResidual()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var reference = VirtualInjectionReferenceGenerator.GenerateCurrents(
            profile,
            startSampleIndex: 20,
            sampleCount: 40,
            sampleRateHz: 4_000);

        var time = 20d / 4_000d;
        var peak = profile.PhaseACurrent.Rms * Math.Sqrt(2);
        var expected = peak * Math.Cos(2 * Math.PI * profile.FrequencyHz * time) +
            peak * profile.PhaseACurrent.DcOffsetPercent / 100d *
            Math.Exp(-time / (profile.PhaseACurrent.DcTimeConstantMilliseconds / 1_000d));

        Assert.AreEqual(expected, reference.PhaseA[0], 1e-12);
        for (var index = 0; index < reference.PhaseA.Length; index++)
        {
            Assert.AreEqual(
                reference.PhaseA[index] + reference.PhaseB[index] + reference.PhaseC[index],
                reference.NeutralOrResidual[index],
                1e-12,
                $"sample {index}");
        }
    }

    [TestMethod]
    public void ResetCtStateReappliesRemanenceWithoutRestartingSourceTime()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var runtime = new VirtualInjectionRuntime(profile, initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(5), trustDegraded: false);
        var sourceBefore = runtime.SourceSampleIndex;

        Assert.IsTrue(runtime.ResetCurrentTransformerState(demagnetize: false));

        Assert.AreEqual(sourceBefore, runtime.SourceSampleIndex);
        Assert.AreEqual(0L, runtime.CurrentTransformerState.PhaseA.ProcessedSampleCount);
        var kneeFlux = Math.Sqrt(2) * profile.CurrentTransformer.KneePointVoltageRms /
            (2 * Math.PI * profile.FrequencyHz);
        Assert.AreEqual(
            profile.CurrentTransformer.RemanencePercent / 100d * kneeFlux,
            runtime.CurrentTransformerState.PhaseA.FluxLinkageVoltSeconds,
            1e-12);
        Assert.AreEqual("rebuilding", runtime.WindowStatus);
    }

    [TestMethod]
    public void DemagnetizeZerosFluxWithoutChangingConfigurationOrSourceTime()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var runtime = new VirtualInjectionRuntime(profile, initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(5), trustDegraded: false);
        var fingerprint = runtime.InjectionFingerprint;
        var sourceBefore = runtime.SourceSampleIndex;

        Assert.IsTrue(runtime.ResetCurrentTransformerState(demagnetize: true));

        Assert.AreEqual(fingerprint, runtime.InjectionFingerprint);
        Assert.AreEqual(sourceBefore, runtime.SourceSampleIndex);
        Assert.AreEqual(0, runtime.CurrentTransformerState.PhaseA.FluxLinkageVoltSeconds, 1e-12);
        Assert.AreEqual(0L, runtime.CurrentTransformerState.PhaseA.ProcessedSampleCount);
        Assert.AreEqual(profile.CurrentTransformer.RemanencePercent, runtime.ActiveProfile.CurrentTransformer.RemanencePercent, 1e-12);
    }

    [TestMethod]
    public void CtStateActionsRequireRunningNonlinearModel()
    {
        var stopped = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        Assert.IsFalse(stopped.ResetCurrentTransformerState(demagnetize: false));

        var ideal = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("Normal balanced"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        ideal.Start();
        Assert.IsFalse(ideal.ResetCurrentTransformerState(demagnetize: true));
    }
}
