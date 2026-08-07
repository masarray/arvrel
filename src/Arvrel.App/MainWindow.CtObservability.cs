using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.Application.Laboratory;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _ctObservabilityInitialized;
    private Button? _ctObservabilityButton;
    private CtObservabilityWindow? _ctObservabilityWindow;
    private DispatcherTimer? _ctObservabilityTimer;

    internal void InitializeCtObservability()
    {
        if (_ctObservabilityInitialized)
            return;
        if (!_virtualInjectionInitialized || _virtualInjectionView is null || _virtualInjectionStatusBadge is null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(InitializeCtObservability));
            return;
        }

        _ctObservabilityInitialized = true;
        InstallCtToolbarButton();
        ReplaceVirtualInjectionApplyHandler();

        _ctObservabilityTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _ctObservabilityTimer.Tick += (_, _) => RefreshCtObservability();
        _ctObservabilityTimer.Start();
        Closed += (_, _) =>
        {
            _ctObservabilityTimer?.Stop();
            _ctObservabilityWindow?.Close();
        };
        RefreshCtObservability();
    }

    private void InstallCtToolbarButton()
    {
        if (_virtualInjectionStatusBadge?.Parent is not Grid toolbar)
            return;

        toolbar.Children.Remove(_virtualInjectionStatusBadge);
        var statusPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statusPanel, 6);
        toolbar.Children.Add(statusPanel);

        _ctObservabilityButton = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "CT IDEAL",
            MinWidth = 92,
            Height = 28,
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Open current-transformer settings, magnetic state, per-channel diagnostics, and ideal-versus-secondary waveform comparison."
        };
        _ctObservabilityButton.Click += (_, _) => OpenCtObservabilityWindow();
        statusPanel.Children.Add(_ctObservabilityButton);
        statusPanel.Children.Add(_virtualInjectionStatusBadge);
    }

    private void ReplaceVirtualInjectionApplyHandler()
    {
        if (_virtualInjectionApplyTimer is null)
            return;
        _virtualInjectionApplyTimer.Tick -= VirtualInjectionApplyTimer_Tick;
        _virtualInjectionApplyTimer.Tick += CtAwareVirtualInjectionApplyTimer_Tick;
    }

    private void CtAwareVirtualInjectionApplyTimer_Tick(object? sender, EventArgs e)
    {
        _virtualInjectionApplyTimer?.Stop();
        TryApplyCtAwareVirtualInjectionEditor();
    }

    private bool TryApplyCtAwareVirtualInjectionEditor()
    {
        if (_virtualInjectionFrequencyText is null)
            return false;

        if (!VirtualInjectionRow.TryParseEngineeringDouble(_virtualInjectionFrequencyText.Text, out var frequency) ||
            !double.IsFinite(frequency) ||
            frequency < VirtualInjectionProfile.MinimumFrequencyHz ||
            frequency > VirtualInjectionProfile.MaximumFrequencyHz)
        {
            SetVirtualInjectionInvalid($"Frequency must be {VirtualInjectionProfile.MinimumFrequencyHz:0}–{VirtualInjectionProfile.MaximumFrequencyHz:0} Hz.");
            _virtualInjectionFrequencyText.BorderBrush = TripBrush;
            return false;
        }
        _virtualInjectionFrequencyText.ClearValue(Control.BorderBrushProperty);

        var active = _scenario.ActiveProfile;
        var channels = new Dictionary<VirtualInjectionSignal, VirtualInjectionChannel>();
        foreach (var row in _virtualInjectionRows)
        {
            if (!row.TryCreateChannel(out var channel, out var error))
            {
                SetVirtualInjectionInvalid(error);
                return false;
            }
            channels[row.Signal] = channel;
        }

        try
        {
            var profile = VirtualInjectionDirectEditorMerge.Apply(
                active,
                _pendingInjectionName,
                frequency,
                channels);

            var changed = _scenario.ApplyProfile(profile);
            UpdateVirtualInjectionProvenance();
            if (changed)
            {
                SetVirtualInjectionStatus("APPLIED · REBUILDING", WarningBrush, "#FBF2E3", "#E2C58F");
                AddEvent("INJECTION", $"{profile.Name} · {profile.Fingerprint()[..12]}");
                StatusText.Text = profile.CurrentTransformer.Enabled
                    ? "Virtual injection and CT study model applied atomically. Pickup and trip are restrained until one coherent cycle is rebuilt."
                    : "Virtual injection applied atomically. Pickup and trip are restrained until one coherent cycle is rebuilt.";
            }
            else
            {
                SetVirtualInjectionStatus("READY", HealthyBrush, "#EAF5EC", "#B9D8BF");
            }

            RenderInitialFrame();
            RefreshPhasorFrame();
            RefreshCtObservability();
            return true;
        }
        catch (ArgumentException ex)
        {
            SetVirtualInjectionInvalid(ex.Message);
            return false;
        }
    }

    private void OpenCtObservabilityWindow()
    {
        if (SourceCombo.SelectedIndex != 0)
            return;

        if (_ctObservabilityWindow is null)
        {
            _ctObservabilityWindow = new CtObservabilityWindow(
                ResetCtRuntimeState,
                DemagnetizeCtRuntimeState)
            {
                Owner = this
            };
            _ctObservabilityWindow.Closed += (_, _) => _ctObservabilityWindow = null;
            _ctObservabilityWindow.Show();
        }
        else
        {
            if (_ctObservabilityWindow.WindowState == WindowState.Minimized)
                _ctObservabilityWindow.WindowState = WindowState.Normal;
            _ctObservabilityWindow.Activate();
        }
        RefreshCtObservability();
    }

    private void RefreshCtObservability()
    {
        if (!_ctObservabilityInitialized || SourceCombo.SelectedIndex != 0)
            return;

        var step = _scenario.Advance(TimeSpan.Zero, _pickupPosition, _tripPosition);
        var snapshot = CtObservabilityProjector.Project(
            _scenario.ActiveProfile,
            step.CtSaturation,
            step.CurrentTransformerState,
            step.SourceSampleIndex,
            step.IsRunning);
        UpdateCtToolbarButton(snapshot);

        if (_ctObservabilityWindow is null)
            return;
        var comparison = new CtComparisonFrame(
            step.IdealCurrentReference,
            step.Waveform.PhaseA,
            step.Waveform.PhaseB,
            step.Waveform.PhaseC,
            step.Waveform.Residual,
            step.Waveform.NominalSamplesPerCycle);
        _ctObservabilityWindow.Update(snapshot, comparison);
    }

    private void UpdateCtToolbarButton(CtObservabilitySnapshot snapshot)
    {
        if (_ctObservabilityButton is null)
            return;

        _ctObservabilityButton.Content = snapshot.BadgeText;
        _ctObservabilityButton.Foreground = snapshot.Status switch
        {
            CtObservabilityStatus.Saturated => TripBrush,
            CtObservabilityStatus.Nonlinear => WarningBrush,
            _ => HealthyBrush
        };
        _ctObservabilityButton.ToolTip = $"{snapshot.StatusText}\n{snapshot.SettingsSummary}\n{snapshot.RuntimeSummary}";
    }

    private void ResetCtRuntimeState()
    {
        if (!_scenario.ResetCurrentTransformerState())
        {
            StatusText.Text = "CT state reset is available only while a nonlinear CT model is running.";
            return;
        }

        AddEvent("CT RESET", "Configured remanence reapplied; source time retained");
        SetVirtualInjectionStatus("CT RESET · REBUILDING", WarningBrush, "#FBF2E3", "#E2C58F");
        StatusText.Text = "CT magnetic state reset to configured remanence. Source phase and DC decay remain continuous; pickup and trip are restrained for one coherent cycle.";
        RenderInitialFrame();
        RefreshPhasorFrame();
        RefreshCtObservability();
    }

    private void DemagnetizeCtRuntimeState()
    {
        if (!_scenario.DemagnetizeCurrentTransformer())
        {
            StatusText.Text = "CT demagnetize is available only while a nonlinear CT model is running.";
            return;
        }

        AddEvent("CT DEMAG", "Runtime flux forced to zero; configured remanence retained");
        SetVirtualInjectionStatus("CT DEMAG · REBUILDING", WarningBrush, "#FBF2E3", "#E2C58F");
        StatusText.Text = "Runtime CT flux set to zero without changing the configured remanence. Source phase and DC decay remain continuous; pickup and trip are restrained for one coherent cycle.";
        RenderInitialFrame();
        RefreshPhasorFrame();
        RefreshCtObservability();
    }
}

internal static class CtObservabilityBootstrap
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
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(window.InitializeCtObservability));
    }
}
