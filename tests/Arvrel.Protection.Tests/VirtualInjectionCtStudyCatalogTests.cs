using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualInjectionCtStudyCatalogTests
{
    [TestMethod]
    public void CtModelPresetsAreUniqueValidAndRoundTripByName()
    {
        Assert.AreEqual(
            VirtualInjectionCtModelPresets.Names.Count,
            VirtualInjectionCtModelPresets.Names.Distinct(StringComparer.Ordinal).Count());

        foreach (var name in VirtualInjectionCtModelPresets.Names)
        {
            var settings = VirtualInjectionCtModelPresets.Create(name);
            settings.Validate();
            Assert.AreEqual(name, VirtualInjectionCtModelPresets.ResolveName(settings), name);
        }
    }

    [TestMethod]
    public void AdditionalCtScenariosCoverBPhaseCPhaseAndThreePhaseStudies()
    {
        var expected = new[]
        {
            VirtualInjectionCtStudyScenarios.PhaseBAsymmetrical,
            VirtualInjectionCtStudyScenarios.PhaseCAsymmetrical,
            VirtualInjectionCtStudyScenarios.ThreePhaseHighBurden
        };

        CollectionAssert.AreEquivalent(expected, VirtualInjectionCtStudyScenarios.AdditionalNames.ToArray());
        foreach (var name in expected)
        {
            var profile = VirtualInjectionCtStudyScenarios.Create(name, 50);
            profile.Validate();
            Assert.IsTrue(profile.CurrentTransformer.Enabled, name);
            Assert.AreEqual(
                VirtualInjectionCtModelPresets.HighBurdenPositiveRemanence,
                VirtualInjectionCtModelPresets.ResolveName(profile.CurrentTransformer),
                name);
        }
    }

    [TestMethod]
    public void AsymmetricalCtScenariosPutDecayingDcOnSelectedFaultPhase()
    {
        var phaseB = VirtualInjectionCtStudyScenarios.Create(
            VirtualInjectionCtStudyScenarios.PhaseBAsymmetrical,
            50);
        Assert.AreEqual(0, phaseB.PhaseACurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(100, phaseB.PhaseBCurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(0, phaseB.PhaseCCurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(20, phaseB.PhaseBCurrent.Rms, 1e-12);

        var phaseC = VirtualInjectionCtStudyScenarios.Create(
            VirtualInjectionCtStudyScenarios.PhaseCAsymmetrical,
            50);
        Assert.AreEqual(0, phaseC.PhaseACurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(0, phaseC.PhaseBCurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(100, phaseC.PhaseCCurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(20, phaseC.PhaseCCurrent.Rms, 1e-12);
    }

    [TestMethod]
    public void ThreePhaseCtScenarioUsesBalancedTwentyAmpereSourceWithoutArtificialDc()
    {
        var profile = VirtualInjectionCtStudyScenarios.Create(
            VirtualInjectionCtStudyScenarios.ThreePhaseHighBurden,
            50);

        Assert.AreEqual(20, profile.PhaseACurrent.Rms, 1e-12);
        Assert.AreEqual(20, profile.PhaseBCurrent.Rms, 1e-12);
        Assert.AreEqual(20, profile.PhaseCCurrent.Rms, 1e-12);
        Assert.AreEqual(0, profile.PhaseACurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(0, profile.PhaseBCurrent.DcOffsetPercent, 1e-12);
        Assert.AreEqual(0, profile.PhaseCCurrent.DcOffsetPercent, 1e-12);
    }
}
