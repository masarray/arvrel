using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private Slider? _sourceCurrentSlider;
    private TextBlock? _currentTargetText;
    private bool _leanInjectionInstalled;
    private bool _syncingCurrentSlider;

    private void ApplyP02LeanInjectionUx()
    {
        if (_leanInjectionInstalled)
            return;
        _leanInjectionInstalled = true;

        var inter = new FontFamily("Inter");
        FontFamily = inter;
        ApplyWorkspaceFontRecursive(this, inter);

        // Manual experimentation is the primary left-panel workflow. Scenario
        // shortcuts and the sequence trace duplicated the profile selector and
        // added visual noise, so keep the engine capability but hide it from the
        // default operator surface.
        HideSectionByLabel("QUICK SCENARIOS", hideFollowingSibling: true);
        HideSectionByLabel("TEST SEQUENCE", hideFollowingSibling: true);

        if (TestProfileCombo is not null)
        {
            TestProfileCombo.Visibility = Visibility.Collapsed;
            HideSectionByLabel("TEST PROFILE", hideFollowingSibling: false);
        }

        InstallCurrentInjectionSlider();

        var header = FindVisualChildren<TextBlock>(this)
            .FirstOrDefault(x => string.Equals(x.Text, "INJECTION FORM", StringComparison.Ordinal));
        if (header is not null)
            header.Text = "INJECTION";

        var subtitle = FindVisualChildren<TextBlock>(this)
            .FirstOrDefault(x => x.Text?.StartsWith("Test-set stimulus", StringComparison.Ordinal) == true);
        if (subtitle is not null)
            subtitle.Text = "Adjust U / I live · start source · observe AVR and OLTC response";
    }

    private void InstallCurrentInjectionSlider()
    {
        if (_sourceCurrentSlider is not null || SourceVoltageSlider.Parent is not StackPanel stack)
            return;

        var voltageIndex = stack.Children.IndexOf(SourceVoltageSlider);
        if (voltageIndex < 0)
            return;

        // Insert after the voltage slider scale row.
        var insertAt = Math.Min(stack.Children.Count, voltageIndex + 3);

        var header = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = "Current target",
            Foreground = new SolidColorBrush(Color.FromRgb(143, 167, 184)),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        _currentTargetText = new TextBlock
        {
            Text = $"{_injectionTargetCurrentA:0.000} A",
            Foreground = new SolidColorBrush(Color.FromRgb(233, 241, 246)),
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_currentTargetText, 1);
        header.Children.Add(_currentTargetText);

        _sourceCurrentSlider = new Slider
        {
            Minimum = 0,
            Maximum = Math.Max(2.0, _settings.NominalCurrentA * 2.0),
            Value = Math.Clamp(_injectionTargetCurrentA, 0, Math.Max(2.0, _settings.NominalCurrentA * 2.0)),
            TickFrequency = Math.Max(0.02, _settings.NominalCurrentA / 20.0),
            Margin = new Thickness(0, 5, 0, 0)
        };
        _sourceCurrentSlider.ValueChanged += SourceCurrentSlider_ValueChanged;

        var scale = new Grid { Margin = new Thickness(0, 2, 0, 10) };
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scale.Children.Add(CurrentScaleText("0 A", HorizontalAlignment.Left));
        var nominal = CurrentScaleText($"1.0 In · {_settings.NominalCurrentA:0.###} A", HorizontalAlignment.Center);
        Grid.SetColumn(nominal, 1);
        scale.Children.Add(nominal);
        var max = CurrentScaleText($"2.0 In · {_sourceCurrentSlider.Maximum:0.###} A", HorizontalAlignment.Right);
        Grid.SetColumn(max, 2);
        scale.Children.Add(max);

        stack.Children.Insert(insertAt, header);
        stack.Children.Insert(insertAt + 1, _sourceCurrentSlider);
        stack.Children.Insert(insertAt + 2, scale);

        InjectionCurrentBox.LostFocus += (_, _) => SyncCurrentSliderFromModel();
    }

    private TextBlock CurrentScaleText(string text, HorizontalAlignment alignment)
        => new()
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(143, 167, 184)),
            FontSize = 8.2,
            HorizontalAlignment = alignment
        };

    private void SourceCurrentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingCurrentSlider || _updatingInjectionUi)
            return;

        _injectionTargetCurrentA = e.NewValue;
        InjectionCurrentBox.Text = e.NewValue.ToString("0.000", CultureInfo.InvariantCulture);
        if (_currentTargetText is not null)
            _currentTargetText.Text = $"{e.NewValue:0.000} A";
        if (IsLoaded)
            RenderCurrent();
    }

    private void RefreshLeanInjectionUx()
    {
        if (!_leanInjectionInstalled)
            return;

        ApplyWorkspaceFontRecursive(this, new FontFamily("Inter"));
        SyncCurrentSliderFromModel();
    }

    private void SyncCurrentSliderFromModel()
    {
        if (_sourceCurrentSlider is null)
            return;

        var max = Math.Max(2.0, _settings.NominalCurrentA * 2.0);
        _syncingCurrentSlider = true;
        try
        {
            _sourceCurrentSlider.Maximum = max;
            _sourceCurrentSlider.TickFrequency = Math.Max(0.02, _settings.NominalCurrentA / 20.0);
            _sourceCurrentSlider.Value = Math.Clamp(_injectionTargetCurrentA, 0, max);
            if (_currentTargetText is not null)
                _currentTargetText.Text = $"{_injectionTargetCurrentA:0.000} A";
        }
        finally
        {
            _syncingCurrentSlider = false;
        }
    }

    private void HideSectionByLabel(string label, bool hideFollowingSibling)
    {
        var text = FindVisualChildren<TextBlock>(this)
            .FirstOrDefault(x => string.Equals(x.Text, label, StringComparison.Ordinal));
        if (text is null)
            return;

        if (hideFollowingSibling && text.Parent is Panel panel)
        {
            var index = panel.Children.IndexOf(text);
            text.Visibility = Visibility.Collapsed;
            if (index >= 0 && index + 1 < panel.Children.Count)
                panel.Children[index + 1].Visibility = Visibility.Collapsed;
            return;
        }

        text.Visibility = Visibility.Collapsed;
    }

    private static void ApplyWorkspaceFontRecursive(DependencyObject root, FontFamily font)
    {
        if (root is TextBlock text)
            text.FontFamily = font;
        else if (root is Control control)
            control.FontFamily = font;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            ApplyWorkspaceFontRecursive(VisualTreeHelper.GetChild(root, i), font);
    }
}
