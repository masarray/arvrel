using System.Globalization;
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
        _timer.Tick -= Timer_Tick;
        _timer.Tick -= StableTimer_Tick;
        if (_closedLoopBench is null)
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
            var signature = string.Join(
                "|",
                _scenario.InjectionFingerprint,
                _scenario.OutputState,
                _scenario.WindowStatus,
                _scenario.SmvDegraded.ToString(CultureInfo.InvariantCulture),
                measurement.PhaseA.ToString("0.0000", CultureInfo.InvariantCulture),
                measurement.PhaseB.ToString("0.0000", CultureInfo.InvariantCulture),
                measurement.PhaseC.ToString("0.0000", CultureInfo.InvariantCulture),
                measurement.Residual.ToString("0.0000", CultureInfo.InvariantCulture),
                $"{_snapshot.Phase50.State}:{_snapshot.Phase50.Progress.ToString("0.0000", CultureInfo.InvariantCulture)}",
                $"{_snapshot.Phase51.State}:{_snapshot.Phase51.Progress.ToString("0.0000", CultureInfo.InvariantCulture)}",
                $"{_snapshot.Earth50.State}:{_snapshot.Earth50.Progress.ToString("0.0000", CultureInfo.InvariantCulture)}",
                $"{_snapshot.Earth51.State}:{_snapshot.Earth51.Progress.ToString("0.0000", CultureInfo.InvariantCulture)}",
                _snapshot.TripLatched.ToString(CultureInfo.InvariantCulture),
                _snapshot.ActiveElement,
                _snapshot.DecisionReason,
                _snapshot.SmvTrust.Code,
                _pickupPosition.ToString("R", CultureInfo.InvariantCulture),
                _tripPosition.ToString("R", CultureInfo.InvariantCulture));

            if (string.Equals(_lastInternalPresentationSignature, signature, StringComparison.Ordinal))
                return;

            RenderInternal(last, _snapshot);
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