using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _virtualInjectionIntegrationInitialized;
    private DispatcherTimer? _virtualInjectionPresentationTimer;
    private Button? _virtualInjectionExportButton;

    internal void InitializeVirtualInjectionIntegration()
    {
        if (_virtualInjectionIntegrationInitialized)
            return;
        if (!_virtualInjectionInitialized)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(InitializeVirtualInjectionIntegration));
            return;
        }

        _virtualInjectionIntegrationInitialized = true;
        SourceCombo.SelectionChanged += VirtualInjectionSourceCombo_SelectionChanged;
        AddHandler(Button.ClickEvent, new RoutedEventHandler(VirtualInjectionWindowButton_Click), handledEventsToo: true);
        InstallVirtualInjectionExportHandler();

        _virtualInjectionPresentationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _virtualInjectionPresentationTimer.Tick += (_, _) => RefreshVirtualInjectionPresentation();
        _virtualInjectionPresentationTimer.Start();
        Closed += (_, _) => _virtualInjectionPresentationTimer?.Stop();

        SyncVirtualInjectionEditorFromProfile(_scenario.ActiveProfile);
        UpdateAnalysisSourceMode(announce: false);
        RefreshVirtualInjectionPresentation();
    }

    internal void StopVirtualInjectionIntegration()
    {
        _virtualInjectionPresentationTimer?.Stop();
        _virtualInjectionPresentationTimer = null;
        if (_virtualInjectionExportButton is not null)
        {
            _virtualInjectionExportButton.Click -= ExportVirtualInjectionEvidence_Click;
            _virtualInjectionExportButton.Click += ExportEvidence_Click;
            _virtualInjectionExportButton = null;
        }
    }

    private void VirtualInjectionSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            UpdateAnalysisSourceMode(announce: false);
            if (SourceCombo.SelectedIndex == 0)
                SyncVirtualInjectionEditorFromProfile(_scenario.ActiveProfile);
            RefreshVirtualInjectionPresentation();
        }));
    }

    private void VirtualInjectionWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            // Existing XAML handlers run first. This projection then keeps the editor,
            // phasor, labels, presets, and reset state synchronized with the source.
            SyncVirtualInjectionEditorFromProfile(_scenario.ActiveProfile);
            RefreshPhasorFrame();
            RefreshVirtualInjectionPresentation();

            if (e.OriginalSource is Button button && ReferenceEquals(button, InjectFaultButton))
            {
                StatusText.Text = _scenario.FaultActive
                    ? "A-G virtual injection applied. Edit any channel to continue from the visible preset values."
                    : "A-G injection cleared. The relay trip latch remains until Reset relay or Reset all.";
            }
        }));
    }

    private void RefreshVirtualInjectionPresentation()
    {
        if (!_virtualInjectionInitialized || SourceCombo.SelectedIndex != 0)
            return;

        var profile = _scenario.ActiveProfile;
        FrequencyText.Text = $"{profile.FrequencyHz:0.000} Hz";
        SamplesPerCycleText.Text = $"  ·  {Arvrel.App.Services.DeterministicLabScenario.SamplesPerCycle} samples/cycle";
        SampleCounterText.Text = $"  ·  smpCnt {_scenario.SampleCounter:0000}";
        SyncText.Text = "  ·  virtual sync";
        SyncText.Foreground = _scenario.SmvDegraded ? WarningBrush : HealthyBrush;
        FpsText.Text = "  ·  internal injection";
        StreamHealthText.Text = _scenario.SmvDegraded
            ? "  ·  INTERNAL · WARN"
            : _scenario.WindowStatus == "coherent"
                ? "  ·  INTERNAL · GOOD"
                : "  ·  INTERNAL · REBUILD";
        StreamHealthText.Foreground = _scenario.SmvDegraded
            ? WarningBrush
            : _scenario.WindowStatus == "coherent"
                ? HealthyBrush
                : WarningBrush;

        var shortFingerprint = _scenario.InjectionFingerprint[..12];
        WaveformSubtitleText.Text = _analysisWorkspaceMode == AnalysisWorkspaceMode.Injection
            ? $"{profile.Name} · {_scenario.WindowStatus} · {shortFingerprint} · valid changes auto-apply atomically"
            : $"Virtual injection waveform · {profile.Name} · {_scenario.NeutralCurrentProvenance} · {_scenario.NeutralVoltageProvenance}";
        WaveformSubtitleText.ToolTip = $"Profile {profile.Name}\nFingerprint {_scenario.InjectionFingerprint}\nIN {_scenario.NeutralCurrentProvenance}\nVN {_scenario.NeutralVoltageProvenance}";
        RelayFooterText.Text = $"{_settings.GroupName} rev {_settings.Revision} · injection {shortFingerprint} · {_scenario.WindowStatus}";

        if (_scenario.WindowStatus == "coherent" &&
            _virtualInjectionStatusText?.Text.StartsWith("APPLIED", StringComparison.Ordinal) == true)
        {
            SetVirtualInjectionStatus("READY", HealthyBrush, "#EAF5EC", "#B9D8BF");
        }
    }

    private void InstallVirtualInjectionExportHandler()
    {
        _virtualInjectionExportButton = FindButtonByToolTip(this, "Export evidence JSON");
        if (_virtualInjectionExportButton is null)
            return;

        _virtualInjectionExportButton.Click -= ExportEvidence_Click;
        _virtualInjectionExportButton.Click += ExportVirtualInjectionEvidence_Click;
    }

    private void ExportVirtualInjectionEvidence_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
        {
            ExportEvidence_Click(sender, e);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export ARVREL virtual-injection evidence",
            Filter = "ARVREL evidence JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"ARVREL-injection-evidence-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var step = _scenario.Advance(TimeSpan.Zero, _pickupPosition, _tripPosition);
        var evidence = new
        {
            schemaVersion = 3,
            exportedAt = DateTimeOffset.Now,
            application = "ARVREL",
            operatingMode = OperatingModeCombo.SelectedIndex == 1 ? "Research" : "Practitioner",
            sourceMode = "Internal virtual injection",
            injection = new
            {
                profile = _scenario.ActiveProfile,
                fingerprint = _scenario.InjectionFingerprint,
                appliedAt = _scenario.AppliedAt,
                windowStatus = _scenario.WindowStatus,
                samplesPerCycle = Arvrel.App.Services.DeterministicLabScenario.SamplesPerCycle,
                cycles = 2,
                neutralCurrentProvenance = _scenario.NeutralCurrentProvenance,
                neutralVoltageProvenance = _scenario.NeutralVoltageProvenance,
                trustDegraded = _scenario.SmvDegraded
            },
            measurement = step.Measurement,
            protection = _snapshot,
            protectionSettings = _settings,
            settingsFingerprint = _settings.Fingerprint(),
            algorithmMode = "standard-active/custom-shadow-only",
            events = _events.Reverse().ToArray()
        };

        File.WriteAllText(
            dialog.FileName,
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        AddEvent("EXPORT", System.IO.Path.GetFileName(dialog.FileName));
        StatusText.Text = $"Virtual-injection evidence exported to {dialog.FileName}.";
    }

    private static Button? FindButtonByToolTip(DependencyObject root, string toolTip)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button button && string.Equals(button.ToolTip as string, toolTip, StringComparison.Ordinal))
                return button;
            var nested = FindButtonByToolTip(child, toolTip);
            if (nested is not null)
                return nested;
        }
        return null;
    }
}

internal static class VirtualInjectionBootstrap
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
                DispatcherPriority.ContextIdle,
                new Action(window.InitializeVirtualInjectionIntegration));
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StopVirtualInjectionIntegration();
    }
}
