using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Arvrel.App.Controls;
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

        // Lamp geometry is owned by RelayIndicatorLampBehavior. Annunciation
        // changes only logical colour/state; it must never make TRIP or a phase
        // lamp physically larger than HEALTHY or SMV BLOCK.
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

        SetAnnunciationState(PickupLed, indication.PickupActive ? RelayIndicatorState.Orange : RelayIndicatorState.Off);
        SetAnnunciationState(TripLed, indication.TripLatched ? RelayIndicatorState.Red : RelayIndicatorState.Off);
        SetAnnunciationState(PhaseALed, ResolveAnnunciationState(indication.PhaseA));
        SetAnnunciationState(PhaseBLed, ResolveAnnunciationState(indication.PhaseB));
        SetAnnunciationState(PhaseCLed, ResolveAnnunciationState(indication.PhaseC));
        SetAnnunciationState(EarthLed, ResolveAnnunciationState(indication.Earth));
    }

    private void ClearRelayAnnunciation()
    {
        _relayAnnunciationSourceIdentity = null;
        _relayAnnunciationLatch.Reset();
        SetAnnunciationState(PickupLed, RelayIndicatorState.Off);
        SetAnnunciationState(TripLed, RelayIndicatorState.Off);
        SetAnnunciationState(PhaseALed, RelayIndicatorState.Off);
        SetAnnunciationState(PhaseBLed, RelayIndicatorState.Off);
        SetAnnunciationState(PhaseCLed, RelayIndicatorState.Off);
        SetAnnunciationState(EarthLed, RelayIndicatorState.Off);
    }

    private static void SetAnnunciationState(System.Windows.Shapes.Ellipse lamp, RelayIndicatorState state)
        => RelayIndicatorLampBehavior.SetPinnedState(lamp, state);

    private static RelayIndicatorState ResolveAnnunciationState(RelayLampState state)
        => state switch
        {
            RelayLampState.Pickup => RelayIndicatorState.Orange,
            RelayLampState.Trip => RelayIndicatorState.Red,
            _ => RelayIndicatorState.Off
        };
}
