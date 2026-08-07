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
        if (!_virtualInjectionInitialized ||
            _virtualInjectionView is null ||
            _virtualInjectionStatusBadge is null ||
            _analysisModeButtons.Count == 0)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(InitializeCtObservability));
            return;
        }

        if (!InstallCtToolbarButton())
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(InitializeCtObservability));
            return;
        }

        _ctObservabilityInitialized = true;
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

    private bool InstallCtToolbarButton()
    {
        if (_ctObservabilityButton is not null)
            return true;

        var analysisButton = _analysisModeButtons.Values.FirstOrDefault();
        if (analysisButton?.Parent is not StackPanel viewPanel ||
            viewPanel.Parent is not Border viewGroup ||
            viewGroup.Parent is not StackPanel controlsLine)
        {
            return false;
        }

        _ctObservabilityButton = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "CT IDEAL",
            MinWidth = 76,
            Height = 28,
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Open current-transformer settings, magnetic state, per-channel diagnostics, event timing, and ideal-versus-secondary waveform/flux comparison."
        };
        _ctObservabilityButton.Click += (_, _) => OpenCtObservabilityWindow();
        controlsLine.Children.Add(_ctObservabilityButton);
        return true;
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
                RestartCtStudyEvent,
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
        var evidence = new CtObservabilityWaveformEvidence(
            step.IdealCurrentReference,
            step.Waveform.PhaseA,
            step.Waveform.PhaseB,
            step.Waveform.PhaseC,
            step.Waveform.Residual,
            step.Waveform.NominalSamplesPerCycle);
        var snapshot = CtObservabilityProjector.Project(
            _scenario.ActiveProfile,
            step.CtSaturation,
            step.CurrentTransformerState,
            step.SourceSampleIndex,
            evidence,
            step.IsRunning);
        UpdateCtToolbarButton(snapshot);

        if (_ctObservabilityWindow is null)
            return;

        _ctObservabilityWindow.Update(snapshot, BuildCtComparisonFrame(step));
    }

    private CtComparisonFrame BuildCtComparisonFrame(Services.ScenarioStep step)
    {
        IReadOnlyList<double> phaseAFlux = Array.Empty<double>();
        IReadOnlyList<double> phaseBFlux = Array.Empty<double>();
        IReadOnlyList<double> phaseCFlux = Array.Empty<double>();
        IReadOnlyList<double> neutralFlux = Array.Empty<double>();
        var profile = _scenario.ActiveProfile;
        if (step.IsRunning && profile.CurrentTransformer.Enabled)
        {
            var reference = step.IdealCurrentReference;
            var settings = profile.CurrentTransformer;
            phaseAFlux = CtSaturationModel.Apply(
                reference.PhaseA,
                reference.SampleRateHz,
                profile.FrequencyHz,
                settings,
                step.CurrentTransformerState.PhaseA).FluxPerUnit;
            phaseBFlux = CtSaturationModel.Apply(
                reference.PhaseB,
                reference.SampleRateHz,
                profile.FrequencyHz,
                settings,
                step.CurrentTransformerState.PhaseB).FluxPerUnit;
            phaseCFlux = CtSaturationModel.Apply(
                reference.PhaseC,
                reference.SampleRateHz,
                profile.FrequencyHz,
                settings,
                step.CurrentTransformerState.PhaseC).FluxPerUnit;
            if (profile.ExplicitNeutralCurrent)
            {
                neutralFlux = CtSaturationModel.Apply(
                    reference.NeutralOrResidual,
                    reference.SampleRateHz,
                    profile.FrequencyHz,
                    settings,
                    step.CurrentTransformerState.Neutral).FluxPerUnit;
            }
        }

        return new CtComparisonFrame(
            step.IdealCurrentReference,
            step.Waveform.PhaseA,
            step.Waveform.PhaseB,
            step.Waveform.PhaseC,
            step.Waveform.Residual,
            phaseAFlux,
            phaseBFlux,
            phaseCFlux,
            neutralFlux,
            step.Waveform.NominalSamplesPerCycle);
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
        _ctObservabilityButton.ToolTip = $"{snapshot.StatusText}\n{snapshot.SettingsSummary}\n{snapshot.RuntimeSummary}\n{snapshot.EventSummary}";
    }

    private void RestartCtStudyEvent()
    {
        if (!_scenario.RestartEvent())
        {
            StatusText.Text = "CT study event restart is available only while the internal virtual source is running.";
            return;
        }

        AddEvent("CT EVENT", "Source event restarted at t=0; CT remanence reapplied");
        SetVirtualInjectionStatus("EVENT RESTART · REBUILDING", WarningBrush, "#FBF2E3", "#E2C58F");
        StatusText.Text = "Virtual source event restarted at t=0 with configured CT remanence. Process sample counting remains continuous; pickup and trip are restrained for one coherent cycle.";
        RenderInitialFrame();
        RefreshPhasorFrame();
        RefreshCtObservability();
    }

    private void ResetCtRuntimeState()
    {
        if (!_scenario.ResetCurrentTransformerState())
        {
            StatusText.Text = "CT state reset is available only while a nonlinear CT model is running.";
            return;
        }

        AddEvent("CT RESET", "Configured remanence reapplied; source event time retained");
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
        StatusText.Text = "Runtime CT flux set to zero without changing configured remanence. Source phase and DC decay remain continuous; pickup and trip are restrained for one coherent cycle.";
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
