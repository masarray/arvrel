using System.Windows;
using System.Windows.Controls;
using Arvrel.App.Controls;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _transformerIedEntryInjected;

    public void InitializeTransformerIedEntryPoint()
        => InjectTransformerIedEntryPoint();

    private void InjectTransformerIedEntryPoint()
    {
        if (_transformerIedEntryInjected || RunButton.Parent is not StackPanel toolbar)
            return;

        var button = new Button
        {
            Style = (Style)FindResource("IconOnlyButton"),
            ToolTip = "Transformer differential IED · 87T / REF",
            Margin = new Thickness(0, 0, 5, 0),
            Content = new LucideIcon
            {
                Kind = LucideIconKind.Activity,
                Width = 17,
                Height = 17,
                Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush")
            }
        };
        button.Click += TransformerIedButton_Click;

        var runIndex = toolbar.Children.IndexOf(RunButton);
        toolbar.Children.Insert(Math.Max(0, runIndex), button);
        _transformerIedEntryInjected = true;
    }

    private void TransformerIedButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex == 0)
        {
            MessageBox.Show(
                this,
                "Transformer differential requires two independent HV/LV Sampled Values streams. Select Live Npcap or PCAP replay first.",
                "Transformer differential IED",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (SourceCombo.SelectedIndex == 1 && !_sourceRunning)
        {
            MessageBox.Show(
                this,
                "Start Live Npcap capture first. The transformer workspace will then follow the two selected HV/LV streams event-by-event.",
                "Transformer differential IED",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (SourceCombo.SelectedIndex == 2 && _processBus.GetStreams().Count < 2)
        {
            MessageBox.Show(
                this,
                "Replay a PCAP/PCAPNG containing at least two transformer-side Sampled Values streams before opening the 87T workspace.",
                "Transformer differential IED",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var window = new TransformerIedWindow(_processBus) { Owner = this };
        window.InitializeP14PractitionerUi();
        window.ShowDialog();
    }
}
