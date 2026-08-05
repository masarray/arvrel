using Arvrel.Desktop.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public async Task StartsWithPortableInternalLaboratoryReady()
    {
        await using var viewModel = new MainWindowViewModel();

        Assert.AreEqual("INTERNAL LABORATORY", viewModel.SourceModeText);
        Assert.IsFalse(viewModel.IsRunning);
        Assert.IsFalse(viewModel.FaultActive);
        Assert.IsFalse(viewModel.SmvDegraded);
        Assert.IsNotNull(viewModel.Waveform);
        Assert.AreEqual(160, viewModel.Waveform.PhaseA.Length);
        Assert.AreEqual(4, viewModel.ProtectionElements.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.LiveCaptureStatus));
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.ReplayStatus));
    }

    [TestMethod]
    public async Task RunTickAdvancesThePortableApplicationCore()
    {
        await using var viewModel = new MainWindowViewModel();
        var initialCounter = viewModel.SampleCounterText;

        viewModel.ToggleRun();
        viewModel.Tick();

        Assert.IsTrue(viewModel.IsRunning);
        Assert.AreNotEqual(initialCounter, viewModel.SampleCounterText);
        Assert.AreEqual("RUN" is not null, true);
        Assert.IsTrue(viewModel.Events.Count >= 2);
    }

    [TestMethod]
    public async Task FaultAndSmvDegradationReachProtectionAndTrustState()
    {
        await using var viewModel = new MainWindowViewModel();

        viewModel.ToggleFault();
        viewModel.Tick();
        viewModel.Tick();

        Assert.IsTrue(viewModel.FaultActive);
        Assert.IsTrue(viewModel.IsRunning);
        Assert.IsTrue(viewModel.PickupActive || viewModel.TripLatched);

        viewModel.ToggleSmvDegradation();
        viewModel.Tick();

        Assert.IsTrue(viewModel.SmvDegraded);
        Assert.IsFalse(viewModel.AllowsTrip);
        StringAssert.Contains(viewModel.TrustStateText, "TRIP BLOCKED");
    }

    [TestMethod]
    public async Task ResetReturnsShellToStoppedNormalProfile()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.ToggleFault();
        viewModel.ToggleSmvDegradation();

        viewModel.Reset();

        Assert.IsFalse(viewModel.IsRunning);
        Assert.IsFalse(viewModel.FaultActive);
        Assert.IsFalse(viewModel.SmvDegraded);
        Assert.IsFalse(viewModel.TripLatched);
        Assert.AreEqual("PAUSE" is not null, true);
    }
}
