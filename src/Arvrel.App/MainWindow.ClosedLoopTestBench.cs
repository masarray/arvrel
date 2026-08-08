using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Arvrel.Application.Laboratory;
using Arvrel.Protection.Algorithms;
using Microsoft.Win32;
using ClosedLoopBench = Arvrel.Application.Laboratory.ClosedLoopVirtualTestBench;

namespace Arvrel.App;

public partial class MainWindow
{
    private ClosedLoopBench? _closedLoopBench;
    private DateTimeOffset? _lastReportedTestSetPickup;
    private DateTimeOffset? _lastReportedTestSetTrip;
    private Button? _closedLoopEvidenceButton;

    internal void InitializeClosedLoopVirtualTestBench()
    {
        if (_closedLoopBench is not null)
            return;

        _closedLoopBench = new ClosedLoopBench(_scenario.CoreScenario, _internalEngine);

        // Replace the legacy 5 ms direct-call loop. WPF remains a 40 ms presenter,
        // while the platform-neutral bench advances protection and wired I/O at the
        // injector's native 4 kHz / 0.25 ms deterministic sample grid.
        _timer.Tick -= Timer_Tick;
        _timer.Tick += ClosedLoopTimer_Tick;

        InstallClosedLoopEvidenceOverride();
        AddEvent("BACKPLANE", "Closed-loop virtual wiring active · 0.25 ms simulation authority");
        EngineModeText.Text = SmvProcessBusController.IsAvailable
            ? "P0 CLOSED LOOP · ARIEC61850 READY"
            : "P0 CLOSED LOOP · VIRTUAL I/O";
    }

    internal void StopClosedLoopVirtualTestBench()
    {
        if (_closedLoopBench is null)
            return;

        _timer.Tick -= ClosedLoopTimer_Tick;
        _timer.Tick += Timer_Tick;
        if (_closedLoopEvidenceButton is not null)
        {
            _closedLoopEvidenceButton.Click -= ExportClosedLoopEvidence_Click;
            _closedLoopEvidenceButton.Click += ExportVirtualInjectionEvidence_Click;
            _closedLoopEvidenceButton = null;
        }
        _closedLoopBench = null;
    }

    private void ClosedLoopTimer_Tick(object? sender, EventArgs e)
    {
        if (SourceCombo.SelectedIndex == 0)
        {
            if (_closedLoopBench is null)
                return;

            if (_internalRunning)
            {
                var result = _closedLoopBench.Advance(TimeSpan.FromMilliseconds(40));
                _snapshot = result.Protection;
                ObserveTransitions(_snapshot);
                ReportClosedLoopTestSetTransitions(result.TestSet, _snapshot);

                // Auto-stop is driven exclusively by TESTSET.BI1 after the delayed
                // relay BO1 contact crosses its virtual wire. Never by TripLatched.
                _internalRunning = _scenario.IsRunning;
                var displayResult = !_internalRunning && result.TestSet.TripDetectedAt is not null
                    ? _closedLoopBench.Advance(TimeSpan.Zero)
                    : result;
                _snapshot = displayResult.Protection;
                var displayStep = _scenario.Project(displayResult.Source, _pickupPosition, _tripPosition);
                RenderInternal(displayStep, _snapshot);

                if (!_internalRunning)
                {
                    UpdateRunButton();
                    RefreshVirtualInjectionRunStopPresentation();
                }
            }
            else if (!_scenario.IsRunning &&
                     !_snapshot.TripLatched &&
                     _closedLoopBench.TestSetSnapshot.InjectionStartedAt is not null)
            {
                // The existing reset/settings workflow owns the relay/source reset.
                // Recreate only the external test-set observer so stale BI/timing
                // evidence cannot leak into the next test.
                RecreateClosedLoopObserver();
            }
            return;
        }

        _streamRefreshDivider++;
        if (_streamRefreshDivider >= 6)
        {
            _streamRefreshDivider = 0;
            RefreshStreamList(force: false);
        }
        RenderSelectedProcessBusStream();
    }

    private void RecreateClosedLoopObserver()
    {
        _closedLoopBench = new ClosedLoopBench(_scenario.CoreScenario, _internalEngine);
        _lastReportedTestSetPickup = null;
        _lastReportedTestSetTrip = null;
    }

