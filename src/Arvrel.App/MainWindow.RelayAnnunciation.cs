using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private readonly RelayAnnunciationLatch _relayAnnunciationLatch = new();
    private DispatcherTimer? _relayAnnunciationTimer;
    private string? _relayAnnunciationSourceIdentity;

    [ModuleInitializer]
    internal static void InitializeRelayAnnunciationClassHandler()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnRelayAnnunciationWindowLoaded));
    }

    private static void OnRelayAnnunciationWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StartRelayAnnunciation();
    }

    private void StartRelayAnnunciation()
    {
        if (_relayAnnunciationTimer is not null)
            return;

        _relayAnnunciationTimer = new DispatcherTimer(DispatcherPriority.ContextIdle)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _relayAnnunciationTimer.Tick += RelayAnnunciationTimer_Tick;
        _relayAnnunciationTimer.Start();
        Closed += (_, _) => StopRelayAnnunciation();
        RefreshRelayAnnunciation();
    }

    private void StopRelayAnnunciation()
    {
        if (_relayAnnunciationTimer is null)
            return;

        _relayAnnunciationTimer.Stop();
        _relayAnnunciationTimer.Tick -= RelayAnnunciationTimer_Tick;
        _relayAnnunciationTimer = null;
    }

    private void RelayAnnunciationTimer_Tick(object? sender, EventArgs e)
        => RefreshRelayAnnunciation();

    private void RefreshRelayAnnunciation()
    {
        ProtectionSnapshot snapshot;
        string sourceIdentity;
        try
        {
            if (SourceCombo.SelectedIndex == 0)
            {
                snapshot = _snapshot;
                sourceIdentity = "internal";
            }
            else
            {
                var runtime = _processBus.GetSnapshot(_selectedStreamKey, ViewCombo.SelectedIndex == 1);
                if (string.IsNullOrWhiteSpace(runtime.Stream.Key))
                    return;

                snapshot = runtime.Protection;
                sourceIdentity = runtime.Stream.Key;
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (!string.Equals(_relayAnnunciationSourceIdentity, sourceIdentity, StringComparison.Ordinal))
        {
            _relayAnnunciationSourceIdentity = sourceIdentity;
            _relayAnnunciationLatch.Reset();
        }

        var indication = _relayAnnunciationLatch.Observe(snapshot);

        // The common PICKUP lamp is momentary. Once trip is latched the red TRIP
        // lamp and the red latched phase/earth causes take visual priority.
        PickupLed.Fill = indication.PickupActive ? WarningBrush : LedOffBrush;
        TripLed.Fill = indication.TripLatched ? TripBrush : LedOffBrush;
        PhaseALed.Fill = ResolveAnnunciationBrush(indication.PhaseA);
        PhaseBLed.Fill = ResolveAnnunciationBrush(indication.PhaseB);
        PhaseCLed.Fill = ResolveAnnunciationBrush(indication.PhaseC);
        EarthLed.Fill = ResolveAnnunciationBrush(indication.Earth);
    }

    private static Brush ResolveAnnunciationBrush(RelayLampState state)
        => state switch
        {
            RelayLampState.Pickup => WarningBrush,
            RelayLampState.Trip => TripBrush,
            _ => LedOffBrush
        };
}
