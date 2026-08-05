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
            _virtualInjectionView is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeAdvancedInjectionFoundation));
            return;
        }

        var injectionToolbar = _virtualInjectionView.Children
            .OfType<Grid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 0);
        if (injectionToolbar is null)
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
            Content = "Advanced…",
            MinWidth = 86,
            Height = 28,
            MinHeight = 28,
            MaxHeight = 28,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 0, 10, 0),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Open the Advanced Injection Laboratory."
        };
        _advancedInjectionButton.Click += AdvancedInjectionButton_Click;
        Grid.SetColumn(_advancedInjectionButton, 5);
        injectionToolbar.Children.Add(_advancedInjectionButton);

        SourceCombo.SelectionChanged += AdvancedInjectionSourceChanged;
        Closing += AdvancedInjectionOwner_Closing;

        _advancedInjectionPresentationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
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
        window.StartInjectionRequested += AdvancedInjectionWindow_StartInjectionRequested;
        window.StopInjectionRequested += AdvancedInjectionWindow_StopInjectionRequested;
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
            StatusText.Text = "Advanced Injection Laboratory opened. Main Window remains in relay-monitoring mode.";
            RefreshAdvancedInjectionPresentation();
        }
        catch
        {
            window.StartInjectionRequested -= AdvancedInjectionWindow_StartInjectionRequested;
            window.StopInjectionRequested -= AdvancedInjectionWindow_StopInjectionRequested;
            window.DetachEditor();
            ReattachMainInjectionEditor();
            _advancedInjectionWindow = null;
            UpdateAdvancedInjectionWorkspaceAvailability();
            throw;
        }
    }

    private void AdvancedInjectionWindow_StartInjectionRequested(object? sender, EventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
            return;

        StartVirtualInjectionSource(announce: true);
        RefreshAdvancedInjectionPresentation();
    }

    private void AdvancedInjectionWindow_StopInjectionRequested(object? sender, EventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
            return;

        StopVirtualInjectionSource(announce: true);
        RefreshAdvancedInjectionPresentation();
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
            window.StartInjectionRequested -= AdvancedInjectionWindow_StartInjectionRequested;
            window.StopInjectionRequested -= AdvancedInjectionWindow_StopInjectionRequested;
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
            ? "Advanced Injection Window closed. Injection remains running."
            : "Advanced Injection Window closed.";
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
        {
            var desiredVisibility = simpleEditorAvailable ? Visibility.Visible : Visibility.Collapsed;
            if (injectionButton.Visibility != desiredVisibility)
                injectionButton.Visibility = desiredVisibility;
        }

        if (_advancedInjectionButton is not null)
        {
            // The launcher belongs to the simple INJECT workspace. Once the
            // modeless window owns the editor it disappears with that authority.
            var desiredVisibility = simpleEditorAvailable ? Visibility.Visible : Visibility.Collapsed;
            if (_advancedInjectionButton.Visibility != desiredVisibility)
                _advancedInjectionButton.Visibility = desiredVisibility;
        }

        if (!simpleEditorAvailable && _analysisWorkspaceMode == AnalysisWorkspaceMode.Injection)
            ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Dual, announce: false);
    }

    private void RefreshAdvancedInjectionPresentation()
    {
        UpdateAdvancedInjectionWorkspaceAvailability();
        if (_advancedInjectionWindow is null)
            return;

        _advancedInjectionWindow.UpdateRuntimeStatus(
            _scenario.ActiveProfile.Name,
            _scenario.OutputState,
            _scenario.InjectionFingerprint[..12]);
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
