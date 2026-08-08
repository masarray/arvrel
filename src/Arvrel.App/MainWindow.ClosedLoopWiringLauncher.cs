using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _closedLoopWiringLauncherInitialized;
    private Button? _closedLoopWiringButton;
    private VirtualWiringWindow? _virtualWiringWindow;

    internal void InitializeClosedLoopWiringLauncher()
    {
        if (_closedLoopWiringLauncherInitialized)
            return;
        if (!IsLoaded || _closedLoopBench is null || _productInjectionFooterActions is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeClosedLoopWiringLauncher));
            return;
        }

        _closedLoopWiringLauncherInitialized = true;
        _closedLoopWiringButton = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "Wiring…",
            Height = 28,
            MinHeight = 28,
            MaxHeight = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 6, 0),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            ToolTip = "Inspect or disconnect virtual injector/relay analog and binary wiring"
        };
        _closedLoopWiringButton.Click += ClosedLoopWiringButton_Click;
        _productInjectionFooterActions.Children.Insert(0, _closedLoopWiringButton);
        Closed += ClosedLoopWiringOwner_Closed;
    }

    private void ClosedLoopWiringButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closedLoopBench is null)
            return;

        if (_virtualWiringWindow is not null)
        {
            if (_virtualWiringWindow.WindowState == WindowState.Minimized)
                _virtualWiringWindow.WindowState = WindowState.Normal;
            _virtualWiringWindow.Activate();
            return;
        }

        _virtualWiringWindow = new VirtualWiringWindow(_closedLoopBench)
        {
            Owner = this
        };
        _virtualWiringWindow.Closed += VirtualWiringWindow_Closed;
        _virtualWiringWindow.Show();
    }

    private void VirtualWiringWindow_Closed(object? sender, EventArgs e)
    {
        if (_virtualWiringWindow is not null)
            _virtualWiringWindow.Closed -= VirtualWiringWindow_Closed;
        _virtualWiringWindow = null;
    }

    private void ClosedLoopWiringOwner_Closed(object? sender, EventArgs e)
    {
        if (_virtualWiringWindow is not null)
        {
            _virtualWiringWindow.Closed -= VirtualWiringWindow_Closed;
            _virtualWiringWindow.Close();
            _virtualWiringWindow = null;
        }
    }
}

internal static class ClosedLoopWiringLauncherBootstrap
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
            new Action(window.InitializeClosedLoopWiringLauncher));
    }
}
