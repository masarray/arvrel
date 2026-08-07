using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class CtSourceModelSeparationTests
{
    [TestMethod]
    public void OrdinarySourcePresetAndQuickFaultPreserveSelectedCtModel()
    {
        var scenario = new DeterministicLabScenario();
        var selectedCt = VirtualInjectionCtModelPresets.Create(
            VirtualInjectionCtModelPresets.HighBurdenNegativeRemanence);
        scenario.ApplyProfile((scenario.ActiveProfile with
        {
            CurrentTransformer = selectedCt
        }).Normalize());

        scenario.FaultActive = true;
        Assert.AreEqual("A-G fault", scenario.ActiveProfile.Name);
        Assert.AreEqual(selectedCt, scenario.ActiveProfile.CurrentTransformer);

        scenario.FaultActive = false;
        Assert.AreEqual("Normal balanced", scenario.ActiveProfile.Name);
        Assert.AreEqual(selectedCt, scenario.ActiveProfile.CurrentTransformer);

        scenario.ApplyPreset("B-G fault");
        Assert.AreEqual("B-G fault", scenario.ActiveProfile.Name);
        Assert.AreEqual(selectedCt, scenario.ActiveProfile.CurrentTransformer);
    }

    [TestMethod]
    public void ExplicitCtStudyScenarioMaySelectItsDocumentedCtStartingPoint()
    {
        var scenario = new DeterministicLabScenario();

        scenario.ApplyPreset(VirtualInjectionCtStudyScenarios.PhaseCAsymmetrical);

        Assert.AreEqual(VirtualInjectionCtStudyScenarios.PhaseCAsymmetrical, scenario.ActiveProfile.Name);
        Assert.AreEqual(
            VirtualInjectionCtModelPresets.HighBurdenPositiveRemanence,
            VirtualInjectionCtModelPresets.ResolveName(scenario.ActiveProfile.CurrentTransformer));
    }
}
