using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class ProcessBusWorkspaceSourceTests
{
    [TestMethod]
    public void Workspace_UsesNativeAvaloniaControlsAndPortableControllerCommands()
    {
        var xaml = Read("src", "Arvrel.Desktop", "Controls", "ProcessBusWorkspace.axaml");
        var behavior = Read("src", "Arvrel.Desktop", "Controls", "ProcessBusWorkspace.axaml.cs");
        var viewModel = Read("src", "Arvrel.Desktop", "ViewModels", "MainWindowViewModel.ProcessBus.cs");

        _ = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);

        StringAssert.Contains(xaml, "PROCESS-BUS SOURCE");
        StringAssert.Contains(xaml, "LIVE CAPTURE ADAPTER");
        StringAssert.Contains(xaml, "PCAP / PCAPNG REPLAY");
        StringAssert.Contains(xaml, "SCL STREAM BINDING");
        StringAssert.Contains(xaml, "DISCOVERED SV STREAM");
        StringAssert.Contains(xaml, "STREAM TELEMETRY");
        StringAssert.Contains(xaml, "StartLiveCaptureCommand");
        StringAssert.Contains(xaml, "ReplayCaptureCommand");
        StringAssert.Contains(xaml, "LoadSclCommand");
        StringAssert.Contains(xaml, "StopProcessBusCommand");

        StringAssert.Contains(behavior, "OpenFilePickerAsync");
        StringAssert.Contains(behavior, "*.pcapng");
        StringAssert.Contains(behavior, "*.scd");
        Assert.IsFalse(behavior.Contains("Microsoft.Win32", StringComparison.Ordinal));
        Assert.IsFalse(behavior.Contains("System.Windows.Controls", StringComparison.Ordinal));

        StringAssert.Contains(viewModel, "_processBus.ListAdapters()");
        StringAssert.Contains(viewModel, "_processBus.StartLiveAsync");
        StringAssert.Contains(viewModel, "_processBus.ReplayAsync");
        StringAssert.Contains(viewModel, "_processBus.LoadScl");
        StringAssert.Contains(viewModel, "_processBus.GetStreams()");
        StringAssert.Contains(viewModel, "_processBus.GetSnapshot");
        StringAssert.Contains(viewModel, "ConcurrentQueue<ProcessBusEventArgs>");
        Assert.IsFalse(viewModel.Contains("System.Windows.Controls", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainWindow_MountsAndTicksProcessBusWorkspaceWithoutReplacingExistingSourceTabs()
    {
        var source = Read("src", "Arvrel.Desktop", "MainWindow.axaml.cs");

        StringAssert.Contains(source, "InstallProcessBusWorkspace()");
        StringAssert.Contains(source, "new ProcessBusWorkspace");
        StringAssert.Contains(source, "Header = \"PROCESS BUS\"");
        StringAssert.Contains(source, "sourceTabs.Items.Insert");
        StringAssert.Contains(source, "viewModel.TickProcessBus()");
        StringAssert.Contains(source, "InstallVirtualRelayFaceplate()");
    }

    [TestMethod]
    public void AsyncCommand_PreventsOverlappingSourceOperations()
    {
        var source = Read("src", "Arvrel.Desktop", "Infrastructure", "AsyncRelayCommand.cs");

        StringAssert.Contains(source, "private bool _isExecuting");
        StringAssert.Contains(source, "!_isExecuting");
        StringAssert.Contains(source, "await _execute()");
        StringAssert.Contains(source, "RaiseCanExecuteChanged()");
    }

    private static string Read(params string[] segments)
        => File.ReadAllText(Locate(segments));

    private static string Locate(params string[] segments)
    {
        var starts = new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory)
        };

        foreach (var start in starts)
        {
            for (var current = start; current is not null; current = current.Parent)
            {
                var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException($"Unable to locate {Path.Combine(segments)}.");
    }
}
