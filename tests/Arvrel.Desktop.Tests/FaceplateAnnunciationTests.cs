using Arvrel.Desktop.ViewModels;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class FaceplateAnnunciationTests
{
    [TestMethod]
    public async Task AgFaultLatchesExactPhaseAndEarthCausesUntilRelayReset()
    {
        await using var viewModel = new MainWindowViewModel();

        viewModel.InjectAgFault();
        for (var index = 0; index < 6; index++)
            viewModel.Tick();

        Assert.IsTrue(viewModel.TripLatched);
        Assert.AreEqual(RelayLampState.Trip, viewModel.PhaseAAnnunciation);
        Assert.AreEqual(RelayLampState.Off, viewModel.PhaseBAnnunciation);
        Assert.AreEqual(RelayLampState.Off, viewModel.PhaseCAnnunciation);
        Assert.AreEqual(RelayLampState.Trip, viewModel.EarthAnnunciation);

        viewModel.ClearInjection();

        Assert.IsTrue(viewModel.TripLatched);
        Assert.AreEqual(RelayLampState.Trip, viewModel.PhaseAAnnunciation);
        Assert.AreEqual(RelayLampState.Trip, viewModel.EarthAnnunciation);

        viewModel.ResetRelay();

        Assert.IsFalse(viewModel.TripLatched);
        Assert.AreEqual(RelayLampState.Off, viewModel.PhaseAAnnunciation);
        Assert.AreEqual(RelayLampState.Off, viewModel.PhaseBAnnunciation);
        Assert.AreEqual(RelayLampState.Off, viewModel.PhaseCAnnunciation);
        Assert.AreEqual(RelayLampState.Off, viewModel.EarthAnnunciation);
    }

    [TestMethod]
    public async Task FaceplateKeysNavigatePortableOperatorPagesAndConfirmSelection()
    {
        await using var viewModel = new MainWindowViewModel();

        Assert.AreEqual("MEASURE", viewModel.FaceplatePageName);
        Assert.AreEqual("IA", viewModel.FaceplateRow1Label);

        viewModel.FaceplateEventsCommand.Execute(null);
        Assert.AreEqual("EVENTS", viewModel.FaceplatePageName);
        Assert.AreEqual("E1", viewModel.FaceplateRow1Label);
        StringAssert.Contains(viewModel.FaceplateNavigationText, "F2");

        viewModel.FaceplateNextCommand.Execute(null);
        Assert.AreEqual("RECORDS", viewModel.FaceplatePageName);
        Assert.AreEqual("STATE", viewModel.FaceplateRow1Label);

        viewModel.FaceplateMenuCommand.Execute(null);
        Assert.AreEqual("SETUP", viewModel.FaceplatePageName);
        Assert.AreEqual("GROUP", viewModel.FaceplateRow1Label);

        var eventCount = viewModel.Events.Count;
        viewModel.FaceplateOkCommand.Execute(null);
        Assert.AreEqual(eventCount + 1, viewModel.Events.Count);
        StringAssert.Contains(viewModel.FaceplateNavigationText, "VIEW CONFIRMED");

        viewModel.FaceplateHomeCommand.Execute(null);
        Assert.AreEqual("MEASURE", viewModel.FaceplatePageName);
    }

    [TestMethod]
    public async Task SmvBlockDoesNotInventPhaseOrEarthTripCauses()
    {
        await using var viewModel = new MainWindowViewModel();

        viewModel.ToggleSmvDegradation();
        viewModel.Tick();

        Assert.IsTrue(viewModel.SmvDegraded);
        Assert.IsFalse(viewModel.TripLatched);
        Assert.AreEqual(RelayLampState.Off, viewModel.PhaseAAnnunciation);
        Assert.AreEqual(RelayLampState.Off, viewModel.PhaseBAnnunciation);
        Assert.AreEqual(RelayLampState.Off, viewModel.PhaseCAnnunciation);
        Assert.AreEqual(RelayLampState.Off, viewModel.EarthAnnunciation);
    }
}
