using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private DispatcherTimer? _transformerInjectionIntegrationTimer;
    private bool _transformerInjectionShellActive;
    private bool _transformerInjectionRunHandlerInstalled;
    private string? _originalInjectFaultLabel;

    internal void InitializeTransformerVirtualInjectionIntegration()
    {
        if (_transformerInjectionIntegrationTimer is not null)
            return;

        InitializeTransformerVirtualInjection();
        if (_transformerVirtualInjectionRuntime is not null)
            _transformerVirtualInjectionRuntime.SnapshotChanged += TransformerInjectionLcdSnapshotChanged;
        _timer.Tick += TransformerInjectionFinalProjection_Tick;
        _transformerInjectionIntegrationTimer = new DispatcherTimer(DispatcherPriority.ContextIdle)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _transformerInjectionIntegrationTimer.Tick += TransformerInjectionIntegrationTimer_Tick;
        _transformerInjectionIntegrationTimer.Start();
        Closed += (_, _) => _transformerInjectionIntegrationTimer?.Stop();
        RefreshTransformerInjectionShellIntegration();
    }

    internal void StopTransformerVirtualInjectionIntegration()
    {
        _transformerInjectionIntegrationTimer?.Stop();
        _transformerInjectionIntegrationTimer = null;
        if (_transformerVirtualInjectionRuntime is not null)
            _transformerVirtualInjectionRuntime.SnapshotChanged -= TransformerInjectionLcdSnapshotChanged;
        if (_transformerInjectionShellActive)
            DeactivateTransformerInjectionShell();
    }

    private void TransformerInjectionIntegrationTimer_Tick(object? sender, EventArgs e)
        => RefreshTransformerInjectionShellIntegration();

    private void RefreshTransformerInjectionShellIntegration()
    {
        var desired = _transformerOcrWorkspaceMounted;
        if (desired && !_transformerInjectionShellActive)
            ActivateTransformerInjectionShell();
        else if (!desired && _transformerInjectionShellActive)
            DeactivateTransformerInjectionShell();

        if (!_transformerInjectionShellActive)
            return;

        // Other deferred bootstrap modules can finish after P18. Re-enforce the
        // handler set while Transformer is selected so one click can never start
        // both the feeder OCR scenario and the paired Transformer source.
        EnsureTransformerInjectionRunHandlers();

        var internalSource = SourceCombo.SelectedIndex == 0;
        if (!internalSource && _transformerVirtualInjectionRuntime?.IsRunning == true)
            StopTransformerVirtualInjection(announce: false);

        // Never publish/reset the synthetic runtime while Live Capture or PCAP Replay
        // owns Transformer evidence. Only hide its editor; the real paired-SV runtime
        // remains the sole authority in those source modes.
        if (internalSource)
            SetTransformerInjectionWorkspaceActive(true);
        else if (_transformerInjectionView is not null)
            _transformerInjectionView.Visibility = Visibility.Collapsed;
        RefreshTransformerInjectionDrawerVisibility();

        if (internalSource)
        {
            _phasorRefreshTimer?.Stop();
            _virtualInjectionRunStopTimer?.Stop();
            RefreshTransformerInjectionRunButton();
            if (_transformerFaceplatePresenter is not null)
            {
                _transformerFaceplatePresenter.UpdateEnvironment(
                    ProcessBusSourceMode.InternalDemo,
                    2,
                    _transformerWorkspaceWindow is { IsVisible: true });
            }
            RenderTransformerInjectionAnalysis();
            RenderTransformerRelaySingleLineIfHome();
        }
        else
        {
            _phasorRefreshTimer?.Start();
            _virtualInjectionRunStopTimer?.Start();
        }
    }

    private void ActivateTransformerInjectionShell()
    {
        _transformerInjectionShellActive = true;
        // A feeder injection may still be running when the operator changes IED type.
        // Stop it before arming the paired Transformer source; configured OCR values
        // remain retained for when the operator returns to OCR.
        if (_scenario.IsRunning)
            _scenario.StopInjection();
        _internalRunning = false;

        EnsureTransformerInjectionRunHandlers();
        if (SourceCombo.SelectedIndex == 0)
            SetTransformerInjectionWorkspaceActive(true);
        else if (_transformerInjectionView is not null)
            _transformerInjectionView.Visibility = Visibility.Collapsed;
        DegradeSmvButton.Visibility = Visibility.Collapsed;
        if (InjectFaultButton.Content is StackPanel panel)
        {
            var label = panel.Children.OfType<TextBlock>().FirstOrDefault();
            if (label is not null)
            {
                _originalInjectFaultLabel ??= label.Text;
                label.Text = "Inject 87T A fault";
            }
        }
        AddEvent("87T INJECT", "Two-sided HV/LV + independent NGR injector armed");
    }

    private void DeactivateTransformerInjectionShell()
    {
        if (_transformerVirtualInjectionRuntime?.IsRunning == true)
            StopTransformerVirtualInjection(announce: false);
        if (_transformerInjectionView is not null)
            _transformerInjectionView.Visibility = Visibility.Collapsed;
        RestoreVirtualInjectionRunHandlers();
        _phasorRefreshTimer?.Start();
        _virtualInjectionRunStopTimer?.Start();
        DegradeSmvButton.Visibility = Visibility.Visible;
        if (InjectFaultButton.Content is StackPanel panel && _originalInjectFaultLabel is not null)
        {
            var label = panel.Children.OfType<TextBlock>().FirstOrDefault();
            if (label is not null)
                label.Text = _originalInjectFaultLabel;
        }
        _transformerInjectionShellActive = false;
    }

    private void EnsureTransformerInjectionRunHandlers()
    {
        // Remove all known lifecycle handlers first. Routed-event removal is safe even
        // when a handler has not been registered yet, and prevents deferred bootstrap
        // order from creating duplicate source authorities.
        RunButton.Click -= VirtualInjectionRunButton_Click;
        RunButton.Click -= RunButton_Click;
        RunButton.Click -= TransformerVirtualInjectionRunButton_Click;
        RunButton.Click += TransformerVirtualInjectionRunButton_Click;

        InjectFaultButton.Click -= VirtualInjectionFaultPreset_Click;
        InjectFaultButton.Click -= InjectFault_Click;
        InjectFaultButton.Click -= TransformerVirtualInjectionFaultButton_Click;
        InjectFaultButton.Click += TransformerVirtualInjectionFaultButton_Click;
        _transformerInjectionRunHandlerInstalled = true;
    }

    private void RestoreVirtualInjectionRunHandlers()
    {
        if (!_transformerInjectionRunHandlerInstalled)
            return;
        RunButton.Click -= TransformerVirtualInjectionRunButton_Click;
        RunButton.Click -= VirtualInjectionRunButton_Click;
        RunButton.Click -= RunButton_Click;
        RunButton.Click += VirtualInjectionRunButton_Click;

        InjectFaultButton.Click -= TransformerVirtualInjectionFaultButton_Click;
        InjectFaultButton.Click -= VirtualInjectionFaultPreset_Click;
        InjectFaultButton.Click -= InjectFault_Click;
        InjectFaultButton.Click += VirtualInjectionFaultPreset_Click;
        _transformerInjectionRunHandlerInstalled = false;
    }

    private async void TransformerVirtualInjectionRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
        {
            RunButton_Click(sender, e);
            return;
        }

        RunButton.IsEnabled = false;
        try
        {
            if (_transformerVirtualInjectionRuntime?.IsRunning == true)
                StopTransformerVirtualInjection(announce: true);
            else
                StartTransformerVirtualInjection(announce: true);
        }
        finally
        {
            RunButton.IsEnabled = true;
            _internalRunning = _transformerVirtualInjectionRuntime?.IsRunning == true;
            RefreshTransformerInjectionRunButton();
        }
        await Task.CompletedTask;
    }

    private void TransformerVirtualInjectionFaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
            return;
        ApplyTransformerInjectionPreset("Internal A fault", announce: false);
        if (_transformerVirtualInjectionRuntime?.IsRunning != true)
            StartTransformerVirtualInjection(announce: false);
        StatusText.Text = "Transformer internal A-phase differential test applied to the synchronized two-sided current source.";
        RefreshTransformerInjectionRunButton();
    }

    private void RefreshTransformerInjectionRunButton()
    {
        if (!_transformerInjectionShellActive || SourceCombo.SelectedIndex != 0)
            return;
        var running = _transformerVirtualInjectionRuntime?.IsRunning == true;
        RunButtonText.Text = running ? "Stop injection" : "Start injection";
        RunButtonIcon.Kind = running ? LucideIconKind.CircleStop : LucideIconKind.Play;
        RunButtonIcon.Filled = running;
        StreamHealthText.Text = running ? "RUNNING" : "STOPPED";
        StreamHealthText.Foreground = running ? HealthyBrush : FindResource("MutedBrush") as System.Windows.Media.Brush;
    }

    private void TransformerInjectionFinalProjection_Tick(object? sender, EventArgs e)
    {
        if (!IsTransformerInternalInjectionActive)
            return;
        // MainWindow.Timer_Tick projects the legacy OCR scenario first. P18 is
        // intentionally registered afterwards and is therefore the final owner of
        // waveform/phasor/header presentation while Transformer Internal demo is active.
        RenderTransformerInjectionAnalysis();
        RenderTransformerRelaySingleLineIfHome();
    }

    private void TransformerInjectionLcdSnapshotChanged(object? sender, TransformerProtectionRuntimeSnapshotChangedEventArgs e)
    {
        if (!IsTransformerInternalInjectionActive)
            return;
        // Advance() raises synchronously, while the injection tick performs its final
        // snapshot publication before returning. Queue the SLD projection so it runs
        // after that publication and avoids HOME-page text flicker.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(RenderTransformerRelaySingleLineIfHome));
    }

    private void RenderTransformerRelaySingleLineIfHome()
    {
        if (_transformerFaceplate is null || _transformerLastSnapshot is null)
            return;

        var header = MultiIedVisualDescendants<TextBlock>(_transformerFaceplate)
            .FirstOrDefault(text => text.Text.Contains("87T", StringComparison.Ordinal) &&
                                    text.Text.Contains("HOME", StringComparison.Ordinal));
        if (header?.Parent is not Panel lcdPanel)
            return;

        var texts = lcdPanel.Children.OfType<TextBlock>().ToArray();
        if (texts.Length < 2)
            return;

        var body = texts[1];
        var measurement = _transformerLastSnapshot.Measurement;
        var phases = _transformerLastSnapshot.Protection?.Differential.Phases;
        if (measurement is null || phases is null)
        {
            header.Text = "TRANSFORMER SINGLE LINE";
            body.Text = "HV --CT--[ 87T ]--CT-- LV\n" +
                        "          |     |\n" +
                        "        NGR     NGR\n\n" +
                        "WAITING FOR PAIRED CURRENT";
            return;
        }

        var phaseA = phases.Single(item => item.Phase == TransformerPhase.A);
        var phaseB = phases.Single(item => item.Phase == TransformerPhase.B);
        var phaseC = phases.Single(item => item.Phase == TransformerPhase.C);
        var hv = measurement.HighVoltage;
        var lv = measurement.LowVoltage;
        header.Text = "TRANSFORMER SINGLE LINE · SECONDARY A";
        body.Text =
            "HV --CT--[ 87T ]--CT-- LV\n" +
            $"A  {hv.FundamentalCurrentA.PhaseA.Magnitude,5:0.000}       {lv.FundamentalCurrentA.PhaseA.Magnitude,5:0.000}\n" +
            $"B  {hv.FundamentalCurrentA.PhaseB.Magnitude,5:0.000}       {lv.FundamentalCurrentA.PhaseB.Magnitude,5:0.000}\n" +
            $"C  {hv.FundamentalCurrentA.PhaseC.Magnitude,5:0.000}       {lv.FundamentalCurrentA.PhaseC.Magnitude,5:0.000}\n" +
            $"N  {NeutralText(hv),5}       {NeutralText(lv),5}\n" +
            $"Id {phaseA.OperatingCurrentPu:0.00}  {phaseB.OperatingCurrentPu:0.00}  {phaseC.OperatingCurrentPu:0.00} pu";
    }

    private static string NeutralText(TransformerWindingMeasurement measurement)
        => measurement.NeutralCurrentAvailable
            ? measurement.NeutralCurrentA.Magnitude.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
            : "--";
}

internal static class TransformerVirtualInjectionIntegrationBootstrap
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
                new Action(window.InitializeTransformerVirtualInjectionIntegration));
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StopTransformerVirtualInjectionIntegration();
    }
}
