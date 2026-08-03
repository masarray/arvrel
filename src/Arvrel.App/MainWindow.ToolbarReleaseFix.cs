using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _releaseToolbarApplied;

    internal void ScheduleReleaseToolbarFix()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(ApplyReleaseToolbarFix));
    }

    private void ApplyReleaseToolbarFix()
    {
        if (_releaseToolbarApplied || SourceCombo.Parent is not Grid legacyToolbar)
            return;

        if (RunButton.Parent is not StackPanel actionPanel)
            return;

        _releaseToolbarApplied = true;

        if (legacyToolbar.Parent is Border toolbarBorder &&
            toolbarBorder.Parent is Grid applicationGrid &&
            applicationGrid.RowDefinitions.Count > 1)
        {
            applicationGrid.RowDefinitions[1].Height = new GridLength(66);
        }

        legacyToolbar.Children.Clear();
        legacyToolbar.ColumnDefinitions.Clear();
        legacyToolbar.Margin = new Thickness(13, 5, 13, 5);
        legacyToolbar.VerticalAlignment = VerticalAlignment.Stretch;
        legacyToolbar.UseLayoutRounding = true;
        legacyToolbar.SnapsToDevicePixels = true;

        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star), MinWidth = 132 });
        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(9) });
        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.65, GridUnitType.Star), MinWidth = 190 });
        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(9) });
        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.50, GridUnitType.Star), MinWidth = 178 });
        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(9) });
        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.92, GridUnitType.Star), MinWidth = 112 });
        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        legacyToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AddReleaseToolbarField(legacyToolbar, 0, "SOURCE", SourceCombo);
        AddReleaseToolbarField(legacyToolbar, 2, "ADAPTER", AdapterCombo);
        AddReleaseToolbarField(legacyToolbar, 4, "SV STREAM", StreamCombo);
        AddReleaseToolbarField(legacyToolbar, 6, "VIEW", ViewCombo);

        var actionHost = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
        actionHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
        actionHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

        Grid.SetRow(actionPanel, 1);
        actionPanel.HorizontalAlignment = HorizontalAlignment.Right;
        actionPanel.VerticalAlignment = VerticalAlignment.Center;
        actionPanel.Margin = new Thickness(0);
        actionHost.Children.Add(actionPanel);

        Grid.SetColumn(actionHost, 8);
        legacyToolbar.Children.Add(actionHost);

        foreach (var button in actionPanel.Children.OfType<Button>())
        {
            button.Height = 36;
            button.MinHeight = 36;
            button.MaxHeight = 36;
            button.VerticalAlignment = VerticalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.Padding = button == RunButton
                ? new Thickness(13, 0, 13, 0)
                : new Thickness(0);

            AlignReleaseToolbarButtonContent(button.Content);
        }

        RunButton.MinWidth = 120;
        RunButtonText.VerticalAlignment = VerticalAlignment.Center;
        RunButtonText.TextAlignment = TextAlignment.Center;
        RunButtonText.TextTrimming = TextTrimming.None;
        RunButtonText.TextWrapping = TextWrapping.NoWrap;
        RunButtonIcon.VerticalAlignment = VerticalAlignment.Center;

        RefreshReleaseToolbarTooltips();
    }

    private void AddReleaseToolbarField(Grid toolbar, int column, string caption, ComboBox combo)
    {
        var field = new Grid
        {
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
        field.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
        field.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

        var label = new TextBlock
        {
            Text = caption,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(88, 107, 120)),
            VerticalAlignment = VerticalAlignment.Top,
            LineHeight = 12,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(1, 0, 0, 1)
        };

        combo.Width = double.NaN;
        combo.MinWidth = 0;
        combo.MaxWidth = double.PositiveInfinity;
        combo.Height = 36;
        combo.MinHeight = 36;
        combo.MaxHeight = 36;
        combo.Margin = new Thickness(0);
        combo.HorizontalAlignment = HorizontalAlignment.Stretch;
        combo.VerticalAlignment = VerticalAlignment.Center;
        combo.VerticalContentAlignment = VerticalAlignment.Center;
        combo.HorizontalContentAlignment = HorizontalAlignment.Left;
        combo.Padding = new Thickness(10, 0, 8, 0);
        combo.SelectionChanged += ReleaseToolbarCombo_SelectionChanged;

        Grid.SetRow(label, 0);
        Grid.SetRow(combo, 1);
        field.Children.Add(label);
        field.Children.Add(combo);

        Grid.SetColumn(field, column);
        toolbar.Children.Add(field);
    }

    private static void AlignReleaseToolbarButtonContent(object? content)
    {
        if (content is StackPanel panel)
        {
            panel.VerticalAlignment = VerticalAlignment.Center;
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            foreach (var child in panel.Children.OfType<FrameworkElement>())
                child.VerticalAlignment = VerticalAlignment.Center;
        }
        else if (content is FrameworkElement element)
        {
            element.VerticalAlignment = VerticalAlignment.Center;
            element.HorizontalAlignment = HorizontalAlignment.Center;
        }
    }

    private void ReleaseToolbarCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo)
            combo.ToolTip = ReleaseToolbarSelectedText(combo);
    }

    private void RefreshReleaseToolbarTooltips()
    {
        SourceCombo.ToolTip = ReleaseToolbarSelectedText(SourceCombo);
        AdapterCombo.ToolTip = ReleaseToolbarSelectedText(AdapterCombo);
        StreamCombo.ToolTip = ReleaseToolbarSelectedText(StreamCombo);
        ViewCombo.ToolTip = ReleaseToolbarSelectedText(ViewCombo);
    }

    private static string ReleaseToolbarSelectedText(ComboBox combo)
    {
        return combo.SelectedItem switch
        {
            ComboBoxItem item => item.Content?.ToString() ?? string.Empty,
            null => combo.Text,
            _ => combo.SelectedItem.ToString() ?? combo.Text
        };
    }
}

internal static class ReleaseToolbarBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoaded));
    }

    private static void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.ScheduleReleaseToolbarFix();
    }
}
