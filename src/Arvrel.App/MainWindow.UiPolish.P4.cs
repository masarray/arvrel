using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _p4UiPolishInitialized;
    private bool _p4UiPolishApplied;

    internal void InitializeP4UiPolish()
    {
        if (_p4UiPolishInitialized)
            return;

        _p4UiPolishInitialized = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);

        SizeChanged += (_, _) => ApplyP4ResponsiveLayout();
        ContentRendered += (_, _) => ScheduleP4UiPolish();
        ScheduleP4UiPolish();
    }

    private void ScheduleP4UiPolish() => Dispatcher.BeginInvoke(
        DispatcherPriority.ApplicationIdle,
        new Action(() =>
        {
            ApplyP4VisualPolish();
            ApplyP4ResponsiveLayout();
        }));

    private void ApplyP4VisualPolish()
    {
        if (_p4UiPolishApplied)
            return;

        _p4UiPolishApplied = true;
        PolishP4Brand();
        PolishP4WaveformHeader();
        PolishP4ProtectionHeader();
        PolishP4ToolbarLabels();
        PolishP4RelayFooter();
        PolishP4StatusBar();
    }

    private void PolishP4Brand()
    {
        if (OperatingModeCombo.Parent is not DependencyObject modeParent)
            return;

        var header = FindP4Ancestor<Border>(modeParent);
        if (header is null)
            return;

        var title = P4VisualDescendants<TextBlock>(header)
            .FirstOrDefault(text => text.Text == "ARVREL");
        if (title is not null)
        {
            title.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
            title.FontSize = 15;
            title.FontWeight = FontWeights.SemiBold;
            title.LineHeight = 18;
        }

        var subtitle = P4VisualDescendants<TextBlock>(header)
            .FirstOrDefault(text => text.Text == "IEC 61850 Process Bus Protection Laboratory");
        if (subtitle is not null)
        {
            subtitle.FontSize = 10.1;
            subtitle.LineHeight = 13;
            subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
            BindP4Tooltip(subtitle);
        }

        EngineModeText.MaxWidth = 240;
        PolishP4DynamicText(EngineModeText);
    }

    private void PolishP4WaveformHeader()
    {
        WaveformSubtitleText.FontSize = 10.3;
        WaveformSubtitleText.LineHeight = 14;
        WaveformSubtitleText.Foreground = P4Brush(83, 102, 116);
        WaveformSubtitleText.Margin = new Thickness(0, 3, 0, 0);
        PolishP4DynamicText(WaveformSubtitleText);

        StreamHealthText.FontSize = 10;
        StreamHealthText.LineHeight = 13;
        StreamHealthText.Foreground = P4Brush(91, 108, 121);
        PolishP4DynamicText(StreamHealthText);

        if (StreamHealthText.Parent is StackPanel titleLine)
        {
            titleLine.VerticalAlignment = VerticalAlignment.Center;
            var heading = titleLine.Children.OfType<TextBlock>()
                .FirstOrDefault(text => !ReferenceEquals(text, StreamHealthText));
            if (heading is not null)
            {
                heading.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
                heading.FontSize = 13.5;
                heading.FontWeight = FontWeights.SemiBold;
                heading.LineHeight = 17;
            }

            foreach (var badge in titleLine.Children.OfType<Border>())
            {
                badge.Margin = new Thickness(9, 0, 0, 0);
                badge.Padding = new Thickness(7, 2, 7, 2);
                badge.VerticalAlignment = VerticalAlignment.Center;
                if (badge.Child is TextBlock badgeText)
                {
                    badgeText.FontSize = 9.1;
                    badgeText.LineHeight = 11;
                }
            }
        }

        if (IaValueText.Parent is StackPanel iaGroup && iaGroup.Parent is StackPanel summary)
        {
            var groups = summary.Children.OfType<StackPanel>().ToArray();
            for (var index = 0; index < groups.Length; index++)
            {
                var group = groups[index];
                group.VerticalAlignment = VerticalAlignment.Center;
                group.Margin = new Thickness(0, 0, index == groups.Length - 1 ? 0 : 12, 0);

                var texts = group.Children.OfType<TextBlock>().ToArray();
                if (texts.Length > 0)
                {
                    texts[0].FontSize = 9.7;
                    texts[0].LineHeight = 12;
                    texts[0].Margin = new Thickness(0, 0, 0, 1);
                    texts[0].Foreground = P4Brush(92, 109, 121);
                }

                if (texts.Length > 1)
                {
                    texts[1].FontSize = 11.2;
                    texts[1].LineHeight = 14;
                    texts[1].FontFamily = new FontFamily("Cascadia Mono, Consolas");
                    PolishP4DynamicText(texts[1]);
                }
            }
        }
    }

    private void PolishP4ProtectionHeader()
    {
        ProtectionReasonText.FontSize = 10.4;
        ProtectionReasonText.LineHeight = 14;
        ProtectionReasonText.Foreground = P4Brush(86, 104, 117);
        ProtectionReasonText.Margin = new Thickness(6, 0, 0, 0);
        ProtectionReasonText.MaxWidth = 620;
        PolishP4DynamicText(ProtectionReasonText);

        if (ProtectionReasonText.Parent is StackPanel titleLine)
        {
            var heading = titleLine.Children.OfType<TextBlock>()
                .FirstOrDefault(text => !ReferenceEquals(text, ProtectionReasonText));
            if (heading is not null)
            {
                heading.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
                heading.FontSize = 12.8;
                heading.FontWeight = FontWeights.SemiBold;
                heading.LineHeight = 16;
            }
        }

        PermissionBadge.Padding = new Thickness(9, 3, 9, 3);
        PermissionText.LineHeight = 12;
    }

    private void PolishP4ToolbarLabels()
    {
        if (SourceCombo.Parent is not Grid toolbar)
            return;

        foreach (var label in toolbar.Children.OfType<TextBlock>())
        {
            if (Grid.GetColumn(label) is not (0 or 3 or 6 or 9))
                continue;

            label.FontSize = 9.8;
            label.LineHeight = 13;
            label.FontWeight = FontWeights.SemiBold;
            label.Foreground = P4Brush(90, 108, 121);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.TextWrapping = TextWrapping.NoWrap;
        }
    }

    private void PolishP4RelayFooter()
    {
        RelayFooterText.FontSize = 9.4;
        RelayFooterText.LineHeight = 12;
        RelayFooterText.Foreground = P4Brush(77, 98, 112);
        RelayFooterText.MaxWidth = 380;
        PolishP4DynamicText(RelayFooterText);

        if (RelayFooterText.Parent is StackPanel footer)
        {
            foreach (var text in footer.Children.OfType<TextBlock>())
            {
                text.FontSize = 9.4;
                text.LineHeight = 12;
                PolishP4DynamicText(text);
            }
        }
    }

    private void PolishP4StatusBar()
    {
        StatusText.FontSize = 10.2;
        StatusText.LineHeight = 13;
        StatusText.Foreground = P4Brush(77, 96, 109);
        StatusText.Margin = new Thickness(0, 0, 16, 0);
        PolishP4DynamicText(StatusText);

        if (ActiveSettingsStatusText.Parent is not StackPanel summary)
            return;

        foreach (var text in summary.Children.OfType<TextBlock>())
        {
            text.FontSize = 9.2;
            text.LineHeight = 12;
            PolishP4DynamicText(text);
        }

        ActiveSettingsStatusText.MaxWidth = 245;
        SclStatusText.MaxWidth = 175;
    }

    private void ApplyP4ResponsiveLayout()
    {
        if (!_p4UiPolishInitialized || ActualWidth <= 0)
            return;

        var compact = ActualWidth < 1380;
        var medium = ActualWidth < 1510;
        ApplyP4ToolbarWidths(compact, medium);
        ApplyP4HeaderDensity(compact, medium);
        ApplyP4WaveformDensity(compact, medium);
        ApplyP4FooterDensity(compact, medium);
    }

    private void ApplyP4ToolbarWidths(bool compact, bool medium)
    {
        if (SourceCombo.Parent is not Grid toolbar || toolbar.ColumnDefinitions.Count < 13)
            return;

        toolbar.ColumnDefinitions[1].Width = new GridLength(compact ? 138 : medium ? 152 : 165);
        toolbar.ColumnDefinitions[4].Width = new GridLength(compact ? 164 : medium ? 188 : 210);
        toolbar.ColumnDefinitions[7].Width = new GridLength(compact ? 154 : medium ? 174 : 190);
        toolbar.ColumnDefinitions[10].Width = new GridLength(compact ? 108 : medium ? 116 : 125);

        foreach (var column in new[] { 2, 5, 8 })
            toolbar.ColumnDefinitions[column].Width = new GridLength(compact ? 7 : 10);
    }

    private void ApplyP4HeaderDensity(bool compact, bool medium)
    {
        OperatingModeCombo.Width = compact ? 160 : medium ? 164 : 168;
        EngineModeText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        EngineModeText.MaxWidth = medium ? 190 : 240;
    }

    private void ApplyP4WaveformDensity(bool compact, bool medium)
    {
        if (StreamHealthText.Parent is not StackPanel titleLine ||
            titleLine.Parent is not StackPanel titleBlock ||
            titleBlock.Parent is not Grid header ||
            header.ColumnDefinitions.Count < 3)
            return;

        var titleWidth = compact ? 245 : medium ? 290 : 340;
        header.ColumnDefinitions[0].Width = new GridLength(titleWidth);
        titleBlock.Width = titleWidth;
        WaveformSubtitleText.Width = titleWidth;
        StreamHealthText.MaxWidth = compact ? 105 : medium ? 130 : 150;

        if (_phasorQuantityCombo is not null)
            _phasorQuantityCombo.Width = compact ? 100 : medium ? 104 : 108;

        if (IaValueText.Parent is StackPanel iaGroup && iaGroup.Parent is StackPanel summary)
        {
            summary.Margin = new Thickness(compact ? 7 : medium ? 10 : 14, 0, 0, 0);
            var groups = summary.Children.OfType<StackPanel>().ToArray();
            for (var index = 0; index < groups.Length; index++)
            {
                groups[index].MinWidth = index == groups.Length - 1
                    ? compact ? 55 : medium ? 59 : 62
                    : compact ? 42 : medium ? 45 : 47;
                groups[index].Margin = new Thickness(
                    0,
                    0,
                    index == groups.Length - 1 ? 0 : compact ? 7 : medium ? 9 : 12,
                    0);
            }
        }

        RelayFooterText.MaxWidth = compact ? 285 : medium ? 330 : 380;
        ProtectionReasonText.MaxWidth = compact ? 430 : medium ? 520 : 620;
    }

    private void ApplyP4FooterDensity(bool compact, bool medium)
    {
        if (ActiveSettingsStatusText.Parent is not StackPanel summary)
            return;

        SclStatusText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ActiveSettingsStatusText.Margin = new Thickness(0, 0, compact ? 9 : 14, 0);
        SclStatusText.Margin = new Thickness(0, 0, medium ? 9 : 14, 0);
        ActiveSettingsStatusText.MaxWidth = compact ? 190 : medium ? 220 : 245;
        SclStatusText.MaxWidth = compact ? 0 : medium ? 125 : 175;

        var safety = summary.Children.OfType<TextBlock>().LastOrDefault();
        if (safety is null)
            return;

        const string full = "VIRTUAL OUTPUT · NO GOOSE · NO PHYSICAL TRIP";
        safety.Text = compact ? "VIRTUAL ONLY · NO GOOSE · NO PHYSICAL TRIP" : full;
        safety.ToolTip = full;
    }

    private static void PolishP4DynamicText(TextBlock text)
    {
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.NoWrap;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        BindP4Tooltip(text);
    }

    private static void BindP4Tooltip(TextBlock text) => BindingOperations.SetBinding(
        text,
        FrameworkElement.ToolTipProperty,
        new Binding(nameof(TextBlock.Text))
        {
            Source = text,
            Mode = BindingMode.OneWay
        });

    private static SolidColorBrush P4Brush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static T? FindP4Ancestor<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is T typed)
                return typed;
        }

        return null;
    }

    private static IEnumerable<T> P4VisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;

            foreach (var descendant in P4VisualDescendants<T>(child))
                yield return descendant;
        }
    }
}

internal static class P4UiPolishBootstrap
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
            window.InitializeP4UiPolish();
    }
}
