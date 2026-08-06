using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Arvrel.Desktop.ViewModels;

namespace Arvrel.Desktop.Controls;

public sealed partial class ProcessBusWorkspace : UserControl
{
    private static readonly FilePickerFileType CaptureFiles = new("PCAP capture")
    {
        Patterns = new[] { "*.pcap", "*.pcapng" }
    };

    private static readonly FilePickerFileType SclFiles = new("IEC 61850 SCL")
    {
        Patterns = new[] { "*.scd", "*.cid", "*.icd", "*.iid", "*.ssd", "*.xml" }
    };

    public ProcessBusWorkspace()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
            (DataContext as MainWindowViewModel)?.InitializeProcessBusWorkspace();
    }

    private async void BrowseReplay_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFileAsync("Select PCAP or PCAPNG capture", CaptureFiles);
        if (path is not null && DataContext is MainWindowViewModel viewModel)
            viewModel.ReplayPath = path;
    }

    private async void BrowseScl_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickFileAsync("Select IEC 61850 SCL file", SclFiles);
        if (path is not null && DataContext is MainWindowViewModel viewModel)
            viewModel.SclPath = path;
    }

    private async Task<string?> PickFileAsync(string title, FilePickerFileType fileType)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider)
            return null;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { fileType, FilePickerFileTypes.All }
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
