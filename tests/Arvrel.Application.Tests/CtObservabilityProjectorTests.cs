using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class CtObservabilityProjectorTests
{
    [TestMethod]
    public void DisabledCtProjectsIdealPublicStatus()
    {
        var profile = VirtualInjectionPresets.Create("Normal balanced");

        var result = CtObservabilityProjector.Project(
            profile,
            CtSaturationFrameDiagnostics.Disabled,
            CtSaturationRuntimeState.Empty,
            sourceSampleIndex: 0,
            isRunning: false);

        Assert.AreEqual(CtObservabilityStatus.Ideal, result.Status);
        Assert.AreEqual("CT IDEAL", result.BadgeText);
        Assert.IsFalse(result.IsEnabled);
        Assert.IsFalse(result.CanResetState);
        Assert.AreEqual(4, result.Channels.Count);
        Assert.AreEqual("3I0", result.Channels[3].Channel);
        Assert.AreEqual("CALCULATED SUM", result.Channels[3].State);
    }

    [TestMethod]
    public void SaturatingPresetProjectsPerChannelPublicDiagnostics()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var runtime = new VirtualInjectionRuntime(profile, initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();
        var runtimeSnapshot = runtime.Advance(TimeSpan.FromMilliseconds(5), trustDegraded: false);

        var result = CtObservabilityProjector.Project(
            profile,
            runtimeSnapshot.Frame.CtSaturation,
            runtimeSnapshot.CurrentTransformerState,
            runtimeSnapshot.SourceSampleIndex,
            runtimeSnapshot.IsRunning);

        Assert.AreEqual(CtObservabilityStatus.Saturated, result.Status);
        Assert.AreEqual("CT SAT", result.BadgeText);
        Assert.IsTrue(result.IsEnabled);
        Assert.IsTrue(result.IsRunning);
        Assert.IsTrue(result.CanResetState);
        StringAssert.Contains(result.SettingsSummary, "Vk 70");
        StringAssert.Contains(result.RuntimeSummary, "source sample 20");

        var phaseA = result.Channels.Single(channel => channel.Channel == "IA");
        Assert.IsTrue(phaseA.Enabled);
        Assert.IsTrue(phaseA.Saturated);
        Assert.IsTrue(phaseA.MaximumAbsoluteFluxPerUnit >= 1);
        Assert.IsTrue(phaseA.WaveformErrorPercent > 0);
    }

    [TestMethod]
    public void StoppedConfiguredCtRemainsVisibleButControlsAreDisabled()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");

        var result = CtObservabilityProjector.Project(
            profile,
            CtSaturationFrameDiagnostics.Disabled,
            CtSaturationRuntimeState.Empty,
            sourceSampleIndex: 0,
            isRunning: false);

        Assert.AreEqual(CtObservabilityStatus.Nonlinear, result.Status);
        Assert.AreEqual("CT NONLINEAR", result.BadgeText);
        Assert.IsTrue(result.IsEnabled);
        Assert.IsFalse(result.CanResetState);
        Assert.IsFalse(result.CanDemagnetize);
        StringAssert.Contains(result.RuntimeSummary, "STOPPED");
    }

    [TestMethod]
    public void DirectEditorMergePreservesCtAndHiddenDcParameters()
    {
        var active = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var edited = Enum.GetValues<VirtualInjectionSignal>()
            .ToDictionary(
                signal => signal,
                signal => active.Channel(signal) with
                {
                    Rms = active.Channel(signal).Rms + (signal == VirtualInjectionSignal.PhaseACurrent ? 1 : 0),
                    AngleDegrees = active.Channel(signal).AngleDegrees + 5,
                    DcOffsetPercent = 0,
                    DcTimeConstantMilliseconds = 10
                });

        var result = VirtualInjectionDirectEditorMerge.Apply(active, "Custom CT study", 55, edited);

        Assert.AreEqual(active.CurrentTransformer, result.CurrentTransformer);
        Assert.AreEqual(active.PhaseACurrent.DcOffsetPercent, result.PhaseACurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(active.PhaseACurrent.DcTimeConstantMilliseconds, result.PhaseACurrent.DcTimeConstantMilliseconds, 1e-12);
        Assert.AreEqual(active.PhaseACurrent.Rms + 1, result.PhaseACurrent.Rms, 1e-12);
        Assert.AreEqual(55, result.FrequencyHz, 1e-12);
    }
}
