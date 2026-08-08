using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _realTestWorkflowLauncherInitialized;
    private Button? _realTestWorkflowButton;
    private RealTestWorkflowsWindow? _realTestWorkflowWindow;

    internal void InitializeRealTestWorkflowLauncher()
    {
        if (_realTestWorkflowLauncherInitialized)
            return;
        if (!IsLoaded || _closedLoopBench is null || _productInjectionFooterActions is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeRealTestWorkflowLauncher));
            return;
        }

        _realTestWorkflowLauncherInitialized = true;
        _realTestWorkflowButton = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "Tests…",
            Height = 28,
            MinHeight = 28,
            MaxHeight = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 6, 0),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            ToolTip = "Run ramp, pulse ramp, pickup/dropout search and state-sequence workflows through the closed-loop virtual test bench"
        };
        _realTestWorkflowButton.Click += RealTestWorkflowButton_Click;
        _productInjectionFooterActions.Children.Insert(0, _realTestWorkflowButton);
        Closed += RealTestWorkflowOwner_Closed;
    }

    private void RealTestWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closedLoopBench is null)
            return;

        _internalRunning = false;
        if (_scenario.IsRunning)
            _scenario.StopInjection();
        UpdateRunButton();
        RefreshVirtualInjectionRunStopPresentation();

        if (_realTestWorkflowWindow is not null)
        {
            if (_realTestWorkflowWindow.WindowState == WindowState.Minimized)
                _realTestWorkflowWindow.WindowState = WindowState.Normal;
            _realTestWorkflowWindow.Activate();
            return;
        }

        _realTestWorkflowWindow = new RealTestWorkflowsWindow(_scenario.CoreScenario, _closedLoopBench)
        {
            Owner = this
        };
        _realTestWorkflowWindow.Closed += RealTestWorkflowWindow_Closed;
        _realTestWorkflowWindow.Show();
        AddEvent("TEST MODE", "P1 real test workflow console opened");
        StatusText.Text = "P1 real test workflows ready · ramp, pulse ramp, pickup/dropout search and state sequencing use TESTSET BI feedback authority.";
    }

    private void RealTestWorkflowWindow_Closed(object? sender, EventArgs e)
    {
        if (_realTestWorkflowWindow is not null)
            _realTestWorkflowWindow.Closed -= RealTestWorkflowWindow_Closed;
        _realTestWorkflowWindow = null;
        _internalRunning = false;
        UpdateRunButton();
        RefreshVirtualInjectionRunStopPresentation();
    }

    private void RealTestWorkflowOwner_Closed(object? sender, EventArgs e)
    {
        if (_realTestWorkflowWindow is null)
            return;
        _realTestWorkflowWindow.Closed -= RealTestWorkflowWindow_Closed;
        _realTestWorkflowWindow.Close();
        _realTestWorkflowWindow = null;
    }
}

internal static class RealTestWorkflowLauncherBootstrap
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
        if (sender is not MainWindow window)
            return;

        window.Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(window.InitializeRealTestWorkflowLauncher));
    }
}
