using Arvrel.Desktop.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class ProcessBusWorkspaceTests
{
    [TestMethod]
    public async Task MissingReplayAndSclFilesAreRejectedWithoutChangingSourceRuntime()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.InitializeProcessBusWorkspace();

        var missingReplay = Path.Combine(Path.GetTempPath(), $"arvrel-missing-{Guid.NewGuid():N}.pcapng");
        viewModel.ReplayPath = missingReplay;
        await viewModel.ReplayCaptureAsync();

        StringAssert.Contains(viewModel.ProcessBusStatus, "Capture file not found");
        Assert.IsFalse(viewModel.ProcessBusRunning);

        var missingScl = Path.Combine(Path.GetTempPath(), $"arvrel-missing-{Guid.NewGuid():N}.scd");
        viewModel.SclPath = missingScl;
        viewModel.LoadSclFile();

        StringAssert.Contains(viewModel.ProcessBusStatus, "SCL file not found");
        Assert.IsFalse(viewModel.ProcessBusRunning);
    }

    [TestMethod]
    public async Task WorkspaceInitializationAndPollingRemainSafeWithoutASelectedStream()
    {
        await using var viewModel = new MainWindowViewModel();

        viewModel.InitializeProcessBusWorkspace();
        viewModel.TickProcessBus();
        viewModel.RefreshProcessBusAdapters();
        viewModel.TickProcessBus();

        Assert.IsNotNull(viewModel.ProcessBusAdapters);
        Assert.IsNotNull(viewModel.ProcessBusStreams);
        Assert.AreEqual("No SV stream selected", viewModel.SelectedStreamSummary);
        StringAssert.Contains(viewModel.ProcessBusCounterText, "frames 0");
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.ProcessBusCapabilityText));
    }

    [TestMethod]
    public async Task EmptyOperatorSelectionsProduceActionableStatusInsteadOfExceptions()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.InitializeProcessBusWorkspace();

        viewModel.SelectedProcessBusAdapter = null;
        await viewModel.StartLiveCaptureAsync();
        StringAssert.Contains(viewModel.ProcessBusStatus, "Select a live capture adapter");

        viewModel.ReplayPath = string.Empty;
        await viewModel.ReplayCaptureAsync();
        StringAssert.Contains(viewModel.ProcessBusStatus, "Select a PCAP or PCAPNG");

        viewModel.SclPath = string.Empty;
        viewModel.LoadSclFile();
        StringAssert.Contains(viewModel.ProcessBusStatus, "Select an SCL file");
    }
}
