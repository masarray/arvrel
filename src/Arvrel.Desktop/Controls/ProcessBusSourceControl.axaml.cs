using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Arvrel.Desktop.ViewModels;

namespace Arvrel.Desktop.Controls;

public sealed partial class ProcessBusSourceControl : UserControl
{
    private static readonly FilePickerFileType CaptureFiles = new("Packet captures")
    {
        Patterns = new[] { "*.pcap", "*.pcapng" },
        MimeTypes = new[]
        {
            "application/vnd.tcpdump.pcap",
            "application/x-pcapng"
        }
    };

    public ProcessBusSourceControl()
    {
        InitializeComponent();
    }

    private async void OpenReplay_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.IsReplayBusy)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open IEC 61850 Sampled Values capture",
            AllowMultiple = false,
            FileTypeFilter = new[] { CaptureFiles }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            await viewModel.ReplayCaptureAsync(path);
    }

    private async void SelectInternal_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.SelectInternalSourceAsync();
    }
}
