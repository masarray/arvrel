using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arvrel.Desktop.Controls;
using Arvrel.Desktop.ViewModels;

namespace Arvrel.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private bool _virtualRelayInstalled;

    public MainWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _timer.Tick += (_, _) => (DataContext as MainWindowViewModel)?.Tick();

        Opened += MainWindow_Opened;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        InstallVirtualRelayFaceplate();
        _timer.Start();
    }

    private void InstallVirtualRelayFaceplate()
    {
        if (_virtualRelayInstalled)
            return;

        var relayTabs = this.GetVisualDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(candidate => candidate.Items
                .OfType<TabItem>()
                .Any(tab => string.Equals(tab.Header?.ToString(), "RELAY", StringComparison.Ordinal)));

        if (relayTabs is null)
            return;

        var faceplate = new VirtualRelayControl
        {
            DataContext = DataContext
        };

        relayTabs.Items.Insert(0, new TabItem
        {
            Header = "FACEPLATE",
            Content = faceplate
        });
        relayTabs.SelectedIndex = 0;
        _virtualRelayInstalled = true;
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.DisposeAsync();
    }
}
