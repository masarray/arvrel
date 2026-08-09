using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    [ModuleInitializer]
    internal static void InitializeRelayResetSeparation()
    {
        // Reset is a relay-equipment command. Registering at Button class level
        // lets this authority run before legacy XAML/lambda click handlers that
        // historically coupled relay reset to DeterministicLabScenario.Reset().
        EventManager.RegisterClassHandler(
            typeof(Button),
            Button.ClickEvent,
            new RoutedEventHandler(OnRelayResetButtonClick));
    }

    private static void OnRelayResetButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !IsRelayResetCommand(button))
            return;

        var hostWindow = Window.GetWindow(button);
        var mainWindow = hostWindow switch
        {
            MainWindow main => main,
            AdvancedInjectionWindow { Owner: MainWindow owner } => owner,
            _ => null
        };
        if (mainWindow is null)
            return;

        // A handled routed event prevents the older Reset_Click handler or the
        // editor lambda from running after the equipment-separated reset.
        e.Handled = true;
        mainWindow.ResetRelayEquipmentOnly();
    }

    private static bool IsRelayResetCommand(Button button)
    {
        var label = ExtractButtonLabel(button.Content);
        return label.Equals("Reset", StringComparison.OrdinalIgnoreCase) ||
               label.Equals("Reset trip", StringComparison.OrdinalIgnoreCase) ||
               label.Equals("Reset relay", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractButtonLabel(object? content)
    {
        return content switch
        {
            string text => text.Trim(),
            TextBlock textBlock => textBlock.Text.Trim(),
            ContentControl control => ExtractButtonLabel(control.Content),
            Panel panel => string.Join(
                " ",
                panel.Children
                    .OfType<UIElement>()
                    .Select(ExtractElementLabel)
                    .Where(text => !string.IsNullOrWhiteSpace(text))).Trim(),
            _ => string.Empty
        };
    }

    private static string ExtractElementLabel(UIElement element)
    {
        return element switch
        {
            TextBlock textBlock => textBlock.Text.Trim(),
            ContentControl control => ExtractButtonLabel(control.Content),
            Panel panel => string.Join(
                " ",
                panel.Children
                    .OfType<UIElement>()
                    .Select(ExtractElementLabel)
                    .Where(text => !string.IsNullOrWhiteSpace(text))).Trim(),
            _ => string.Empty
        };
    }

    private void ResetRelayEquipmentOnly()
    {
        ResetTransitionMarkers();

        if (SourceCombo.SelectedIndex == 0)
        {
            var wasRunning = _scenario.IsRunning;
            var configuredProfile = _scenario.ActiveProfile;
            var configuredFingerprint = _scenario.InjectionFingerprint;
            var outputFingerprint = _scenario.OutputFingerprint;
            var heldTripCapture = _closedLoopBench?.TripCapture;

            _internalEngine.Reset();
            _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
            ClearRelayAnnunciation();

            // With output OFF, let the relay BO release, bounce and TESTSET BI
            // debounce path settle naturally. This makes the BI lamps fall through
            // the same modeled path used to assert them; no software-forced BI reset.
            if (!wasRunning && _closedLoopBench is not null)
            {
                var settled = _closedLoopBench.Advance(_closedLoopBench.FeedbackSettleTime);
                _snapshot = settled.Protection;
            }

            // Re-render the relay against the source that is already present.
            // Do not call scenario.Reset(), Restart(), StopInjection(), or apply
            // a nominal preset: the virtual test set is separate equipment.
            var currentSource = _scenario.Advance(
                TimeSpan.Zero,
                _pickupPosition,
                _tripPosition);
            RenderInternal(currentSource, _snapshot);
            RefreshPhasorFrame();
            RefreshVirtualInjectionRunStopPresentation();

            AddEvent(
                "RELAY RESET",
                $"Trip latch and timers cleared; injection {(wasRunning ? "RUNNING" : "STOPPED")} · {configuredProfile.Name}");
            StatusText.Text = wasRunning
                ? $"Relay reset complete. Virtual injection '{configuredProfile.Name}' remains RUNNING."
                : heldTripCapture is not null
                    ? $"Relay reset complete. TESTSET feedback released; trip capture from run {heldTripCapture.TestRunId} remains frozen for inspection."
                    : $"Relay reset complete. Virtual injection '{configuredProfile.Name}' remains STOPPED.";

            // These values are intentionally observed after reset. They are
            // included in the event tooltip/evidence path and make accidental
            // source mutation visible during manual diagnostics.
            RelayFooterText.ToolTip =
                $"Relay reset did not change the virtual source.\n" +
                $"Configured fingerprint {configuredFingerprint}\n" +
                $"Effective output fingerprint {outputFingerprint}\n" +
                $"Output state {_scenario.OutputState}";
            return;
        }

        _processBus.ResetProtection(_selectedStreamKey);
        ClearRelayAnnunciation();
        AddEvent("RELAY RESET", "Selected live/replay relay state reset; capture source unchanged");
        RenderSelectedProcessBusStream();
        StatusText.Text = "Relay reset complete. The selected live/replay source remains unchanged.";
    }
}
