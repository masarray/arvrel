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

    private void ScheduleP4UiPolish()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() =>
            {
                ApplyP4VisualPolish();
                ApplyP4ResponsiveLayout();
            }));
    }

    private void ApplyP4VisualPolish()
    {
        if (_p4UiPolishApplied)
            return;

        _p4UiPolishApplied = true;

        PolishApplicationBrand();
        PolishWaveformHeader();
        PolishProtectionHeader();
        PolishSourceToolbarLabels();
        PolishRelayFooter();
        PolishApplicationStatusBar();
    }

    private void PolishApplicationBrand()
    {
        if (OperatingModeCombo.Parent is not DependencyObject modeParent)
            return;

        var headerBorder = FindAncestor<Border>(modeParent);
        if (headerBorder is null)
            return;

        var brandTitle = Descendants<TextBlock>(headerBorder)
            .FirstOrDefault(text => text.Text == "ARVREL");
        if (brandTitle is not null)
        {
            brandTitle.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
            brandTitle.FontSize = 15;
            brandTitle.FontWeight = FontWeights.SemiBold;
            brandTitle.LineHeight = 18;
            brandTitle.VerticalAlignment = VerticalAlignment.Center;
        }

        var brandSubtitle = Descendants<TextBlock>(headerBorder)
            .FirstOrDefault(text => text.Text == "IEC 61850 Process Bus Protection Laboratory");
        if (brandSubtitle is not null)
        {
            brandSubtitle.FontSize = 10.1;
            brandSubtitle.LineHeight = 13;
            brandSubtitle.VerticalAlignment = VerticalAlignment.Center;
            brandSubtitle.TextTrimming = TextTrimming.CharacterEllipsis;
            brandSubtitle.ToolTip = brandSubtitle.Text;
        }

        EngineModeText.MaxWidth = 225;
        PolishDynamicText(EngineModeText, trim: true);
    }

    private void PolishWaveformHeader()
    {
        WaveformSubtitleText.FontSize = 10.3;
        WaveformSubtitleText.LineHeight = 14;
        WaveformSubtitleText.Foreground = CreateBrush(83, 102, 116);
        WaveformSubtitleText.Margin = new Thickness(0, 3, 0, 0);
        WaveformSubtitleText.TextWrapping = TextWrapping.NoWrap;
        PolishDynamicText(WaveformSubtitleText, trim: true);

        StreamHealthText.FontSize = 10;
        StreamHealthText.LineHeight = 13;
        StreamHealthText.Foreground = CreateBrush(91, 108, 121);
        PolishDynamicText(StreamHealthText, trim: true);

        if (StreamHealthText.Parent is StackPanel titleLine)
        {
            titleLine.VerticalAlignment = VerticalAlignment.Center;
            var waveformTitle = titleLine.Children.OfType<TextBlock>()
                .FirstOrDefault(text => !ReferenceEquals(text, StreamHealthText));
            if (waveformTitle is not null)
            {
                waveformTitle.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
                waveformTitle.FontSize = 13.5;
                waveformTitle.FontWeight = FontWeights.SemiBold;
                waveformTitle.LineHeight = 17;
                waveformTitle.VerticalAlignment = VerticalAlignment.Center;
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
                    badgeText.VerticalAlignment = VerticalAlignment.Center;
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
                    texts[0].Foreground = CreateBrush(92, 109, 121);
                    texts[0].VerticalAlignment = VerticalAlignment.Center;
                    texts[0].TextWrapping = TextWrapping.NoWrap;
                }

                if (texts.Length > 1)
                {
                    texts[1].FontSize = 11.2;
                    texts[1].LineHeight = 14;
                    texts[1].FontFamily = new FontFamily("Cascadia Mono, Consolas");
                    texts[1].VerticalAlignment = VerticalAlignment.Center;
                    texts[1].TextWrapping = TextWrapping.NoWrap;
                    PolishDynamicText(texts[1], trim: true);
                }
            }
        }
    }

    private void PolishProtectionHeader()
    {
        ProtectionReasonText.FontSize = 10.4;
        ProtectionReasonText.LineHeight = 14;
        ProtectionReasonText.Foreground = CreateBrush(86, 104, 117);
        ProtectionReasonText.Margin = new Thickness(6, 0, 0, 0);
        ProtectionReasonText.MaxWidth = 520;
        PolishDynamicText(ProtectionReasonText, trim: true);

        if (ProtectionReasonText.Parent is StackPanel titleLine)
        {
            titleLine.VerticalAlignment = VerticalAlignment.Center;
            var heading = titleLine.Children.OfType<TextBlock>()
                .FirstOrDefault(text => !ReferenceEquals(text, ProtectionReasonText));
            if (heading is not null)
            {
                heading.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
                heading.FontSize = 12.8;
                heading.FontWeight = FontWeights.SemiBold;
                heading.LineHeight = 16;
                heading.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        PermissionBadge.Padding = new Thickness(9, 3, 9, 3);
        PermissionBadge.VerticalAlignment = VerticalAlignment.Center;
        PermissionText.LineHeight = 12;
        PermissionText.VerticalAlignment = VerticalAlignment.Center;
    }

    private void PolishSourceToolbarLabels()
    {
        if (SourceCombo.Parent is not Grid toolbarGrid)
            return;

        foreach (var label in toolbarGrid.Children.OfType<TextBlock>())
        {
            var column = Grid.GetColumn(label);
            if (column is not (0 or 3 or 6 or 9))
                continue;

            label.FontSize = 9.8;
            label.LineHeight = 13;
            label.FontWeight = FontWeights.SemiBold;
            label.Foreground = CreateBrush(90, 108, 121);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.TextWrapping = TextWrapping.NoWrap;
        }
    }

    private void PolishRelayFooter()
    {
        RelayFooterText.FontSize = 9.4;
        RelayFooterText.LineHeight = 12;
        RelayFooterText.Foreground = CreateBrush(77, 98, 112);
        RelayFooterText.MaxWidth = 300;
        PolishDynamicText(RelayFooterText, trim: true);

        if (RelayFooterText.Parent is StackPanel relayFooterStack)
        {
            relayFooterStack.VerticalAlignment = VerticalAlignment.Center;
            foreach (var text in relayFooterStack.Children.OfType<TextBlock>())
            {
                text.FontSize = 9.4;
                text.LineHeight = 12;
                text.VerticalAlignment = VerticalAlignment.Center;
                text.TextWrapping = TextWrapping.NoWrap;
                PolishDynamicText(text, trim: true);
            }
        }
    }

    private void PolishApplicationStatusBar()
    {
        StatusText.FontSize = 10.2;
        StatusText.LineHeight = 13;
        StatusText.Foreground = CreateBrush(77, 96, 109);
        StatusText.Margin = new Thickness(0, 0, 16, 0);
        StatusText.VerticalAlignment = VerticalAlignment.Center;
        PolishDynamicText(StatusText, trim: true);

        if (ActiveSettingsStatusText.Parent is not StackPanel statusSummary)
            return;

        statusSummary.VerticalAlignment = VerticalAlignment.Center;
        statusSummary.HorizontalAlignment = HorizontalAlignment.Right;

        foreach (var text in statusSummary.Children.OfType<TextBlock>())
        {
            text.FontSize = 9.2;
            text.LineHeight = 12;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            BindTooltipToText(text);
        }

        ActiveSettingsStatusText.MaxWidth = 210;
        SclStatusText.MaxWidth = 86;
    }

    private void ApplyP4ResponsiveLayout()
    {
        if (!_p4UiPolishInitialized || ActualWidth <= 0)
            return;

        var compact = ActualWidth < 1375;
        var medium = ActualWidth < 1460;

        ApplyResponsiveToolbarWidths(compact, medium);
        ApplyResponsiveHeader(compact, medium);
        ApplyResponsiveWaveformHeader(compact);
        ApplyResponsiveStatusBar(compact, medium);
    }

    private void ApplyResponsiveToolbarWidths(bool compact, bool medium)
    {
        if (SourceCombo.Parent is not Grid toolbarGrid || toolbarGrid.ColumnDefinitions.Count < 13)
            return;

        var sourceWidth = compact ? 138 : medium ? 152 : 165;
        var adapterWidth = compact ? 164 : medium ? 188 : 210;
        var streamWidth = compact ? 154 : medium ? 174 : 190;
        var viewWidth = compact ? 108 : medium ? 116 : 125;

        toolbarGrid.ColumnDefinitions[1].Width = new GridLength(sourceWidth);
        toolbarGrid.ColumnDefinitions[4].Width = new GridLength(adapterWidth);
        toolbarGrid.ColumnDefinitions[7].Width = new GridLength(streamWidth);
        toolbarGrid.ColumnDefinitions[10].Width = new GridLength(viewWidth);

        var gap = compact ? 7 : 10;
        foreach (var column in new[] { 2, 5, 8 })
            toolbarGrid.ColumnDefinitions[column].Width = new GridLength(gap);
    }

    private void ApplyResponsiveHeader(bool compact, bool medium)
    {
        OperatingModeCombo.Width = compact ? 132 : 142;
        EngineModeText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        EngineModeText.MaxWidth = medium ? 175 : 225;
    }

    private void ApplyResponsiveWaveformHeader(bool compact)
    {
        if (StreamHealthText.Parent is not StackPanel titleLine ||
            titleLine.Parent is not StackPanel titleBlock ||
            titleBlock.Parent is not Grid headerGrid ||
            headerGrid.ColumnDefinitions.Count < 3)
            return;

        var titleWidth = compact ? 210 : 232;
        headerGrid.ColumnDefinitions[0].Width = new GridLength(titleWidth);
        titleBlock.Width = titleWidth;
        WaveformSubtitleText.Width = titleWidth;
        StreamHealthText.MaxWidth = compact ? 72 : 92;

        if (_phasorQuantityCombo is not null)
            _phasorQuantityCombo.Width = compact ? 100 : 108;

        if (IaValueText.Parent is StackPanel iaGroup && iaGroup.Parent is StackPanel summary)
        {
            summary.Margin = new Thickness(compact ? 8 : 14, 0, 0, 0);
            var groups = summary.Children.OfType<StackPanel>().ToArray();
            for (var index = 0; index < groups.Length; index++)
            {
                groups[index].MinWidth = index == groups.Length - 1
                    ? compact ? 55 : 62
                    : compact ? 42 : 47;
                groups[index].Margin = new Thickness(
                    0,
                    0,
                    index == groups.Length - 1 ? 0 : compact ? 7 : 12,
                    0);
            }
        }
    }

    private void ApplyResponsiveStatusBar(bool compact, bool medium)
    {
        if (ActiveSettingsStatusText.Parent is not StackPanel statusSummary)
            return;

        SclStatusText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ActiveSettingsStatusText.Margin = new Thickness(0, 0, compact ? 9 : 14, 0);
        SclStatusText.Margin = new Thickness(0, 0, medium ? 9 : 14, 0);

        var safetyText = statusSummary.Children.OfType<TextBlock>().LastOrDefault();
        if (safetyText is not null)
        {
            const string full = "VIRTUAL OUTPUT · NO GOOSE · NO PHYSICAL TRIP";
            safetyText.Text = compact
                ? "VIRTUAL ONLY · NO GOOSE · NO PHYSICAL TRIP"
                : full;
            safetyText.ToolTip = full;
        }
    }

    private static void PolishDynamicText(TextBlock text, bool trim)
    {
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.NoWrap;
        if (trim)
            text.TextTrimming = TextTrimming.CharacterEllipsis;
        BindTooltipToText(text);
    }

    private static void BindTooltipToText(TextBlock text)
    {
        BindingOperations.SetBinding(
            text,
            FrameworkElement.ToolTipProperty,
            new Binding(nameof(TextBlock.Text))
            {
                Source = text,
                Mode = BindingMode.OneWay
            });
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
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

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;

            foreach (var descendant in Descendants<T>(child))
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