    private void ReportClosedLoopTestSetTransitions(
        VirtualTestSetTimingSnapshot testSet,
        Arvrel.Protection.ProtectionSnapshot protection)
    {
        if (testSet.PickupDetectedAt is { } pickup && pickup != _lastReportedTestSetPickup)
        {
            _lastReportedTestSetPickup = pickup;
            AddEvent("TEST PICKUP", $"BI2 · {testSet.PickupTime?.TotalMilliseconds:0.000} ms");
        }

        if (testSet.TripDetectedAt is not { } trip || trip == _lastReportedTestSetTrip)
            return;

        _lastReportedTestSetTrip = trip;
        var pickupMs = testSet.PickupTime?.TotalMilliseconds;
        var tripMs = testSet.TripTime?.TotalMilliseconds;
        var relayOperateMs = protection.LatchedOperation is { } operation
            ? (operation.TripTimestamp - operation.PickupTimestamp).TotalMilliseconds
            : (double?)null;

        AddEvent("TEST TRIP", $"BI1 · {tripMs:0.000} ms · output stopped");
        StatusText.Text =
            $"Closed-loop trip measured at TESTSET.BI1 · PICKUP {pickupMs:0.000} ms · " +
            $"TRIP {tripMs:0.000} ms · relay P→T {relayOperateMs:0.000} ms · output stopped.";
    }

    private void InstallClosedLoopEvidenceOverride()
    {
        _closedLoopEvidenceButton = FindButtonByToolTip(this, "Export evidence JSON");
        if (_closedLoopEvidenceButton is null)
            return;

        _closedLoopEvidenceButton.Click -= ExportEvidence_Click;
        _closedLoopEvidenceButton.Click -= ExportVirtualInjectionEvidence_Click;
        _closedLoopEvidenceButton.Click += ExportClosedLoopEvidence_Click;
    }

    private void ExportClosedLoopEvidence_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0 || _closedLoopBench is null)
        {
            ExportEvidence_Click(sender, e);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export ARVREL closed-loop test evidence",
            Filter = "ARVREL evidence JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"ARVREL-closed-loop-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var current = _closedLoopBench.Advance(TimeSpan.Zero);
        var algorithmRuntime = AlgorithmRuntimeRegistry.Snapshot();
        var evidence = new
        {
            schemaVersion = 5,
            exportedAt = DateTimeOffset.Now,
            application = "ARVREL",
            operatingMode = OperatingModeCombo.SelectedIndex == 1 ? "Research" : "Practitioner",
            sourceMode = "Closed-loop virtual secondary injection",
            simulation = new
            {
                quantumMilliseconds = ClosedLoopBench.SimulationQuantum.TotalMilliseconds,
                sampleRateHz = Services.DeterministicLabScenario.SampleRateHz,
                autoStopAuthority = "TESTSET.BI1 rising edge only"
            },
            injection = new
            {
                configuredProfile = _scenario.ActiveProfile,
                effectiveOutputProfile = _scenario.OutputProfile,
                configuredFingerprint = _scenario.InjectionFingerprint,
                effectiveOutputFingerprint = _scenario.OutputFingerprint,
                configuredAt = _scenario.AppliedAt,
                outputStateChangedAt = _scenario.OutputStateChangedAt,
                isRunning = _scenario.IsRunning,
                outputState = _scenario.OutputState,
                windowStatus = _scenario.WindowStatus
            },
            wiring = new
            {
                topology = _closedLoopBench.Topology,
                topologyFingerprint = _closedLoopBench.Topology.Fingerprint()
            },
            relayContactProfile = VirtualRelayContactProfile.NumericalRelayDefault,
            testSet = _closedLoopBench.TestSetSnapshot,
            relayMeasurement = current.RelayMeasurement,
            protection = _snapshot,
            protectionSettings = _settings,
            settingsFingerprint = _settings.Fingerprint(),
            algorithmMode = algorithmRuntime.Mode,
            algorithmRuntime,
            events = _events.Reverse().ToArray()
        };

        File.WriteAllText(
            dialog.FileName,
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        AddEvent("EXPORT", System.IO.Path.GetFileName(dialog.FileName));
        StatusText.Text = $"Closed-loop evidence exported to {dialog.FileName}.";
    }
}

internal static class ClosedLoopVirtualTestBenchBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.UnloadedEvent,
            new RoutedEventHandler(OnUnloaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        window.ContentRendered -= Window_ContentRendered;
        window.ContentRendered += Window_ContentRendered;
    }

    private static void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (sender is MainWindow window)
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(window.InitializeClosedLoopVirtualTestBench));
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StopClosedLoopVirtualTestBench();
    }
}
