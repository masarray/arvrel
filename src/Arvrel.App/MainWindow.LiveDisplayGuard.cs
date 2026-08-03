using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private DispatcherTimer? _liveDisplayGuardTimer;
    private WaveformFrame? _lastCoherentWaveform;
    private PhasorDisplayFrame? _lastCoherentPhasor;
    private string? _displayGuardStreamKey;
    private int _displayGuardPhasorCycle = -1;
    private bool _livePhasorTimerSuspended;
    private bool _phasorQuantityGuardAttached;

    internal void InitializeLiveDisplayGuard()
    {
        ContentRendered += (_, _) => StartLiveDisplayGuard();
        Closed += (_, _) => _liveDisplayGuardTimer?.Stop();
    }

    private void StartLiveDisplayGuard()
    {
        if (_liveDisplayGuardTimer is not null)
            return;

        AttachPhasorQuantityGuard();
        _liveDisplayGuardTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(60)
        };
        _liveDisplayGuardTimer.Tick += (_, _) => ApplyLiveDisplayGuard();
        _liveDisplayGuardTimer.Start();
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
            Dispatcher.BeginInvoke(
                DispatcherPriority.Render,
                new Action(ApplyLiveDisplayGuard));
        };
    }

    private void ApplyLiveDisplayGuard()
    {
        AttachPhasorQuantityGuard();

        if (SourceCombo.SelectedIndex == 0)
        {
            ResumeStandardPhasorTimer();
            return;
        }

        SuspendStandardPhasorTimer();

        if (string.IsNullOrWhiteSpace(_selectedStreamKey))
        {
            SmvScope.Frame = WaveformFrame.Empty;
            if (_phasorScope is not null)
                _phasorScope.Frame = PhasorDisplayFrame.Unavailable(
                    _phasorDisplayMode,
                    "Waiting for a coherent SMV measurement window.");
            return;
        }

        var snapshot = _processBus.GetSnapshot(_selectedStreamKey, ViewCombo.SelectedIndex == 1);
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

        if (coherent)
        {
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
            if (_phasorScope is not null &&
                (phasorCycle != _displayGuardPhasorCycle || _lastCoherentPhasor is null))
            {
                var projected = PhasorDisplayProjector.Project(
                    snapshot.Measurement.Phasors,
                    _phasorDisplayMode);
                if (projected.IsAvailable)
                {
                    _lastCoherentPhasor = projected;
                    _displayGuardPhasorCycle = phasorCycle;
                }
            }
        }

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
                coherent
                    ? "Waiting for a complete 4I+4V phasor window."
                    : $"Holding display · {snapshot.Measurement.SmvTrust.Code}.");
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
