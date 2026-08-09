using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.Application.Laboratory;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _relayResetInProgress;

    [ModuleInitializer]
    internal static void InitializeRelayResetSeparation()
    {
        // RESET is a relay-equipment command. Intercept legacy button routes so every
        // visible reset entry point reaches the same authoritative transaction.
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

        // A handled routed event prevents older Reset_Click/editor handlers from
        // becoming a second reset authority for the same physical command.
        e.Handled = true;
        mainWindow.ExecuteRelayResetCommand();
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

    private void ExecuteRelayResetCommand()
    {
        if (_relayResetInProgress)
            return;

        _relayResetInProgress = true;
        try
        {
            StatusText.ToolTip = null;
            ResetRelayEquipmentOnly();
            NotifyRelayOperatorReset();
            NotifyRelayEvidenceReset();
        }
        finally
        {
            _relayResetInProgress = false;
        }
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
            ClosedLoopRelayResetResult? resetResult = null;

            if (_closedLoopBench is not null)
            {
                // One operator click owns the complete reset transaction. If auto-stop
                // froze the simulation at the BI1 edge, first let the causal relay
                // acquisition observe the already-OFF source, then clear the relay once
                // and continue until BO1/BO2 and TESTSET BI1/BI2 are physically idle.
                resetResult = ClosedLoopRelayResetTransaction.Execute(
                    _closedLoopBench,
                    _internalEngine);
                _snapshot = resetResult.FinalStep.Protection;
            }
            else
            {
                _internalEngine.Reset();
                _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
            }

            ClearRelayAnnunciation();

            if (resetResult is not null)
            {
                var displayStep = _scenario.Project(
                    resetResult.FinalStep.Source,
                    _pickupPosition,
                    _tripPosition) with
                {
                    Measurement = resetResult.FinalStep.RelayMeasurement
                };
                RenderInternal(displayStep, _snapshot);
            }
            else
            {
                // Legacy fallback only when the closed-loop bench has not initialized.
                // Do not reset/restart the virtual source; relay and test set are
                // separate equipment.
                var currentSource = _scenario.Advance(
                    TimeSpan.Zero,
                    _pickupPosition,
                    _tripPosition);
                RenderInternal(currentSource, _snapshot);
            }

            RefreshPhasorFrame();
            RefreshVirtualInjectionRunStopPresentation();

            if (resetResult is { ReadyToRearm: true })
            {
                AddEvent(
                    "RELAY RESET",
                    $"ONE CLICK · READY TO RE-ARM · settle {resetResult.SimulatedSettleTime.TotalMilliseconds:0.000} ms · {configuredProfile.Name}");
                StatusText.Text = heldTripCapture is not null
                    ? $"Relay reset complete · READY TO RE-ARM · BO/BI feedback released in {resetResult.SimulatedSettleTime.TotalMilliseconds:0.000} ms simulated time · trip capture run {heldTripCapture.TestRunId} retained."
                    : $"Relay reset complete · READY TO RE-ARM · BO/BI feedback released in {resetResult.SimulatedSettleTime.TotalMilliseconds:0.000} ms simulated time.";
            }
            else if (resetResult is { SourceWasRunning: true })
            {
                AddEvent("RELAY RESET", "Reset applied while virtual source remains energized; protection may reassert");
                StatusText.Text =
                    "Relay reset applied once. Virtual injection remains RUNNING, so pickup/trip may reassert until the applied fault quantity is removed or output is stopped.";
            }
            else if (resetResult is not null)
            {
                AddEvent("RELAY RESET TIMEOUT", resetResult.Detail);
                StatusText.Text = $"Relay reset incomplete: {resetResult.Detail}";
            }
            else
            {
                AddEvent(
                    "RELAY RESET",
                    $"Trip latch and timers cleared; injection {(wasRunning ? "RUNNING" : "STOPPED")} · {configuredProfile.Name}");
                StatusText.Text = wasRunning
                    ? $"Relay reset complete. Virtual injection '{configuredProfile.Name}' remains RUNNING."
                    : $"Relay reset complete. Virtual injection '{configuredProfile.Name}' remains STOPPED.";
            }

            RelayFooterText.ToolTip =
                $"Relay reset did not change the virtual source.\n" +
                $"Configured fingerprint {configuredFingerprint}\n" +
                $"Effective output fingerprint {outputFingerprint}\n" +
                $"Output state {_scenario.OutputState}" +
                (resetResult is null
                    ? string.Empty
                    : $"\nReset postcondition ready={(resetResult.ReadyToRearm ? 1 : 0)}" +
                      $"\nSimulated settle {resetResult.SimulatedSettleTime.TotalMilliseconds:0.000} ms" +
                      $"\n{resetResult.Detail}");
            return;
        }

        _processBus.ResetProtection(_selectedStreamKey);
        ClearRelayAnnunciation();
        AddEvent("RELAY RESET", "Selected live/replay relay state reset; capture source unchanged");
        RenderSelectedProcessBusStream();
        StatusText.Text = "Relay reset complete. The selected live/replay source remains unchanged.";
    }
}
