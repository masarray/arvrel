using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls.VirtualRelay;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _p6VirtualRelayInstalled;

    internal void InitializeP6VirtualRelay()
    {
        if (_p6VirtualRelayInstalled)
            return;

        // The LCD and measurement presenters initialize synchronously at Loaded.
        // Install P6 one dispatcher tier later, then bind those state presenters
        // to the new native control.
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(InstallP6VirtualRelay));
    }

    private void InstallP6VirtualRelay()
    {
        if (_p6VirtualRelayInstalled)
            return;

        var oldHealthy = HealthyLed;
        var relayHost = P6VisualAncestors<Border>(oldHealthy).LastOrDefault();
        if (relayHost is null)
            return;

        var relay = new VirtualRelayControl();
        relay.ResetRequested += P6Relay_ResetRequested;

        // Replace the complete legacy faceplate subtree. The surrounding right
        // workspace column remains intact, including sizing and responsiveness.
        relayHost.Child = relay;
        relayHost.Padding = new Thickness(0);
        relayHost.Background = Brushes.Transparent;
        relayHost.BorderBrush = Brushes.Transparent;
        relayHost.BorderThickness = new Thickness(0);
        relayHost.ClipToBounds = false;

        // Redirect the existing protection and process-bus presentation logic to
        // P6 binding anchors. No protection-domain state is copied or recreated.
        LcdHeaderText = relay.LcdHeaderText;
        LcdIaText = relay.LcdIaText;
        LcdIbText = relay.LcdIbText;
        LcdIcText = relay.LcdIcText;
        LcdResidualText = relay.LcdResidualText;
        LcdStatusText = relay.LcdStatusText;
        RelayFooterText = relay.RelayFooterText;

        HealthyLed = relay.HealthyLamp.Lens;
        PickupLed = relay.PickupLamp.Lens;
        TripLed = relay.TripLamp.Lens;
        PhaseALed = relay.PhaseALamp.Lens;
        PhaseBLed = relay.PhaseBLamp.Lens;
        PhaseCLed = relay.PhaseCLamp.Lens;
        EarthLed = relay.EarthLamp.Lens;
        BlockLed = relay.BlockLamp.Lens;

        HealthyLed.Fill = HealthyBrush;
        PickupLed.Fill = LedOffBrush;
        TripLed.Fill = LedOffBrush;
        PhaseALed.Fill = LedOffBrush;
        PhaseBLed.Fill = LedOffBrush;
        PhaseCLed.Fill = LedOffBrush;
        EarthLed.Fill = LedOffBrush;
        BlockLed.Fill = LedOffBrush;

        _p6VirtualRelayInstalled = true;
        RebindRelayStatePresentersToP6();
    }

    private void RebindRelayStatePresentersToP6()
    {
        StopRelayMeasurementHome();
        StopRelayFaceplate();

        _relayMeasurementHomeInitialized = false;
        _relayLcdContentHost = null;
        _relayMeasurementHomePanel = null;
        _relayLcdHomePhasor = null;
        _relayLcdDisplayedPhasor = null;
        _relayLcdPhasorPresentationSignature = null;
        _relayLcdPhasorSourceIdentity = null;
        Array.Clear(_relayLcdCurrentValues);
        Array.Clear(_relayLcdVoltageValues);

        _relayFaceplateInitialized = false;
        _relayLcdHeader = null;
        _relayLcdBody = null;
        _relayLcdFooter = null;

        InitializeRelayFaceplate();
        InitializeRelayMeasurementHome();
        RefreshRelayAnnunciation();
    }

    private void P6Relay_ResetRequested(object sender, RoutedEventArgs e)
        => Reset_Click(sender, e);

    private static IEnumerable<T> P6VisualAncestors<T>(DependencyObject child)
        where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(child);
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
                yield return match;
        }
    }
}

internal static class P6VirtualRelayBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.InitializeP6VirtualRelay();
    }
}
