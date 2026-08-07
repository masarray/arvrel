using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class CtSaturationRuntimeStateTests
{
    private const double SampleRateHz = 4_000;
    private const double FrequencyHz = 50;

    [TestMethod]
    public void SplitProcessingMatchesOneContinuousCtRun()
    {
        var settings = SaturatingSettings();
        var ideal = GenerateCurrent(sampleCount: 320);
        var continuous = CtSaturationModel.Apply(
            ideal,
            SampleRateHz,
            FrequencyHz,
            settings);

        var first = CtSaturationModel.Apply(
            ideal[..160],
            SampleRateHz,
            FrequencyHz,
            settings);
        var second = CtSaturationModel.Apply(
            ideal[160..],
            SampleRateHz,
            FrequencyHz,
            settings,
            first.FinalState);
        var split = first.SecondaryCurrentA.Concat(second.SecondaryCurrentA).ToArray();

        Assert.AreEqual(continuous.SecondaryCurrentA.Length, split.Length);
        for (var index = 0; index < split.Length; index++)
            Assert.AreEqual(continuous.SecondaryCurrentA[index], split[index], 1e-12, $"sample {index}");

        Assert.IsTrue(second.Diagnostics.StateWasCarried);
        Assert.AreEqual(160L, second.Diagnostics.InitialProcessedSampleCount);
        Assert.AreEqual(320L, second.Diagnostics.FinalProcessedSampleCount);
        Assert.AreEqual(
            continuous.FinalState.FluxLinkageVoltSeconds,
            second.FinalState.FluxLinkageVoltSeconds,
            1e-12);
        Assert.AreEqual(
            continuous.FinalState.PreviousSecondaryCurrentA,
            second.FinalState.PreviousSecondaryCurrentA,
            1e-12);
        Assert.AreEqual(
            continuous.FinalState.PreviousSecondaryVoltageV,
            second.FinalState.PreviousSecondaryVoltageV,
            1e-12);
    }

    [TestMethod]
    public void RuntimeCarriesMagneticStateAndContinuousSourceTimeAcrossAdvances()
    {
        var runtime = new VirtualInjectionRuntime(
            VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical"),
            initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();

        var initial = runtime.Advance(TimeSpan.Zero, trustDegraded: false);
        Assert.IsFalse(initial.Frame.CtSaturation.PhaseA.StateWasCarried);
        Assert.AreEqual(0L, initial.CurrentTransformerState.PhaseA.ProcessedSampleCount);
        Assert.AreEqual(0L, initial.SourceSampleIndex);

        var first = runtime.Advance(TimeSpan.FromMilliseconds(5), trustDegraded: false);
        Assert.AreEqual(20L, first.SourceSampleIndex);
        Assert.AreEqual(20L, first.CurrentTransformerState.PhaseA.ProcessedSampleCount);
        Assert.IsTrue(first.Frame.CtSaturation.PhaseA.StateWasCarried);
        Assert.AreEqual(20L, first.Frame.CtSaturation.PhaseA.InitialProcessedSampleCount);

        var second = runtime.Advance(TimeSpan.FromMilliseconds(5), trustDegraded: false);
        Assert.AreEqual(40L, second.SourceSampleIndex);
        Assert.AreEqual(40L, second.CurrentTransformerState.PhaseA.ProcessedSampleCount);
        Assert.AreEqual(40L, second.Frame.CtSaturation.PhaseA.InitialProcessedSampleCount);

        var kneeFlux = Math.Sqrt(2) * runtime.ActiveProfile.CurrentTransformer.KneePointVoltageRms /
            (2 * Math.PI * runtime.ActiveProfile.FrequencyHz);
        Assert.AreEqual(
            second.CurrentTransformerState.PhaseA.FluxLinkageVoltSeconds / kneeFlux,
            second.Frame.CtSaturation.PhaseA.InitialFluxPerUnit,
            1e-9);
        Assert.IsFalse(
            first.Frame.PhaseACurrentSamples.SequenceEqual(second.Frame.PhaseACurrentSamples),
            "A stateful runtime must not restart the same DC-offset waveform and remanent-flux trajectory on every frame.");
    }

    [TestMethod]
    public void SourceEventChangeKeepsStateWhenCtIdentityIsUnchanged()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var runtime = new VirtualInjectionRuntime(profile, initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(10), trustDegraded: false);
        var before = runtime.CurrentTransformerState;

        var changedSource = profile with
        {
            Name = "CT saturation - changed source event",
            PhaseACurrent = profile.PhaseACurrent with
            {
                Rms = 18,
                AngleDegrees = 20,
                DcOffsetPercent = 80
            }
        };

        Assert.IsTrue(runtime.Apply(changedSource));
        var snapshot = runtime.Advance(TimeSpan.Zero, trustDegraded: false);

        Assert.AreEqual(0L, snapshot.SourceSampleIndex);
        Assert.AreEqual(before.PhaseA, snapshot.CurrentTransformerState.PhaseA);
        Assert.IsTrue(snapshot.Frame.CtSaturation.PhaseA.StateWasCarried);
        Assert.AreEqual(
            before.PhaseA.ProcessedSampleCount,
            snapshot.Frame.CtSaturation.PhaseA.InitialProcessedSampleCount);
    }

    [TestMethod]
    public void CtParameterChangeResetsMagneticStateBeforePreview()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var runtime = new VirtualInjectionRuntime(profile, initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(10), trustDegraded: false);
        Assert.IsTrue(runtime.CurrentTransformerState.HasHistory);

        var changedCt = profile with
        {
            Name = "CT saturation - changed burden",
            CurrentTransformer = profile.CurrentTransformer with
            {
                BurdenResistanceOhm = profile.CurrentTransformer.BurdenResistanceOhm + 0.25
            }
        };

        Assert.IsTrue(runtime.Apply(changedCt));
        var snapshot = runtime.Advance(TimeSpan.Zero, trustDegraded: false);

        Assert.IsFalse(snapshot.CurrentTransformerState.HasHistory);
        Assert.AreEqual(0L, snapshot.SourceSampleIndex);
        Assert.IsFalse(snapshot.Frame.CtSaturation.PhaseA.StateWasCarried);
        Assert.AreEqual(0L, snapshot.Frame.CtSaturation.PhaseA.InitialProcessedSampleCount);
        Assert.AreEqual(
            changedCt.CurrentTransformer.RemanencePercent / 100d,
            snapshot.Frame.CtSaturation.PhaseA.InitialFluxPerUnit,
            1e-9);
    }

    [TestMethod]
    public void StopAndResetClearRuntimeHistoryButKeepConfiguredRemanenceForNextStart()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var runtime = new VirtualInjectionRuntime(profile, initialTimestamp: DateTimeOffset.UnixEpoch);
        runtime.Start();
        runtime.Advance(TimeSpan.FromMilliseconds(5), trustDegraded: false);
        Assert.IsTrue(runtime.CurrentTransformerState.HasHistory);

        Assert.IsTrue(runtime.Stop());
        Assert.IsFalse(runtime.CurrentTransformerState.HasHistory);
        Assert.AreEqual(0L, runtime.SourceSampleIndex);
        var stopped = runtime.Advance(TimeSpan.Zero, trustDegraded: false);
        Assert.AreEqual(0, stopped.Frame.Measurement.Phasors!.PhaseACurrent.Magnitude, 0.0001);

        Assert.IsTrue(runtime.Start());
        var restarted = runtime.Advance(TimeSpan.Zero, trustDegraded: false);
        Assert.IsFalse(restarted.Frame.CtSaturation.PhaseA.StateWasCarried);
        Assert.AreEqual(
            profile.CurrentTransformer.RemanencePercent / 100d,
            restarted.Frame.CtSaturation.PhaseA.InitialFluxPerUnit,
            1e-9);

        runtime.Advance(TimeSpan.FromMilliseconds(5), trustDegraded: false);
        runtime.Reset();
        Assert.IsFalse(runtime.CurrentTransformerState.HasHistory);
        Assert.AreEqual(0L, runtime.SourceSampleIndex);
        Assert.IsFalse(runtime.IsRunning);
    }

    private static CtSaturationSettings SaturatingSettings()
        => new()
        {
            Enabled = true,
            RatedSecondaryCurrentA = 5,
            KneePointVoltageRms = 70,
            SecondaryWindingResistanceOhm = 1,
            BurdenResistanceOhm = 3.5,
            BurdenInductanceMilliHenries = 0.2,
            ExcitationCurrentAtKneeA = 0.03,
            ExcitationCurrentAtTwiceKneeA = 50,
            SaturationExponent = 2,
            RemanencePercent = 60,
            MaximumFluxPerUnit = 2.5,
            MaximumExcitationCurrentA = 200
        };

    private static double[] GenerateCurrent(int sampleCount)
    {
        var result = new double[sampleCount];
        var peak = 20 * Math.Sqrt(2);
        var dcTimeConstantSeconds = 0.06;
        for (var index = 0; index < sampleCount; index++)
        {
            var time = index / SampleRateHz;
            result[index] = peak * Math.Cos(2 * Math.PI * FrequencyHz * time) +
                peak * Math.Exp(-time / dcTimeConstantSeconds);
        }
        return result;
    }
}
