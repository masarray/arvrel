using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Arvrel.Application.Laboratory;
using Arvrel.ProcessBus;
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

        _closedLoopBench = new ClosedLoopBench(
            _scenario.CoreScenario,
            _internalEngine,
            contactProfile: VirtualRelayContactProfile.RealisticNumericalRelay,
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault);

        // Replace the legacy 5 ms direct-call loop. WPF remains a 40 ms presenter,
        // while the platform-neutral bench advances protection and wired I/O at the
        // injector's native 4 kHz / 0.25 ms deterministic sample grid.
        _timer.Tick -= Timer_Tick;
        _timer.Tick += ClosedLoopTimer_Tick;

        InstallClosedLoopEvidenceOverride();
        AddEvent("BACKPLANE", "Closed-loop virtual wiring active · realistic relay front end · 0.25 ms simulation authority");
        EngineModeText.Text = SmvProcessBusController.IsAvailable
            ? "P1 RELAY FRONT END · ARIEC61850 READY"
            : "P1 RELAY FRONT END · VIRTUAL I/O";
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
                ReportClosedLoopTestSetTransitions(result.TestSet);

                // Auto-stop is driven exclusively by TESTSET.BI1 after the delayed
                // relay BO1 contact crosses its virtual wire. Never by TripLatched.
                // ClosedLoopVirtualTestBench returns immediately at the accepted BI1
                // edge, so this frame is the exact pre-stop fault frame and must not
                // be replaced by a zero-output Advance(0) frame.
                _internalRunning = _scenario.IsRunning;
                var displayStep = _scenario.Project(result.Source, _pickupPosition, _tripPosition) with
                {
                    Measurement = result.RelayMeasurement
                };
                RenderInternal(displayStep, _snapshot);

                if (!_internalRunning)
                {
                    UpdateRunButton();
                    RefreshVirtualInjectionRunStopPresentation();
                }
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

    private void ReportClosedLoopTestSetTransitions(VirtualTestSetTimingSnapshot testSet)
    {
        if (testSet.PickupDetectedAt is { } pickup && pickup != _lastReportedTestSetPickup)
        {
            _lastReportedTestSetPickup = pickup;
            AddEvent(
                "TEST PICKUP",
                $"BI2 ↑ · START→BI2 {testSet.PickupTime?.TotalMilliseconds:0.000} ms");
        }

        if (testSet.TripDetectedAt is not { } trip || trip == _lastReportedTestSetTrip)
            return;

        _lastReportedTestSetTrip = trip;
        var pickupText = testSet.PickupTime is { } pickupTime
            ? $"BI2 {pickupTime.TotalMilliseconds:0.000} ms"
            : "BI2 —";
        var tripText = testSet.TripTime is { } tripTime
            ? $"BI1 {tripTime.TotalMilliseconds:0.000} ms"
            : "BI1 —";
        var relayText = testSet.RelayTripTime is { } relayTrip
            ? testSet.RelayPickupToTrip is { } relayOperate
                ? $"relay START→TRIP {relayTrip.TotalMilliseconds:0.000} ms · P→T {relayOperate.TotalMilliseconds:0.000} ms"
                : $"relay START→TRIP {relayTrip.TotalMilliseconds:0.000} ms"
            : "relay timing not correlated to this test run";

        AddEvent(
            "TEST TRIP",
            $"BI1 ↑ · START→BI1 {testSet.TripTime?.TotalMilliseconds:0.000} ms · output stopped");
        StatusText.Text =
            $"TESTSET measured trip · {tripText} · {pickupText} · {relayText} · output stopped · capture frozen at BI1 edge.";
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
            schemaVersion = 7,
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
            relayFrontEndProfile = _closedLoopBench.FrontEndProfile,
            relayFrontEndProfileFingerprint = _closedLoopBench.FrontEndProfile.Fingerprint(),
            relayFrontEnd = _closedLoopBench.FrontEndSnapshot,
            relayContactProfile = _closedLoopBench.ContactProfile,
            testSet = _closedLoopBench.TestSetSnapshot,
            tripCapture = _closedLoopBench.TripCapture,
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
