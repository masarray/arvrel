using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class CtSaturationModelTests
{
    private const double SampleRateHz = 4_000;
    private const double FrequencyHz = 50;

    [TestMethod]
    public void DisabledModel_IsAnExactPassThrough()
    {
        var ideal = GenerateCurrent(5, 20, dcOffsetPercent: 80, dcTimeConstantMilliseconds: 45);

        var result = CtSaturationModel.Apply(
            ideal,
            SampleRateHz,
            FrequencyHz,
            CtSaturationSettings.Disabled);

        CollectionAssert.AreEqual(ideal, result.SecondaryCurrentA);
        Assert.IsFalse(result.Diagnostics.Enabled);
        Assert.IsFalse(result.Diagnostics.Saturated);
    }

    [TestMethod]
    public void BelowKneeOperation_PreservesCurrentWithNegligibleError()
    {
        var ideal = GenerateCurrent(1, 0);
        var settings = CtSaturationSettings.ProtectionClass5AStudy with
        {
            KneePointVoltageRms = 100,
            SecondaryWindingResistanceOhm = 0.5,
            BurdenResistanceOhm = 1,
            BurdenInductanceMilliHenries = 0.1,
            RemanencePercent = 0
        };

        var result = CtSaturationModel.Apply(ideal, SampleRateHz, FrequencyHz, settings);

        Assert.IsTrue(result.Diagnostics.Enabled);
        Assert.IsFalse(result.Diagnostics.Saturated);
        Assert.IsTrue(result.Diagnostics.MaximumAbsoluteFluxPerUnit < 0.1);
        Assert.IsTrue(Math.Abs(result.Diagnostics.RmsMagnitudeErrorPercent) < 0.05);
        Assert.IsTrue(result.Diagnostics.WaveformErrorPercent < 0.05);
    }

    [TestMethod]
    public void AsymmetricalFault_HighBurdenAndRemanenceProduceSaturation()
    {
        var ideal = GenerateCurrent(20, 0, dcOffsetPercent: 100, dcTimeConstantMilliseconds: 60);
        var settings = SaturatingSettings(burdenResistanceOhm: 3.5);

        var result = CtSaturationModel.Apply(ideal, SampleRateHz, FrequencyHz, settings);

        Assert.IsTrue(result.Diagnostics.Saturated);
        Assert.IsTrue(result.Diagnostics.FirstSaturatedSample >= 0);
        Assert.IsTrue(result.Diagnostics.FirstSaturationMilliseconds < 5);
        Assert.IsTrue(result.Diagnostics.MaximumAbsoluteFluxPerUnit > 1.2);
        Assert.IsTrue(result.Diagnostics.RmsMagnitudeErrorPercent < -20);
        Assert.IsTrue(result.Diagnostics.WaveformErrorPercent > 30);
        Assert.IsTrue(result.Diagnostics.MinimumMagnitudeRatio < 0.5);
    }

    [TestMethod]
    public void HigherBurden_AdvancesSaturationAndIncreasesRatioError()
    {
        var ideal = GenerateCurrent(20, 0, dcOffsetPercent: 100, dcTimeConstantMilliseconds: 60);
        var lowBurden = CtSaturationModel.Apply(
            ideal,
            SampleRateHz,
            FrequencyHz,
            SaturatingSettings(burdenResistanceOhm: 0.2));
        var highBurden = CtSaturationModel.Apply(
            ideal,
            SampleRateHz,
            FrequencyHz,
            SaturatingSettings(burdenResistanceOhm: 3.5));

        Assert.IsTrue(lowBurden.Diagnostics.Saturated);
        Assert.IsTrue(highBurden.Diagnostics.Saturated);
        Assert.IsTrue(
            highBurden.Diagnostics.FirstSaturatedSample < lowBurden.Diagnostics.FirstSaturatedSample,
            "Greater secondary burden must drive the core to knee flux earlier for the same asymmetrical current.");
        Assert.IsTrue(
            highBurden.Diagnostics.RmsMagnitudeErrorPercent < lowBurden.Diagnostics.RmsMagnitudeErrorPercent,
            "Greater burden must produce a larger negative RMS ratio error in this deterministic study case.");
    }

    [TestMethod]
    public void SaturationPreset_DistortsRelayCurrentAndPublishesDiagnostics()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var frame = VirtualInjectionGenerator.Generate(
            profile,
            DateTimeOffset.UtcNow,
            SmvTrustState.Healthy);

        Assert.IsTrue(profile.CurrentTransformer.Enabled);
        Assert.AreEqual(100, profile.PhaseACurrent.DcOffsetPercent, 1e-9);
        Assert.IsTrue(frame.CtSaturation.Enabled);
        Assert.IsTrue(frame.CtSaturation.Saturated);
        Assert.IsTrue(frame.CtSaturation.PhaseA.Saturated);
        Assert.IsTrue(frame.CtSaturation.PhaseA.WaveformErrorPercent > 30);
        Assert.IsTrue(frame.Measurement.Phasors!.PhaseACurrent.Magnitude < 15);
        CollectionAssert.AreEqual(
            frame.ResidualCurrentSamples,
            frame.PhaseACurrentSamples
                .Zip(frame.PhaseBCurrentSamples, (phaseA, phaseB) => phaseA + phaseB)
                .Zip(frame.PhaseCCurrentSamples, (phaseAB, phaseC) => phaseAB + phaseC)
                .ToArray());
    }

    [TestMethod]
    public void Fingerprint_CoversCtParametersAndFaultAsymmetry()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var changedBurden = profile with
        {
            CurrentTransformer = profile.CurrentTransformer with
            {
                BurdenResistanceOhm = profile.CurrentTransformer.BurdenResistanceOhm + 0.1
            }
        };
        var changedOffset = profile with
        {
            PhaseACurrent = profile.PhaseACurrent with
            {
                DcOffsetPercent = profile.PhaseACurrent.DcOffsetPercent - 5
            }
        };

        Assert.AreNotEqual(profile.Fingerprint(), changedBurden.Fingerprint());
        Assert.AreNotEqual(profile.Fingerprint(), changedOffset.Fingerprint());
    }

    [TestMethod]
    public void InvalidExcitationCurve_IsRejected()
    {
        var invalid = CtSaturationSettings.ProtectionClass5AStudy with
        {
            ExcitationCurrentAtKneeA = 2,
            ExcitationCurrentAtTwiceKneeA = 1
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => invalid.Validate());
    }

    private static CtSaturationSettings SaturatingSettings(double burdenResistanceOhm)
        => new()
        {
            Enabled = true,
            RatedSecondaryCurrentA = 5,
            KneePointVoltageRms = 70,
            SecondaryWindingResistanceOhm = 1,
            BurdenResistanceOhm = burdenResistanceOhm,
            BurdenInductanceMilliHenries = 0.2,
            ExcitationCurrentAtKneeA = 0.03,
            ExcitationCurrentAtTwiceKneeA = 50,
            SaturationExponent = 2,
            RemanencePercent = 60,
            MaximumFluxPerUnit = 2.5,
            MaximumExcitationCurrentA = 200
        };

    private static double[] GenerateCurrent(
        double rms,
        double angleDegrees,
        double dcOffsetPercent = 0,
        double dcTimeConstantMilliseconds = 60,
        int sampleCount = 160)
    {
        var result = new double[sampleCount];
        var peak = rms * Math.Sqrt(2);
        var phase = angleDegrees * Math.PI / 180;
        var dcFraction = dcOffsetPercent / 100d;
        var dcTimeConstantSeconds = dcTimeConstantMilliseconds / 1_000d;
        for (var index = 0; index < sampleCount; index++)
        {
            var time = index / SampleRateHz;
            result[index] = peak * Math.Cos(2 * Math.PI * FrequencyHz * time + phase) +
                peak * dcFraction * Math.Exp(-time / dcTimeConstantSeconds);
        }
        return result;
    }
}