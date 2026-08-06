using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Shapes;
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

        // P6 owns all physical lamp geometry. Annunciation is deliberately a
        // state-only authority and may change only the logical lens colour.
        _relayAnnunciationTimer = new DispatcherTimer(DispatcherPriority.Render)
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
                {
                    ClearRelayAnnunciation();
                    return;
                }

                snapshot = runtime.Protection;
                sourceIdentity = runtime.Stream.Key;
            }
        }
        catch (InvalidOperationException)
        {
            ClearRelayAnnunciation();
            return;
        }

        if (!string.Equals(_relayAnnunciationSourceIdentity, sourceIdentity, StringComparison.Ordinal))
        {
            _relayAnnunciationSourceIdentity = sourceIdentity;
            _relayAnnunciationLatch.Reset();
        }

        var indication = _relayAnnunciationLatch.Observe(snapshot);

        SetAnnunciationState(
            PickupLed,
            indication.PickupActive ? P6AnnunciationLampState.Amber : P6AnnunciationLampState.Off);
        SetAnnunciationState(
            TripLed,
            indication.TripLatched ? P6AnnunciationLampState.Red : P6AnnunciationLampState.Off);
        SetAnnunciationState(PhaseALed, ResolveAnnunciationState(indication.PhaseA));
        SetAnnunciationState(PhaseBLed, ResolveAnnunciationState(indication.PhaseB));
        SetAnnunciationState(PhaseCLed, ResolveAnnunciationState(indication.PhaseC));
        SetAnnunciationState(EarthLed, ResolveAnnunciationState(indication.Earth));
    }

    private void ClearRelayAnnunciation()
    {
        _relayAnnunciationSourceIdentity = null;
        _relayAnnunciationLatch.Reset();
        SetAnnunciationState(PickupLed, P6AnnunciationLampState.Off);
        SetAnnunciationState(TripLed, P6AnnunciationLampState.Off);
        SetAnnunciationState(PhaseALed, P6AnnunciationLampState.Off);
        SetAnnunciationState(PhaseBLed, P6AnnunciationLampState.Off);
        SetAnnunciationState(PhaseCLed, P6AnnunciationLampState.Off);
        SetAnnunciationState(EarthLed, P6AnnunciationLampState.Off);
    }

    private static void SetAnnunciationState(Ellipse lamp, P6AnnunciationLampState state)
    {
        var brush = state switch
        {
            P6AnnunciationLampState.Amber => WarningBrush,
            P6AnnunciationLampState.Red => TripBrush,
            _ => LedOffBrush
        };

        if (!ReferenceEquals(lamp.Fill, brush))
            lamp.Fill = brush;
    }

    private static P6AnnunciationLampState ResolveAnnunciationState(RelayLampState state)
        => state switch
        {
            RelayLampState.Pickup => P6AnnunciationLampState.Amber,
            RelayLampState.Trip => P6AnnunciationLampState.Red,
            _ => P6AnnunciationLampState.Off
        };

    private enum P6AnnunciationLampState
    {
        Off,
        Amber,
        Red
    }
}
