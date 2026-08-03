using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private WaveformFrame? _lastCoherentWaveform;
    private PhasorDisplayFrame? _lastCoherentPhasor;
    private string? _displayGuardStreamKey;
    private int _displayGuardPhasorCycle = -1;
    private bool _livePhasorTimerSuspended;
    private bool _phasorQuantityGuardAttached;
    private bool _liveDisplayRenderingAttached;
    private long _lastLiveProjectionTicks;

    internal void InitializeLiveDisplayGuard()
    {
        ContentRendered += (_, _) => StartLiveDisplayGuard();
        Closed += (_, _) => StopLiveDisplayGuard();
    }

    private void StartLiveDisplayGuard()
    {
        if (_liveDisplayRenderingAttached)
            return;

        AttachPhasorQuantityGuard();
        CompositionTarget.Rendering += LiveDisplayGuard_Rendering;
        _liveDisplayRenderingAttached = true;
    }

    private void StopLiveDisplayGuard()
    {
        if (!_liveDisplayRenderingAttached)
            return;

        CompositionTarget.Rendering -= LiveDisplayGuard_Rendering;
        _liveDisplayRenderingAttached = false;
    }

    private void AttachPhasorQuantityGuard()
    {
        if (_phasorQuantityGuardAttached || _phasorQuantityCombo is null)
            return;

        _phasorQuantityGuardAttached = true;
        _phasorQuantityCombo.SelectionChanged += (_, _) =>
        {
            _lastCoherentPhasor = null;
            _displayGuardPhasorCycle = -1;
            _lastLiveProjectionTicks = 0;
            Dispatcher.BeginInvoke(
                DispatcherPriority.Render,
                new Action(ReassertLiveDisplay));
        };
    }

    private void LiveDisplayGuard_Rendering(object? sender, EventArgs e)
    {
        AttachPhasorQuantityGuard();

        if (SourceCombo.SelectedIndex == 0)
        {
            ResumeStandardPhasorTimer();
            return;
        }

        SuspendStandardPhasorTimer();

        // Snapshot/projection work remains throttled, but the last accepted frame is
        // reasserted on every WPF render. This closes the former race where the normal
        // 40 ms UI timer briefly painted raw/discontinuous data before the 60 ms guard.
        var nowTicks = Stopwatch.GetTimestamp();
        var projectionDue = _lastLiveProjectionTicks == 0 ||
                            Stopwatch.GetElapsedTime(_lastLiveProjectionTicks, nowTicks) >= TimeSpan.FromMilliseconds(35);
        if (projectionDue)
        {
            _lastLiveProjectionTicks = nowTicks;
            UpdateLiveDisplayProjection();
        }

        ReassertLiveDisplay();
    }

    private void UpdateLiveDisplayProjection()
    {
        if (string.IsNullOrWhiteSpace(_selectedStreamKey))
        {
            _displayGuardStreamKey = null;
            _lastCoherentWaveform = null;
            _lastCoherentPhasor = null;
            _displayGuardPhasorCycle = -1;
            return;
        }

        SmvRuntimeSnapshot snapshot;
        try
        {
            snapshot = _processBus.GetSnapshot(_selectedStreamKey, ViewCombo.SelectedIndex == 1);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(snapshot.Stream.Key))
            return;

        if (!string.Equals(_displayGuardStreamKey, snapshot.Stream.Key, StringComparison.Ordinal))
        {
            _displayGuardStreamKey = snapshot.Stream.Key;
            _lastCoherentWaveform = null;
            _lastCoherentPhasor = null;
            _displayGuardPhasorCycle = -1;
        }

        var expectedWindow = Math.Max(2, snapshot.SamplesPerCycle * 2);
        var waveformComplete = snapshot.Waveform.PhaseA.Count >= expectedWindow &&
                               snapshot.Waveform.PhaseB.Count == snapshot.Waveform.PhaseA.Count &&
                               snapshot.Waveform.PhaseC.Count == snapshot.Waveform.PhaseA.Count &&
                               snapshot.Waveform.Residual.Count == snapshot.Waveform.PhaseA.Count;
        var coherent = waveformComplete && IsDisplayCoherentTrust(snapshot.Measurement.SmvTrust);

        if (!coherent)
            return;

        _lastCoherentWaveform = new WaveformFrame(
            snapshot.Waveform.PhaseA,
            snapshot.Waveform.PhaseB,
            snapshot.Waveform.PhaseC,
            snapshot.Waveform.Residual,
            snapshot.Waveform.FrequencyHz,
            _pickupPosition,
            _tripPosition);

        var samplesPerCycle = Math.Max(1, snapshot.SamplesPerCycle);
        var phasorCycle = snapshot.SampleCounter / samplesPerCycle;
        if (_phasorScope is null ||
            phasorCycle == _displayGuardPhasorCycle && _lastCoherentPhasor is not null)
            return;

        var projected = PhasorDisplayProjector.Project(
            snapshot.Measurement.Phasors,
            _phasorDisplayMode);
        if (!projected.IsAvailable)
            return;

        _lastCoherentPhasor = projected;
        _displayGuardPhasorCycle = phasorCycle;
    }

    private void ReassertLiveDisplay()
    {
        if (SourceCombo.SelectedIndex == 0)
            return;

        SmvScope.Frame = _lastCoherentWaveform is null
            ? WaveformFrame.Empty
            : _lastCoherentWaveform with
            {
                PickupPosition = _pickupPosition,
                TripPosition = _tripPosition
            };

        if (_phasorScope is null)
            return;

        _phasorScope.Frame = _lastCoherentPhasor
            ?? PhasorDisplayFrame.Unavailable(
                _phasorDisplayMode,
                "Waiting for a coherent 4I+4V SMV window.");
    }

    private static bool IsDisplayCoherentTrust(SmvTrustState trust)
    {
        if (!trust.AllowsMeasurement)
            return false;

        return trust.Code is
            "HEALTHY" or
            "SCL_UNBOUND" or
            "SCALING_UNRESOLVED";
    }

    private void SuspendStandardPhasorTimer()
    {
        if (_livePhasorTimerSuspended)
            return;

        _phasorRefreshTimer?.Stop();
        _livePhasorTimerSuspended = true;
    }

    private void ResumeStandardPhasorTimer()
    {
        if (!_livePhasorTimerSuspended)
            return;

        _phasorRefreshTimer?.Start();
        _livePhasorTimerSuspended = false;
        _displayGuardStreamKey = null;
        _lastCoherentWaveform = null;
        _lastCoherentPhasor = null;
        _displayGuardPhasorCycle = -1;
        _lastLiveProjectionTicks = 0;
    }
}

internal static class LiveDisplayGuardBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoaded));
    }

    private static void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.InitializeLiveDisplayGuard();
    }
}
