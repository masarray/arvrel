using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _advancedInjectionInitialized;
    private bool _advancedInjectionOwnerClosing;
    private bool _suppressAdvancedInjectionClosePrompt;
    private AdvancedInjectionWindow? _advancedInjectionWindow;
    private Button? _advancedInjectionButton;
    private DispatcherTimer? _advancedInjectionPresentationTimer;

    private bool IsAdvancedInjectionOpen => _advancedInjectionWindow is not null;

    internal void InitializeAdvancedInjectionFoundation()
    {
        if (_advancedInjectionInitialized)
            return;
        if (!_phasorWorkspaceInitialized ||
            !_virtualInjectionInitialized ||
            _analysisHost is null ||
            _virtualInjectionView is null ||
            _phasorQuantityCombo?.Parent is not Panel controlsLine)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeAdvancedInjectionFoundation));
            return;
        }

        _advancedInjectionInitialized = true;
        _advancedInjectionButton = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "ADVANCED",
            MinWidth = 78,
            Height = 32,
            MinHeight = 32,
            MaxHeight = 32,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(10, 0, 10, 0),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            ToolTip = "Open the modeless Advanced Injection Laboratory. The Main Window INJECT workspace is hidden while it owns the editor."
        };
        _advancedInjectionButton.Click += AdvancedInjectionButton_Click;
        controlsLine.Children.Add(_advancedInjectionButton);

        SourceCombo.SelectionChanged += AdvancedInjectionSourceChanged;
        Closing += AdvancedInjectionOwner_Closing;

        _advancedInjectionPresentationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _advancedInjectionPresentationTimer.Tick += (_, _) => RefreshAdvancedInjectionPresentation();
        _advancedInjectionPresentationTimer.Start();

        UpdateAdvancedInjectionWorkspaceAvailability();
        RefreshAdvancedInjectionPresentation();
    }

    internal void StopAdvancedInjectionFoundation()
    {
        _advancedInjectionPresentationTimer?.Stop();
        _advancedInjectionPresentationTimer = null;

        if (_advancedInjectionWindow is not null)
        {
            _suppressAdvancedInjectionClosePrompt = true;
            _advancedInjectionWindow.Close();
            _suppressAdvancedInjectionClosePrompt = false;
        }
    }

    private void AdvancedInjectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
            return;

        if (IsAdvancedInjectionOpen)
        {
            _advancedInjectionWindow!.FocusDirectEditor();
            return;
        }

        OpenAdvancedInjectionWindow();
    }

    private void OpenAdvancedInjectionWindow()
    {
        if (_analysisHost is null || _virtualInjectionView is null)
            return;
        if (IsAdvancedInjectionOpen)
        {
            _advancedInjectionWindow!.FocusDirectEditor();
            return;
        }

        ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Dual, announce: false);
        _virtualInjectionView.Visibility = Visibility.Collapsed;
        if (_analysisHost.Children.Contains(_virtualInjectionView))
            _analysisHost.Children.Remove(_virtualInjectionView);

        var window = new AdvancedInjectionWindow
        {
            Owner = this
        };
        window.Closing += AdvancedInjectionWindow_Closing;
        window.Closed += AdvancedInjectionWindow_Closed;

        try
        {
            window.AttachEditor(_virtualInjectionView);
            _advancedInjectionWindow = window;
            UpdateAdvancedInjectionWorkspaceAvailability();
            window.Show();
            window.FocusDirectEditor();
            AddEvent("ADV INJECT", "Advanced Injection Window opened; Main INJECT workspace hidden");
            StatusText.Text = "Advanced Injection Laboratory opened. Main Window remains a waveform, phasor, and relay monitoring surface.";
            RefreshAdvancedInjectionPresentation();
        }
        catch
        {
            window.DetachEditor();
            ReattachMainInjectionEditor();
            _advancedInjectionWindow = null;
            UpdateAdvancedInjectionWorkspaceAvailability();
            throw;
        }
    }

    private void AdvancedInjectionWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_suppressAdvancedInjectionClosePrompt ||
            _advancedInjectionOwnerClosing ||
            !_scenario.IsRunning)
            return;

        var result = MessageBox.Show(
            this,
            "Virtual injection is still running.\n\n" +
            "Yes  — stop output and close\n" +
            "No   — keep output running and close\n" +
            "Cancel — keep the Advanced Injection Window open",
            "ARVREL advanced injection",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);

        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == MessageBoxResult.Yes)
            StopVirtualInjectionSource(announce: false);
    }

    private void AdvancedInjectionWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is AdvancedInjectionWindow window)
        {
            window.Closing -= AdvancedInjectionWindow_Closing;
            window.Closed -= AdvancedInjectionWindow_Closed;
            var editor = window.DetachEditor();
            if (editor is not null && !ReferenceEquals(editor, _virtualInjectionView))
                throw new InvalidOperationException("The Advanced Injection Window returned an unexpected editor instance.");
        }

        _advancedInjectionWindow = null;
        if (_advancedInjectionOwnerClosing)
            return;

        ReattachMainInjectionEditor();
        UpdateAdvancedInjectionWorkspaceAvailability();
        ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Dual, announce: false);
        AddEvent("ADV CLOSE", "Main INJECT workspace restored; DUAL monitoring retained");
        StatusText.Text = _scenario.IsRunning
            ? "Advanced Injection Window closed. Injection remains running and the Main Window simple editor is available again."
            : "Advanced Injection Window closed. Main Window injection workspace is available again.";
    }

    private void ReattachMainInjectionEditor()
    {
        if (_analysisHost is null || _virtualInjectionView is null)
            return;
        if (!_analysisHost.Children.Contains(_virtualInjectionView))
        {
            Grid.SetColumn(_virtualInjectionView, 0);
            _analysisHost.Children.Add(_virtualInjectionView);
        }
        _virtualInjectionView.Visibility = Visibility.Collapsed;
    }

    private void AdvancedInjectionSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            if (SourceCombo.SelectedIndex != 0 && _advancedInjectionWindow is not null)
            {
                _suppressAdvancedInjectionClosePrompt = true;
                _advancedInjectionWindow.Close();
                _suppressAdvancedInjectionClosePrompt = false;
            }

            UpdateAdvancedInjectionWorkspaceAvailability();
            if (SourceCombo.SelectedIndex == 0)
                ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Dual, announce: false);
        }));
    }

    private void AdvancedInjectionOwner_Closing(object? sender, CancelEventArgs e)
    {
        _advancedInjectionOwnerClosing = true;
        if (_advancedInjectionWindow is null)
            return;

        _suppressAdvancedInjectionClosePrompt = true;
        _advancedInjectionWindow.Close();
        _suppressAdvancedInjectionClosePrompt = false;
    }

    private void UpdateAdvancedInjectionWorkspaceAvailability()
    {
        var internalMode = SourceCombo.SelectedIndex == 0;
        var simpleEditorAvailable = internalMode && !IsAdvancedInjectionOpen;

        if (_analysisModeButtons.TryGetValue(AnalysisWorkspaceMode.Injection, out var injectionButton))
            injectionButton.Visibility = simpleEditorAvailable ? Visibility.Visible : Visibility.Collapsed;

        if (_advancedInjectionButton is not null)
        {
            _advancedInjectionButton.Visibility = internalMode ? Visibility.Visible : Visibility.Collapsed;
            _advancedInjectionButton.Content = IsAdvancedInjectionOpen ? "FOCUS INJECTION" : "ADVANCED";
            _advancedInjectionButton.ToolTip = IsAdvancedInjectionOpen
                ? "Bring the active Advanced Injection Laboratory to the foreground."
                : "Open the modeless Advanced Injection Laboratory. The Main Window INJECT workspace is hidden while it owns the editor.";
        }

        if (!simpleEditorAvailable && _analysisWorkspaceMode == AnalysisWorkspaceMode.Injection)
            ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Dual, announce: false);
    }

    private void RefreshAdvancedInjectionPresentation()
    {
        UpdateAdvancedInjectionWorkspaceAvailability();
        if (_advancedInjectionWindow is null)
            return;

        var shortFingerprint = _scenario.InjectionFingerprint[..12];
        _advancedInjectionWindow.UpdateRuntimeStatus(
            _scenario.ActiveProfile.Name,
            _scenario.OutputState,
            shortFingerprint);
    }
}

internal static class AdvancedInjectionFoundationBootstrap
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
                new Action(window.InitializeAdvancedInjectionFoundation));
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StopAdvancedInjectionFoundation();
    }
}
