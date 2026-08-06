using System.Windows;
using System.Windows.Controls;

namespace Arvrel.App.Controls.VirtualRelay;

/// <summary>
/// The P6 native virtual relay faceplate. It owns hardware geometry and visual
/// materials only; MainWindow remains the authority for protection, injection,
/// process-bus state, LCD navigation, annunciation and reset behavior.
/// </summary>
public partial class VirtualRelayControl : UserControl
{
    public VirtualRelayControl()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? ResetRequested;

    private void ResetButton_Click(object sender, RoutedEventArgs e)
        => ResetRequested?.Invoke(this, e);
}
