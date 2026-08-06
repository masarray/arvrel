using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Arvrel.Desktop.Controls;
using Arvrel.Desktop.ViewModels;

namespace Arvrel.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private VirtualRelayFaceplate? _virtualRelayFaceplate;

    public MainWindow()
    {
        InitializeComponent();
        InstallVirtualRelayFaceplate();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _timer.Tick += (_, _) => (DataContext as MainWindowViewModel)?.Tick();

        DataContextChanged += (_, _) =>
        {
            if (_virtualRelayFaceplate is not null)
                _virtualRelayFaceplate.DataContext = DataContext;
        };

        Opened += (_, _) => _timer.Start();
        Closed += MainWindow_Closed;
    }

    private void InstallVirtualRelayFaceplate()
    {
        var relayTabs = this.GetLogicalDescendants()
            .OfType<TabControl>()
            .LastOrDefault();
        if (relayTabs is null)
            throw new InvalidOperationException("Avalonia relay workspace TabControl was not found.");

        _virtualRelayFaceplate = new VirtualRelayFaceplate
        {
            DataContext = DataContext,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        relayTabs.Items.Insert(0, new TabItem
        {
            Header = "FACEPLATE",
            Content = _virtualRelayFaceplate
        });
        relayTabs.SelectedIndex = 0;
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.DisposeAsync();
    }
}
