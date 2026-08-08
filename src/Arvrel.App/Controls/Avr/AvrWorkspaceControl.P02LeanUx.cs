using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private Slider? _sourceCurrentSlider;
    private TextBlock? _currentTargetText;
    private TextBlock? _currentNominalScaleText;
    private TextBlock? _currentMaximumScaleText;
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
        ApplyLeanWorkspaceGeometry();

        HideSectionByLabel("QUICK SCENARIOS", hideFollowingSibling: true);
        HideSectionByLabel("TEST SEQUENCE", hideFollowingSibling: true);

        if (TestProfileCombo is not null)
        {
            TestProfileCombo.Visibility = Visibility.Collapsed;
            HideSectionByLabel("TEST PROFILE", hideFollowingSibling: false);
        }

        InstallCurrentInjectionSlider();
        WrapAdvancedTimingFields();

        var header = FindVisualChildren<TextBlock>(this)
            .FirstOrDefault(x => string.Equals(x.Text, "INJECTION FORM", StringComparison.Ordinal));
        if (header is not null)
            header.Text = "INJECTION";

        var subtitle = FindVisualChildren<TextBlock>(this)
            .FirstOrDefault(x => x.Text?.StartsWith("Test-set stimulus", StringComparison.Ordinal) == true);
        if (subtitle is not null)
            subtitle.Text = "Adjust U / I live · start source · observe AVR and OLTC response";

        var sourceTarget = FindVisualChildren<TextBlock>(this)
            .FirstOrDefault(x => string.Equals(x.Text, "CONFIGURED SOURCE TARGET", StringComparison.Ordinal));
        if (sourceTarget is not null)
            sourceTarget.Text = "SOURCE SETPOINTS";
    }

    private void ApplyLeanWorkspaceGeometry()
    {
        if (VisualTreeHelper.GetParent(ConfigurationPanel) is not Grid root || root.ColumnDefinitions.Count < 5)
            return;

        root.ColumnDefinitions[0].MinWidth = 330;
        root.ColumnDefinitions[0].Width = new GridLength(350);
        root.ColumnDefinitions[1].Width = new GridLength(8);
        root.ColumnDefinitions[3].Width = new GridLength(8);
        root.ColumnDefinitions[4].Width = new GridLength(38);
    }

    private void InstallCurrentInjectionSlider()
    {
        if (_sourceCurrentSlider is not null || SourceVoltageSlider.Parent is not StackPanel stack)
            return;

        var voltageIndex = stack.Children.IndexOf(SourceVoltageSlider);
        if (voltageIndex < 0)
            return;

        var insertAt = Math.Min(stack.Children.Count, voltageIndex + 2);

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

        var scale = new Grid { Margin = new Thickness(0, 2, 0, 8) };
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scale.Children.Add(CurrentScaleText("0 A", HorizontalAlignment.Left));
        _currentNominalScaleText = CurrentScaleText($"1.0 In · {_settings.NominalCurrentA:0.###} A", HorizontalAlignment.Center);
        Grid.SetColumn(_currentNominalScaleText, 1);
        scale.Children.Add(_currentNominalScaleText);
        _currentMaximumScaleText = CurrentScaleText($"2.0 In · {_sourceCurrentSlider.Maximum:0.###} A", HorizontalAlignment.Right);
        Grid.SetColumn(_currentMaximumScaleText, 2);
        scale.Children.Add(_currentMaximumScaleText);

        stack.Children.Insert(insertAt, header);
        stack.Children.Insert(insertAt + 1, _sourceCurrentSlider);
        stack.Children.Insert(insertAt + 2, scale);

        InjectionCurrentBox.LostFocus += (_, _) => SyncCurrentSliderFromModel();
    }

    private void WrapAdvancedTimingFields()
    {
        DependencyObject? current = RampBox;
        Grid? timingGrid = null;
        StackPanel? host = null;
        while (current is not null)
        {
            if (current is Grid grid && grid.Parent is StackPanel stack)
            {
                timingGrid = grid;
                host = stack;
                break;
            }
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        if (timingGrid is null || host is null)
            return;

        var index = host.Children.IndexOf(timingGrid);
        if (index < 0)
            return;

        host.Children.RemoveAt(index);
        var advanced = new Expander
        {
            Header = "Advanced ramp / timing",
            IsExpanded = false,
            Foreground = new SolidColorBrush(Color.FromRgb(143, 167, 184)),
            FontSize = 9.2,
            Margin = new Thickness(0, 3, 0, 1),
            Content = timingGrid
        };
        host.Children.Insert(index, advanced);
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
        if (_leanInjectionInstalled)
            SyncCurrentSliderFromModel();
    }

    private void SyncCurrentSliderFromModel()
    {
        if (_sourceCurrentSlider is null)
            return;

        var max = Math.Max(2.0, _settings.NominalCurrentA * 2.0);
        var target = Math.Clamp(_injectionTargetCurrentA, 0, max);
        _syncingCurrentSlider = true;
        try
        {
            if (Math.Abs(_sourceCurrentSlider.Maximum - max) > 1e-9)
                _sourceCurrentSlider.Maximum = max;
            var tick = Math.Max(0.02, _settings.NominalCurrentA / 20.0);
            if (Math.Abs(_sourceCurrentSlider.TickFrequency - tick) > 1e-9)
                _sourceCurrentSlider.TickFrequency = tick;
            if (Math.Abs(_sourceCurrentSlider.Value - target) > 1e-6)
                _sourceCurrentSlider.Value = target;

            var targetText = $"{_injectionTargetCurrentA:0.000} A";
            if (_currentTargetText is not null && !string.Equals(_currentTargetText.Text, targetText, StringComparison.Ordinal))
                _currentTargetText.Text = targetText;

            var nominalText = $"1.0 In · {_settings.NominalCurrentA:0.###} A";
            if (_currentNominalScaleText is not null && !string.Equals(_currentNominalScaleText.Text, nominalText, StringComparison.Ordinal))
                _currentNominalScaleText.Text = nominalText;

            var maximumText = $"2.0 In · {max:0.###} A";
            if (_currentMaximumScaleText is not null && !string.Equals(_currentMaximumScaleText.Text, maximumText, StringComparison.Ordinal))
                _currentMaximumScaleText.Text = maximumText;
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

        FrameworkElement section = text;
        DependencyObject? current = text;
        StackPanel? stack = null;
        while (current is not null)
        {
            if (current is FrameworkElement element && element.Parent is StackPanel parentStack)
            {
                section = element;
                stack = parentStack;
                break;
            }
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        section.Visibility = Visibility.Collapsed;
        if (!hideFollowingSibling || stack is null)
            return;

        var index = stack.Children.IndexOf(section);
        if (index >= 0 && index + 1 < stack.Children.Count)
            stack.Children[index + 1].Visibility = Visibility.Collapsed;
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
