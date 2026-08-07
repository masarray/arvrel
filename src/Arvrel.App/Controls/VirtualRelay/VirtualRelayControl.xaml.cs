using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.VirtualRelay;

/// <summary>
/// The P6 native virtual relay faceplate. It owns hardware geometry and visual
/// materials only; MainWindow remains the authority for protection, injection,
/// process-bus state, LCD navigation, annunciation and reset behavior.
/// </summary>
public partial class VirtualRelayControl : UserControl
{
    private static readonly HashSet<string> StatusLedLabels = new(StringComparer.Ordinal)
    {
        "HEALTHY",
        "PICKUP",
        "TRIP",
        "PHASE A",
        "PHASE B",
        "PHASE C",
        "EARTH",
        "SMV BLOCK"
    };

    public VirtualRelayControl()
    {
        InitializeComponent();
        EnableResponsiveHardwareScale();
        ImproveStatusLedReadability(this);
        MarkNativeHardwareButtons(this);
    }

    public event RoutedEventHandler? ResetRequested;

    private void ResetButton_Click(object sender, RoutedEventArgs e)
        => ResetRequested?.Invoke(this, e);

    private void EnableResponsiveHardwareScale()
    {
        // XAML historically used DownOnly, which was correct while the relay lived
        // in a narrow fixed sidebar but made the faceplate stay physically small on
        // maximized/high-resolution displays. P0.2 lets the same vector hardware
        // scale both down and up while preserving its native aspect ratio.
        if (Content is not Viewbox scaler)
            return;

        scaler.Stretch = Stretch.Uniform;
        scaler.StretchDirection = StretchDirection.Both;
        scaler.HorizontalAlignment = HorizontalAlignment.Stretch;
        scaler.VerticalAlignment = VerticalAlignment.Stretch;
    }

    private static void ImproveStatusLedReadability(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is TextBlock textBlock)
            {
                if (StatusLedLabels.Contains(textBlock.Text))
                {
                    // A restrained increase keeps the relay authentic while ensuring
                    // LED meanings remain readable before any Viewbox up-scaling.
                    textBlock.FontSize = Math.Max(textBlock.FontSize, 11.6);
                    textBlock.FontWeight = FontWeights.SemiBold;
                    TextOptions.SetTextFormattingMode(textBlock, TextFormattingMode.Display);
                    TextOptions.SetTextHintingMode(textBlock, TextHintingMode.Fixed);
                }
                else if (string.Equals(textBlock.Text, "STATUS", StringComparison.Ordinal))
                {
                    textBlock.FontSize = Math.Max(textBlock.FontSize, 10.8);
                    textBlock.FontWeight = FontWeights.SemiBold;
                }
            }

            if (child is DependencyObject dependencyObject)
                ImproveStatusLedReadability(dependencyObject);
        }
    }

    private static void MarkNativeHardwareButtons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is Button button)
                button.Tag = "ARVREL_TACTILE";

            if (child is DependencyObject dependencyObject)
                MarkNativeHardwareButtons(dependencyObject);
        }
    }
}
