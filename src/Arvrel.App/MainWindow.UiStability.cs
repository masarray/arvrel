using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Arvrel.App.Services;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _globalUiStabilityInitialized;
    private string? _lastInternalPresentationSignature;
    private int _stableExternalRenderDivider;
    private int _lastStableSourceIndex = -1;

    internal void InitializeGlobalUiStability()
    {
        if (_globalUiStabilityInitialized)
            return;

        _globalUiStabilityInitialized = true;

        // The legacy timer rendered every text block, LED, waveform, and footer
        // every 40 ms. It also competed with the virtual-injection presentation
        // timer, causing INTERNAL/GOOD and STOPPED/RUNNING strings to alternate.
        // Keep the 40 ms protection execution cadence, but render only when the
        // values visible to the operator actually change.
        _timer.Tick -= Timer_Tick;
        _timer.Tick += StableTimer_Tick;
        _lastInternalPresentationSignature = null;
    }

    internal void StopGlobalUiStability()
    {
        if (!_globalUiStabilityInitialized)
            return;
        _timer.Tick -= StableTimer_Tick;
        _globalUiStabilityInitialized = false;
    }

    private void StableTimer_Tick(object? sender, EventArgs e)
    {
        var sourceIndex = SourceCombo.SelectedIndex;
        if (_lastStableSourceIndex != sourceIndex)
        {
            _lastStableSourceIndex = sourceIndex;
            _lastInternalPresentationSignature = null;
            _stableExternalRenderDivider = 0;
        }

        if (sourceIndex == 0)
        {
            if (!_internalRunning)
                return;

            ScenarioStep? last = null;
            for (var index = 0; index < 8; index++)
            {
                last = _scenario.Advance(TimeSpan.FromMilliseconds(5), _pickupPosition, _tripPosition);
                _snapshot = _internalEngine.Evaluate(last.Measurement);
                ObserveTransitions(_snapshot);
            }

            if (last is null)
                return;

            var measurement = last.Measurement;
            var signature = FormattableString.Invariant(
                $"{_scenario.InjectionFingerprint}|{_scenario.OutputState}|{_scenario.WindowStatus}|{_scenario.SmvDegraded}|" +
                $"{measurement.PhaseA:0.0000}|{measurement.PhaseB:0.0000}|{measurement.PhaseC:0.0000}|{measurement.Residual:0.0000}|" +
                $"{_snapshot.Phase50.State}:{_snapshot.Phase50.Progress:0.0000}|" +
                $"{_snapshot.Phase51.State}:{_snapshot.Phase51.Progress:0.0000}|" +
                $"{_snapshot.Earth50.State}:{_snapshot.Earth50.Progress:0.0000}|" +
                $"{_snapshot.Earth51.State}:{_snapshot.Earth51.Progress:0.0000}|" +
                $"{_snapshot.TripLatched}|{_snapshot.ActiveElement}|{_snapshot.DecisionReason}|{_snapshot.SmvTrust.Code}|" +
                $"{_pickupPosition:R}|{_tripPosition:R}");

            if (string.Equals(_lastInternalPresentationSignature, signature, StringComparison.Ordinal))
                return;

            RenderInternal(last, _snapshot);
            // RenderInternal contains the legacy generic labels. Apply the
            // injection-specific concise presentation in the same dispatcher
            // turn so no intermediate GOOD/STOPPED text reaches the screen.
            RefreshVirtualInjectionRunStopPresentation();
            _lastInternalPresentationSignature = signature;
            return;
        }

        _streamRefreshDivider++;
        if (_streamRefreshDivider >= 6)
        {
            _streamRefreshDivider = 0;
            RefreshStreamList(force: false);
        }

        // Live/replay instruments remain responsive at roughly 8 Hz while
        // avoiding a full WPF tree update at the 25 Hz protection cadence.
        _stableExternalRenderDivider++;
        if (_stableExternalRenderDivider < 3)
            return;
        _stableExternalRenderDivider = 0;
        RenderSelectedProcessBusStream();
    }
}

internal static class GlobalUiStabilityBootstrap
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

        window.Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(window.InitializeGlobalUiStability));
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StopGlobalUiStability();
    }
}