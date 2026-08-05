using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Services;
using Microsoft.Win32;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _virtualInjectionIntegrationInitialized;
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

        SyncVirtualInjectionEditorFromProfile(_scenario.ActiveProfile);
        UpdateAnalysisSourceMode(announce: false);
        RefreshVirtualInjectionPresentation();
    }

    internal void StopVirtualInjectionIntegration()
    {
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
            SyncVirtualInjectionEditorFromProfile(_scenario.ActiveProfile);
            RefreshPhasorFrame();
            RefreshVirtualInjectionPresentation();
        }));
    }

    private void RefreshVirtualInjectionPresentation()
    {
        if (!_virtualInjectionInitialized || SourceCombo.SelectedIndex != 0)
            return;

        // Presentation ownership belongs exclusively to the run/stop presenter.
        // The previous 80 ms integration timer rewrote the same header, subtitle,
        // source strip, and relay footer as the 250 ms run/stop presenter. Their
        // different strings caused the remaining STOPPED/smpCnt/fingerprint flicker.
        RefreshVirtualInjectionRunStopPresentation();
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
            schemaVersion = 4,
            exportedAt = DateTimeOffset.Now,
            application = "ARVREL",
            operatingMode = OperatingModeCombo.SelectedIndex == 1 ? "Research" : "Practitioner",
            sourceMode = "Internal virtual injection",
            injection = new
            {
                configuredProfile = _scenario.ActiveProfile,
                effectiveOutputProfile = _scenario.OutputProfile,
                configuredFingerprint = _scenario.InjectionFingerprint,
                effectiveOutputFingerprint = _scenario.OutputFingerprint,
                configuredAt = _scenario.AppliedAt,
                outputStateChangedAt = _scenario.OutputStateChangedAt,
                isRunning = _scenario.IsRunning,
                outputState = _scenario.OutputState,
                windowStatus = _scenario.WindowStatus,
                injectedFrequencyHz = _scenario.ActiveProfile.FrequencyHz,
                nominalFrequencyHz = DeterministicLabScenario.Frequency,
                sampleRateHz = DeterministicLabScenario.SampleRateHz,
                samplesPerNominalCycle = DeterministicLabScenario.SamplesPerCycle,
                cycles = 2,
                neutralCurrentProvenance = _scenario.NeutralCurrentProvenance,
                neutralVoltageProvenance = _scenario.NeutralVoltageProvenance,
                trustDegraded = _scenario.SmvDegraded,
                stopContract = "STOP forces all effective virtual voltage and current outputs to zero while retaining the configured profile."
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
