using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Arvrel.App.Controls;

namespace Arvrel.App;

public partial class MainWindow
{
    private DispatcherTimer? _liveDisplayGuardTimer;
    private WaveformFrame? _lastCoherentWaveform;
    private PhasorDisplayFrame? _lastCoherentPhasor;
    private string? _displayGuardStreamKey;
    private int _displayGuardPhasorCycle = -1;

    internal void InitializeLiveDisplayGuard()
    {
        ContentRendered += (_, _) => StartLiveDisplayGuard();
        Closed += (_, _) => _liveDisplayGuardTimer?.Stop();
    }

    private void StartLiveDisplayGuard()
    {
        if (_liveDisplayGuardTimer is not null)
            return;

        _liveDisplayGuardTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _liveDisplayGuardTimer.Tick += (_, _) => ApplyLiveDisplayGuard();
        _liveDisplayGuardTimer.Start();
    }

    private void ApplyLiveDisplayGuard()
    {
        if (SourceCombo.SelectedIndex == 0 || string.IsNullOrWhiteSpace(_selectedStreamKey))
            return;

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

        var waveformComplete = snapshot.Waveform.PhaseA.Count >= snapshot.SamplesPerCycle * 2 &&
                               snapshot.Waveform.PhaseB.Count == snapshot.Waveform.PhaseA.Count &&
                               snapshot.Waveform.PhaseC.Count == snapshot.Waveform.PhaseA.Count &&
                               snapshot.Waveform.Residual.Count == snapshot.Waveform.PhaseA.Count;
        var coherent = snapshot.Measurement.SmvTrust.AllowsMeasurement && waveformComplete;

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

        if (_lastCoherentWaveform is not null)
            SmvScope.Frame = _lastCoherentWaveform with
            {
                PickupPosition = _pickupPosition,
                TripPosition = _tripPosition
            };

        if (_phasorScope is not null && _lastCoherentPhasor is not null)
            _phasorScope.Frame = _lastCoherentPhasor;
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
