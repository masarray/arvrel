using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class DisplayHandoverSourceTests
{
    [TestMethod]
    public void DisplayProjection_RequiresCoherentTwoCycleWindowAndRetainsAcceptedFrame()
    {
        var source = Read("src", "Arvrel.Desktop", "ViewModels", "MainWindowViewModel.Display.cs");

        StringAssert.Contains(source, "expectedWindow = Math.Max(2, snapshot.SamplesPerCycle * 2)");
        StringAssert.Contains(source, "snapshot.Measurement.SmvTrust.AllowsMeasurement");
        StringAssert.Contains(source, "\"HEALTHY\" or");
        StringAssert.Contains(source, "\"SCL_UNBOUND\" or");
        StringAssert.Contains(source, "\"SCALING_UNRESOLVED\"");
        StringAssert.Contains(source, "if (IsCoherentForDisplay(snapshot))");
        StringAssert.Contains(source, "_coherentProcessBusSnapshot = snapshot");
        StringAssert.Contains(source, "_coherentProcessBusWaveform = ConvertWaveform");
        Assert.IsFalse(source.Contains("else\n        {\n            _coherentProcessBusSnapshot = SmvRuntimeSnapshot.Empty", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainViewModel_RoutesExistingDisplayBindingsThroughSelectedSource()
    {
        var source = Read("src", "Arvrel.Desktop", "ViewModels", "MainWindowViewModel.cs");
        var faceplate = Read("src", "Arvrel.Desktop", "ViewModels", "MainWindowViewModel.Faceplate.cs");

        StringAssert.Contains(source, "P5.9 · GUARDED SOURCE HANDOVER");
        StringAssert.Contains(source, "SourceModeText => ActiveDisplaySourceText");
        StringAssert.Contains(source, "SmvDegraded => IsProcessBusDisplayActive");
        StringAssert.Contains(source, "!DisplayProtection.SmvTrust.AllowsTrip");
        StringAssert.Contains(source, "AllowsTrip => DisplayProtection.SmvTrust.AllowsTrip");
        StringAssert.Contains(source, "TripLatched => DisplayAnnunciation.TripLatched");
        StringAssert.Contains(source, "FormatCurrent(DisplayMeasurement.PhaseA)");
        StringAssert.Contains(source, "ScenarioWaveform Waveform => DisplayWaveform");
        StringAssert.Contains(source, "RunCommand = new AsyncRelayCommand(ToggleDisplayedSourceAsync)");
        StringAssert.Contains(source, "ResetRelayCommand = new RelayCommand(ResetDisplayedRelay)");
        StringAssert.Contains(source, "if (!IsProcessBusDisplayActive)");

        StringAssert.Contains(faceplate, "PhaseAAnnunciation => DisplayAnnunciation.PhaseA");
        StringAssert.Contains(faceplate, "Compact(DisplayActiveElement, 24)");
        StringAssert.Contains(faceplate, "DisplayFingerprintText");
        StringAssert.Contains(faceplate, "DisplayProvenanceText");
    }

    [TestMethod]
    public void ProcessBusWorkspace_ExposesExplicitHandoverAndNeverAutoActivatesLiveDisplay()
    {
        var xaml = Read("src", "Arvrel.Desktop", "Controls", "ProcessBusWorkspace.axaml");
        var source = Read("src", "Arvrel.Desktop", "ViewModels", "MainWindowViewModel.Display.cs");
        var processBus = Read("src", "Arvrel.Desktop", "ViewModels", "MainWindowViewModel.ProcessBus.cs");

        StringAssert.Contains(xaml, "OPERATOR DISPLAY SOURCE");
        StringAssert.Contains(xaml, "ActivateProcessBusDisplayCommand");
        StringAssert.Contains(xaml, "ActivateInternalDisplayCommand");
        StringAssert.Contains(xaml, "Only a coherent two-cycle SV window");

        StringAssert.Contains(source, "private void ActivateProcessBusDisplay()");
        StringAssert.Contains(source, "private void ActivateInternalDisplay()");
        StringAssert.Contains(source, "Select an SV stream before activating");
        StringAssert.Contains(source, "waiting for a coherent two-cycle SV window");
        StringAssert.Contains(source, "Selected SV stream changed; display returned to INTERNAL LAB");

        Assert.IsFalse(processBus.Contains("ActivateProcessBusDisplay();", StringComparison.Ordinal));
        StringAssert.Contains(processBus, "ClearProcessBusSelectionForNewSource");
        StringAssert.Contains(processBus, "SelectedProcessBusStream is null");
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
