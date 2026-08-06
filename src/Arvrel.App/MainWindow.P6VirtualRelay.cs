using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.App.Controls.VirtualRelay;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _p6VirtualRelayInstalled;
    private bool _p6VirtualRelayLoadHooked;

    internal void InitializeP6VirtualRelay()
    {
        if (_p6VirtualRelayInstalled)
            return;

        // StartupUri may activate the application before the MainWindow visual
        // tree is fully loaded. Use the window's own lifecycle as the fallback,
        // not a module initializer or class-handler side channel.
        if (!IsLoaded)
        {
            if (!_p6VirtualRelayLoadHooked)
            {
                _p6VirtualRelayLoadHooked = true;
                Loaded += P6Window_Loaded;
            }

            return;
        }

        InstallP6VirtualRelay();
    }

    private void P6Window_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= P6Window_Loaded;
        _p6VirtualRelayLoadHooked = false;
        InstallP6VirtualRelay();
    }

    private void InstallP6VirtualRelay()
    {
        if (_p6VirtualRelayInstalled)
            return;

        var relayHost = ResolveP6RelayWorkspaceCard(HealthyLed);
        if (relayHost is null)
        {
            const string failure = "P6 native relay host was not found; legacy faceplate remains active.";
            StatusText.Text = failure;
            AddEvent("P6 ERROR", failure);
            return;
        }

        var relay = new VirtualRelayControl();
        relay.ResetRequested += P6Relay_ResetRequested;

        // Replace only the complete relay card in the right workspace column.
        relayHost.Child = relay;
        relayHost.Padding = new Thickness(0);
        relayHost.Background = Brushes.Transparent;
        relayHost.BorderBrush = Brushes.Transparent;
        relayHost.BorderThickness = new Thickness(0);
        relayHost.ClipToBounds = false;
        relayHost.Effect = null;

        // P6 already owns enclosure depth and shadow. Remove exactly one host
        // card around it so the relay reads as a calm hardware object rather
        // than a panel nested inside another bright enterprise card.
        CalmP6WorkspaceChrome(relayHost);

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

        if (!EngineModeText.Text.Contains("P6", StringComparison.Ordinal))
            EngineModeText.Text = $"{EngineModeText.Text} · P6";

        AddEvent("P6", "Native virtual relay faceplate mounted");
        StatusText.Text = "P6 native virtual relay faceplate active.";
    }

    private static void CalmP6WorkspaceChrome(Border relayHost)
    {
        var workspaceFrame = P6VisualAncestors<Border>(relayHost).FirstOrDefault();
        if (workspaceFrame is null)
            return;

        workspaceFrame.Background = Brushes.Transparent;
        workspaceFrame.BorderBrush = Brushes.Transparent;
        workspaceFrame.BorderThickness = new Thickness(0);
        workspaceFrame.Padding = new Thickness(0);
        workspaceFrame.Effect = null;
        workspaceFrame.ClipToBounds = false;
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

    private static Border? ResolveP6RelayWorkspaceCard(DependencyObject child)
        => P6VisualAncestors<Border>(child)
            .FirstOrDefault(border => border.Child is Border);

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
