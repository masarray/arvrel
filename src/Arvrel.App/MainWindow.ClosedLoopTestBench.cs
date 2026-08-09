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
            frontEndProfile: VirtualRelayFrontEndProfile.NumericalRelayDefault,
            metrologyProfile: MetrologyTimingProfile.CmcStyle);

        // WPF remains presentation only. Protection acquisition stays on the 4 kHz
        // relay sample grid while TESTSET BI timing is timestamped by the independent
        // microsecond metrology clock and sampled at 10 kHz. Closed-loop owns source
        // advancement exclusively regardless of bootstrap initializer order.
        _timer.Tick -= Timer_Tick;
        _timer.Tick -= StableTimer_Tick;
        _timer.Tick += ClosedLoopTimer_Tick;

        InstallClosedLoopEvidenceOverride();
        InitializeClosedLoopOperatorClarity();
        AddEvent("BACKPLANE", "Closed-loop active · causal relay ADC/DFT · 1 µs metrology clock · 10 kHz TESTSET BI");
        EngineModeText.Text = SmvProcessBusController.IsAvailable
            ? "P0 METROLOGY · ARIEC61850 READY"
            : "P0 METROLOGY · VIRTUAL I/O";
    }

    internal void StopClosedLoopVirtualTestBench()
    {
        if (_closedLoopBench is null)
            return;

        _timer.Tick -= ClosedLoopTimer_Tick;
        _timer.Tick -= Timer_Tick;
        _timer.Tick -= StableTimer_Tick;
        if (_globalUiStabilityInitialized)
            _timer.Tick += StableTimer_Tick;
        else
            _timer.Tick += Timer_Tick;

        if (_closedLoopEvidenceButton is not null)
        {
            _closedLoopEvidenceButton.Click -= ExportClosedLoopEvidence_Click;
            _closedLoopEvidenceButton.Click += ExportVirtualInjectionEvidence_Click;
            _closedLoopEvidenceButton = null;
        }

        StatusText.ToolTip = null;
        _closedLoopBench = null;
    }

    private void ClosedLoopTimer_Tick(object? sender, EventArgs e)
    {
        if (SourceCombo.SelectedIndex == 0)
        {
            if (_closedLoopBench is null)
                return;

            // Trip details belong to one completed test run only. As soon as a new
            // run is armed/idle, discard the previous run's explanatory tooltip so
            // unrelated status text can never inherit stale BI1/capture evidence.
            if (_closedLoopBench.TestSetSnapshot.TimerState != VirtualTestSetTimerState.Completed &&
                StatusText.ToolTip is not null)
            {
                StatusText.ToolTip = null;
            }

            if (_internalRunning)
            {
                // Advance one 250 µs relay quantum at a time from the WPF presentation
                // slice. The bench still owns all timing authority; this only lets the
                // desktop observe the exact first frame where generic relay pickup is
                // asserted instead of trying to infer its source 40 ms later.
                var remaining = TimeSpan.FromMilliseconds(40);
                ClosedLoopVirtualTestBenchStep? result = null;
                while (remaining > TimeSpan.Zero && _scenario.IsRunning)
                {
                    var quantum = remaining > ClosedLoopBench.SimulationQuantum
                        ? ClosedLoopBench.SimulationQuantum
                        : remaining;
                    result = _closedLoopBench.Advance(quantum);
                    remaining -= quantum;

                    _snapshot = result.Protection;
                    ObserveFirstAnyPickupSource(result.TestSet, result.Protection);
                    ObserveClosedLoopProtectionTransitions(result.TestSet, _snapshot);
                    ReportClosedLoopTestSetTransitions(result.TestSet);

                    if (!result.TestSet.OutputRunning)
                        break;
                }

                if (result is null)
                    return;

                _internalRunning = _scenario.IsRunning;
                var displayStep = _scenario.Project(result.Source, _pickupPosition, _tripPosition) with
                {
                    Measurement = result.RelayMeasurement
                };
                RenderInternal(displayStep, _snapshot);
                RefreshClosedLoopOperatorState(result.TestSet, _snapshot);

                if (!_internalRunning)
                {
                    UpdateRunButton();
                    RefreshVirtualInjectionRunStopPresentation();
                }
            }
            return;
        }

        if (StatusText.ToolTip is not null)
            StatusText.ToolTip = null;

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
                "TESTSET BI2",
                $"{testSet.PickupTime?.TotalMilliseconds:0.000} ms · ACCEPT · from RELAY ANY [{FirstAnyPickupSourceFor(testSet)}]");
        }

        if (testSet.TripDetectedAt is not { } trip || trip == _lastReportedTestSetTrip)
            return;

        _lastReportedTestSetTrip = trip;
        var rail = BuildClosedLoopTimingRail(testSet, _snapshot);
        var detail = BuildClosedLoopTimingDetail(testSet, _snapshot);

        AddEvent(
            "TESTSET BI1",
            $"{testSet.TripTime?.TotalMilliseconds:0.000} ms · ACCEPT · OUTPUT OFF · {testSet.TimingResolutionMicroseconds} µs resolution");

        StatusText.Text = rail;
        StatusText.ToolTip =
            $"{detail}\n" +
            "TESTSET measurement authority: accepted wired BI1 edge.\n" +
            "Frozen waveform/phasor: first relay processing frame after that accepted edge, not an interpolated frame at the exact BI sample instant.";
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
        var testSetSnapshot = _closedLoopBench.TestSetSnapshot;
        var operatingElementTiming = RelayOperationTimingCorrelator.Correlate(
            testSetSnapshot,
            _snapshot);
        var tripCapture = _closedLoopBench.TripCapture;
        long? captureFrameOffsetUs = TryGetTripCaptureFrameOffsetMicroseconds(tripCapture, out var offsetUs)
            ? offsetUs
            : null;
        object? tripCaptureTiming = tripCapture is null
            ? null
            : new
            {
                measurementAuthority = "TESTSET.BI1 accepted wired rising edge",
                bi1AcceptedAt = tripCapture.TestSet.TripDetectedAt,
                bi1AcceptedMicroseconds = tripCapture.TestSet.TripDetectedMicroseconds,
                captureFrameAt = tripCapture.CapturedAt,
                captureFrameMicroseconds = tripCapture.TestSet.ObservedMicroseconds,
                captureFrameOffsetMicroseconds = captureFrameOffsetUs,
                displayFreezeSemantics = "Frozen waveform/phasor are the first relay processing frame in which the accepted BI1 edge is observable; they are not claimed to be an interpolated state at the exact 10 kHz BI sampling instant."
            };

        var evidence = new
        {
            schemaVersion = 9,
            exportedAt = DateTimeOffset.Now,
            application = "ARVREL",
            operatingMode = OperatingModeCombo.SelectedIndex == 1 ? "Research" : "Practitioner",
            sourceMode = "Closed-loop virtual secondary injection",
            simulation = new
            {
                protectionQuantumMilliseconds = ClosedLoopBench.SimulationQuantum.TotalMilliseconds,
                sourceSampleRateHz = Services.DeterministicLabScenario.SampleRateHz,
                metrology = _closedLoopBench.MetrologyProfile,
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
            legacyRelayFrontEnd = _closedLoopBench.FrontEndSnapshot,
            causalRelayFrontEnd = _closedLoopBench.CausalFrontEndSnapshot,
            relayContactProfile = _closedLoopBench.ContactProfile,
            testSet = testSetSnapshot,
            firstAnyPickup = new
            {
                source = FirstAnyPickupSourceFor(testSetSnapshot),
                relayAssertFromStart = testSetSnapshot.RelayPickupTime,
                testSetBi2FromStart = testSetSnapshot.PickupTime,
                semantics = "Source is captured on the first 4 kHz relay frame that asserts generic ANY-PICKUP/BO2; BI2 is the later wired test-set acceptance."
            },
            operatingElementTiming,
            tripCapture,
            tripCaptureTiming,
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
        StatusText.ToolTip = null;
        StatusText.Text = $"Closed-loop metrology evidence exported to {dialog.FileName}.";
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
