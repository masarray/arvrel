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
            ToolTip = "Transformer differential IED · 87T / REF · deterministic self-test",
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
        // P15 deliberately allows the transformer workspace to open with Internal Demo
        // or with no discovered SV streams. That path is used only for the deterministic
        // packaged-core self-test. Applying the live/replay runtime still requires two
        // distinct HV/LV streams and remains guarded by TransformerIedWindow.BuildConfiguration.
        var window = new TransformerIedWindow(_processBus) { Owner = this };
        window.InitializeP14PractitionerUi();
        window.InitializeP15PublicTestUi();
        window.ShowDialog();
    }
}