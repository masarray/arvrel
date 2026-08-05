using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls;

namespace Arvrel.App;

public partial class MainWindow
{
    private static readonly Brush VirtualInjectionStoppedBrush = Freeze(
        new SolidColorBrush(Color.FromRgb(101, 117, 134)));

    private bool _virtualInjectionRunStopInitialized;
    private DispatcherTimer? _virtualInjectionRunStopTimer;

    internal void InitializeVirtualInjectionRunStop()
    {
        if (_virtualInjectionRunStopInitialized)
            return;
        if (!_virtualInjectionInitialized || _virtualInjectionView is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(InitializeVirtualInjectionRunStop));
            return;
        }

        _virtualInjectionRunStopInitialized = true;

        // The top-right toolbar is the single START/STOP authority. Keeping a
        // second pair of controls inside the editor created visual duplication
        // and made the source state less obvious to the operator.
        RunButton.Click -= RunButton_Click;
        RunButton.Click += VirtualInjectionRunButton_Click;
        InjectFaultButton.Click -= InjectFault_Click;
        InjectFaultButton.Click += VirtualInjectionFaultPreset_Click;
        SourceCombo.SelectionChanged += VirtualInjectionRunStopSourceChanged;

        _virtualInjectionRunStopTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _virtualInjectionRunStopTimer.Tick += (_, _) => RefreshVirtualInjectionRunStopPresentation();
        _virtualInjectionRunStopTimer.Start();
        Closed += (_, _) => _virtualInjectionRunStopTimer?.Stop();

        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Dual, announce: false)));
        RefreshVirtualInjectionRunStopPresentation();
    }

    internal void StopVirtualInjectionRunStop()
    {
        _virtualInjectionRunStopTimer?.Stop();
        _virtualInjectionRunStopTimer = null;
    }

    private async void VirtualInjectionRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
        {
            RunButton_Click(sender, e);
            return;
        }

        RunButton.IsEnabled = false;
        try
        {
            if (_scenario.IsRunning)
                StopVirtualInjectionSource(announce: true);
            else
                StartVirtualInjectionSource(announce: true);
        }
        finally
        {
            RunButton.IsEnabled = true;
            UpdateRunButton();
            RefreshVirtualInjectionRunStopPresentation();
        }

        await Task.CompletedTask;
    }

    private void VirtualInjectionFaultPreset_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
            return;

        ApplyVirtualInjectionPreset("A-G fault", announce: false);
        if (!_scenario.IsRunning)
            StartVirtualInjectionSource(announce: false);

        StatusText.Text = _scenario.IsRunning
            ? "A-G fault injection started. Relay operation follows the measured current, active pickup, delay, and trust state."
            : "A-G fault values were loaded, but injection could not start because the editor contains invalid data.";
        RefreshVirtualInjectionRunStopPresentation();
    }

    private void VirtualInjectionRunStopSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            if (_scenario.IsRunning)
                _scenario.StopInjection();
            _internalRunning = false;

            if (SourceCombo.SelectedIndex == 0)
            {
                ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Dual, announce: false);
                SyncVirtualInjectionEditorFromProfile(_scenario.ActiveProfile);
                RenderStoppedVirtualOutput();
            }

            UpdateRunButton();
            RefreshVirtualInjectionRunStopPresentation();
        }));
    }

    private bool StartVirtualInjectionSource(bool announce)
    {
        if (SourceCombo.SelectedIndex != 0)
            return false;

        if (!TryApplyVirtualInjectionEditor())
        {
            _internalRunning = false;
            RefreshVirtualInjectionRunStopPresentation();
            return false;
        }

        var changed = _scenario.StartInjection();
        _internalRunning = _scenario.IsRunning;
        if (changed)
        {
            AddEvent("INJ START", $"{_scenario.ActiveProfile.Name} · {_scenario.InjectionFingerprint[..12]}");
            SetVirtualInjectionStatus("STARTING", WarningBrush, "#FBF2E3", "#E2C58F");
        }

        UpdateRunButton();
        RefreshPhasorFrame();
        RefreshVirtualInjectionRunStopPresentation();
        if (announce)
        {
            StatusText.Text = changed
                ? "Virtual output energized. Pickup and trip are enabled after one coherent nominal cycle."
                : "Virtual output is already running.";
        }
        return changed;
    }

    private bool StopVirtualInjectionSource(bool announce)
    {
        if (SourceCombo.SelectedIndex != 0)
            return false;

        var changed = _scenario.StopInjection();
        _internalRunning = false;
        RenderStoppedVirtualOutput();
        if (changed)
            AddEvent("INJ STOP", "Virtual output forced to 0 V / 0 A; configured values retained");

        UpdateRunButton();
        RefreshPhasorFrame();
        RefreshVirtualInjectionRunStopPresentation();
        if (announce)
        {
            StatusText.Text = changed
                ? "Virtual output stopped at 0 V / 0 A. Configured values remain armed."
                : "Virtual output is already stopped.";
        }
        return changed;
    }

    private void RenderStoppedVirtualOutput()
    {
        ScenarioStep? last = null;
        for (var index = 0; index < 8; index++)
        {
            last = _scenario.Advance(TimeSpan.FromMilliseconds(5), _pickupPosition, _tripPosition);
            _snapshot = _internalEngine.Evaluate(last.Measurement);
            ObserveTransitions(_snapshot);
        }

        if (last is not null)
        {
            RenderInternal(last, _snapshot);
            RefreshVirtualInjectionRunStopPresentation();
        }
    }

    private void RefreshVirtualInjectionRunStopPresentation()
    {
        if (!_virtualInjectionRunStopInitialized || SourceCombo.SelectedIndex != 0)
            return;

        var running = _scenario.IsRunning;
        SetTextIfChanged(RunButtonText, running ? "Stop injection" : "Start injection");
        var desiredIcon = running ? LucideIconKind.CircleStop : LucideIconKind.Play;
        if (RunButtonIcon.Kind != desiredIcon)
            RunButtonIcon.Kind = desiredIcon;
        if (RunButtonIcon.Filled != running)
            RunButtonIcon.Filled = running;

        var currentStatus = _virtualInjectionStatusText?.Text ?? string.Empty;
        var preserveValidationStatus = currentStatus.StartsWith("INVALID", StringComparison.Ordinal) ||
                                       currentStatus.StartsWith("EDITING", StringComparison.Ordinal);
        if (!preserveValidationStatus)
        {
            var desiredStatus = !running
                ? "STOPPED"
                : _scenario.WindowStatus == "rebuilding"
                    ? "STARTING"
                    : "RUNNING";
            if (!string.Equals(currentStatus, desiredStatus, StringComparison.Ordinal))
            {
                if (!running)
                    SetVirtualInjectionStatus(desiredStatus, VirtualInjectionStoppedBrush, "#F2F5F7", "#CBD3DA");
                else if (_scenario.WindowStatus == "rebuilding")
                    SetVirtualInjectionStatus(desiredStatus, WarningBrush, "#FBF2E3", "#E2C58F");
                else
                    SetVirtualInjectionStatus(desiredStatus, HealthyBrush, "#EAF5EC", "#B9D8BF");
            }
        }

        var streamText = _scenario.SmvDegraded
            ? "  ·  WARN"
            : !running
                ? "  ·  STOPPED"
                : _scenario.WindowStatus == "coherent"
                    ? "  ·  RUNNING"
                    : "  ·  STARTING";
        SetTextIfChanged(StreamHealthText, streamText);

        var streamBrush = _scenario.SmvDegraded
            ? WarningBrush
            : running
                ? HealthyBrush
                : VirtualInjectionStoppedBrush;
        if (!ReferenceEquals(StreamHealthText.Foreground, streamBrush))
            StreamHealthText.Foreground = streamBrush;

        var stateText = !running
            ? "STOPPED"
            : _scenario.WindowStatus == "coherent"
                ? "RUNNING"
                : "STARTING";
        SetTextIfChanged(WaveformSubtitleText, $"{_scenario.ActiveProfile.Name} · {stateText}");

        var tooltip =
            $"Configured profile {_scenario.ActiveProfile.Name}\n" +
            $"Configured fingerprint {_scenario.InjectionFingerprint}\n" +
            $"Output state {_scenario.OutputState}\n" +
            $"Effective output fingerprint {_scenario.OutputFingerprint}\n" +
            $"Injected frequency {_scenario.ActiveProfile.FrequencyHz:0.###} Hz\n" +
            $"IN {_scenario.NeutralCurrentProvenance}\n" +
            $"VN {_scenario.NeutralVoltageProvenance}";
        if (!Equals(WaveformSubtitleText.ToolTip, tooltip))
            WaveformSubtitleText.ToolTip = tooltip;

        // Internal injection is not an Ethernet SV publisher. Present only
        // stable engineering facts and avoid a constantly changing synthetic
        // smpCnt that adds visual noise without helping the operator.
        SetTextIfChanged(FrequencyText, $"{_scenario.ActiveProfile.FrequencyHz:0.000} Hz");
        SetTextIfChanged(SamplesPerCycleText, "  ·  80 samples/cycle");
        SetTextIfChanged(SampleCounterText, "  ·  4 kHz");
        SetTextIfChanged(SyncText, "  ·  VIRTUAL");
        SetTextIfChanged(FpsText, string.Empty);
        if (!ReferenceEquals(SyncText.Foreground, HealthyBrush))
            SyncText.Foreground = HealthyBrush;

        SetTextIfChanged(RelayFooterText, $"{_settings.GroupName} · REV {_settings.Revision}");
    }

    private static void SetTextIfChanged(TextBlock? textBlock, string text)
    {
        if (textBlock is not null && !string.Equals(textBlock.Text, text, StringComparison.Ordinal))
            textBlock.Text = text;
    }
}

internal static class VirtualInjectionRunStopBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.UnloadedEvent,
            new RoutedEventHandler(OnUnloaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        window.ContentRendered -= Window_ContentRendered;
        window.ContentRendered += Window_ContentRendered;
    }

    private static void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (sender is MainWindow window)
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(window.InitializeVirtualInjectionRunStop));
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StopVirtualInjectionRunStop();
    }
}
