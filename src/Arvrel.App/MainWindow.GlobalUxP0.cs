using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _p0GlobalUxInitialized;
    private Border? _p0AnalysisDivider;
    private int _p0SemanticDivider;
    private string? _p0ProtectionSemanticSignature;

    internal void InitializeGlobalUxFoundation()
    {
        if (_p0GlobalUxInitialized)
            return;

        if (!IsLoaded || !_phasorWorkspaceInitialized || _analysisHost is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeGlobalUxFoundation));
            return;
        }

        _p0GlobalUxInitialized = true;

        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        P0PolishProductBar();
        P0BuildContextBar();
        P0ApplyWorkspaceProportions();
        P0ApplyAnalysisInstrument();
        P0ApplyProtectionRail();
        P0ApplyReadability();
        P0RefreshProtectionSemanticColors(force: true);

        SizeChanged += P0Window_SizeChanged;
        SourceCombo.SelectionChanged += P0AnalysisContextChanged;
        foreach (var button in _analysisModeButtons.Values)
            button.Click += P0AnalysisContextChanged;

        _timer.Tick += P0SemanticTimer_Tick;
        Closed += (_, _) => _timer.Tick -= P0SemanticTimer_Tick;
    }

    private void P0PolishProductBar()
    {
        if (Content is Grid root && root.RowDefinitions.Count >= 4)
        {
            root.RowDefinitions[0].Height = new GridLength(44);
            root.RowDefinitions[1].Height = new GridLength(58);
            root.RowDefinitions[3].Height = new GridLength(26);
        }

        OperatingModeCombo.Width = 148;
        OperatingModeCombo.Height = 30;
        OperatingModeCombo.MinHeight = 30;
        OperatingModeCombo.MaxHeight = 30;
        OperatingModeCombo.Margin = new Thickness(0, 0, 8, 0);

        EngineModeText.Visibility = Visibility.Collapsed;
        ActiveSettingsStatusText.ToolTip = EngineModeText.Text;

        TopHealthText.FontSize = 9.8;
        TopHealthText.LineHeight = 12;
        TopHealthLed.Width = 8;
        TopHealthLed.Height = 8;

        if (P0FindAncestor<Border>(TopHealthText) is { } healthBadge)
        {
            healthBadge.Height = 30;
            healthBadge.Padding = new Thickness(9, 0, 9, 0);
            healthBadge.Margin = new Thickness(0);
            healthBadge.CornerRadius = new CornerRadius(5);
        }

        var header = P0FindAncestor<Border>(OperatingModeCombo);
        if (header is not null)
        {
            foreach (var title in P0VisualDescendants<TextBlock>(header))
            {
                if (title.Text == "ARVREL")
                {
                    title.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
                    title.FontSize = 14;
                    title.FontWeight = FontWeights.SemiBold;
                }
                else if (title.Text == "IEC 61850 Process Bus Protection Laboratory")
                {
                    title.FontSize = 9.6;
                    title.Foreground = P0Brush("#A7B7C1");
                }
            }
        }
    }

    private void P0BuildContextBar()
    {
        if (SourceCombo.Parent is not Grid toolbar || RunButton.Parent is not StackPanel actionPanel)
            return;

        toolbar.Children.Clear();
        toolbar.ColumnDefinitions.Clear();
        toolbar.Margin = new Thickness(12, 5, 12, 5);
        toolbar.VerticalAlignment = VerticalAlignment.Stretch;
        toolbar.UseLayoutRounding = true;
        toolbar.SnapsToDevicePixels = true;

        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.02, GridUnitType.Star), MinWidth = 128 });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.58, GridUnitType.Star), MinWidth = 178 });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.42, GridUnitType.Star), MinWidth = 166 });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.86, GridUnitType.Star), MinWidth = 106 });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        P0AddContextField(toolbar, 0, "SOURCE", SourceCombo);
        P0AddContextField(toolbar, 2, "ADAPTER", AdapterCombo);
        P0AddContextField(toolbar, 4, "SV STREAM", StreamCombo);
        P0AddContextField(toolbar, 6, "VIEW", ViewCombo);

        var actionHost = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
        actionHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        actionHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

        Grid.SetRow(actionPanel, 1);
        actionPanel.Margin = new Thickness(0);
        actionPanel.HorizontalAlignment = HorizontalAlignment.Right;
        actionPanel.VerticalAlignment = VerticalAlignment.Center;
        actionHost.Children.Add(actionPanel);

        Grid.SetColumn(actionHost, 8);
        toolbar.Children.Add(actionHost);

        foreach (var button in actionPanel.Children.OfType<Button>())
        {
            button.Height = 34;
            button.MinHeight = 34;
            button.MaxHeight = 34;
            button.VerticalAlignment = VerticalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;

            if (!ReferenceEquals(button, RunButton))
            {
                button.Width = 34;
                button.MinWidth = 34;
                button.MaxWidth = 34;
                button.Padding = new Thickness(0);
                button.Background = P0Brush("#FFFFFF");
                button.BorderBrush = P0Brush("#D5DFE6");
            }
        }

        RunButton.MinWidth = 116;
        RunButton.Padding = new Thickness(13, 0, 13, 0);
        RunButton.Margin = new Thickness(2, 0, 0, 0);
        RunButtonText.VerticalAlignment = VerticalAlignment.Center;
        RunButtonIcon.VerticalAlignment = VerticalAlignment.Center;

        P0RefreshContextTooltips();
    }

    private void P0AddContextField(Grid toolbar, int column, string caption, ComboBox combo)
    {
        var field = new Grid
        {
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
        field.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        field.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

        var label = new TextBlock
        {
            Text = caption,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 9.3,
            FontWeight = FontWeights.SemiBold,
            Foreground = P0Brush("#5A6C79"),
            VerticalAlignment = VerticalAlignment.Top,
            LineHeight = 11,
            Margin = new Thickness(1, 0, 0, 1),
            TextWrapping = TextWrapping.NoWrap
        };

        combo.Width = double.NaN;
        combo.MinWidth = 0;
        combo.MaxWidth = double.PositiveInfinity;
        combo.Height = 34;
        combo.MinHeight = 34;
        combo.MaxHeight = 34;
        combo.Margin = new Thickness(0);
        combo.HorizontalAlignment = HorizontalAlignment.Stretch;
        combo.VerticalAlignment = VerticalAlignment.Center;
        combo.VerticalContentAlignment = VerticalAlignment.Center;
        combo.HorizontalContentAlignment = HorizontalAlignment.Left;
        combo.Padding = new Thickness(10, 0, 8, 0);
        combo.SelectionChanged += P0ContextCombo_SelectionChanged;

        Grid.SetRow(label, 0);
        Grid.SetRow(combo, 1);
        field.Children.Add(label);
        field.Children.Add(combo);
        Grid.SetColumn(field, column);
        toolbar.Children.Add(field);
    }

    private void P0ApplyWorkspaceProportions()
    {
        if (Content is not Grid root)
            return;

        var workspace = root.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => Grid.GetRow(grid) == 2 && grid.ColumnDefinitions.Count == 3);
        if (workspace is null)
            return;

        var compact = ActualWidth > 0 && ActualWidth < 1380;
        workspace.Margin = new Thickness(10);
        workspace.ColumnDefinitions[0].Width = new GridLength(2.28, GridUnitType.Star);
        workspace.ColumnDefinitions[0].MinWidth = compact ? 720 : 750;
        workspace.ColumnDefinitions[1].Width = new GridLength(8);
        workspace.ColumnDefinitions[2].Width = new GridLength(0.95, GridUnitType.Star);
        workspace.ColumnDefinitions[2].MinWidth = compact ? 400 : 420;
        workspace.ColumnDefinitions[2].MaxWidth = compact ? 460 : 500;

        var relayBay = workspace.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 2);
        if (relayBay is not null)
        {
            relayBay.Background = P0ResourceBrush("UxInstrumentBayBrush", "#151D24");
            relayBay.BorderBrush = Brushes.Transparent;
            relayBay.BorderThickness = new Thickness(0);
            relayBay.Padding = new Thickness(8);
            relayBay.CornerRadius = new CornerRadius(6);
            relayBay.ClipToBounds = false;
        }

        var analysisColumn = workspace.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => Grid.GetColumn(grid) == 0 && grid.RowDefinitions.Count >= 3);
        if (analysisColumn is not null)
        {
            analysisColumn.RowDefinitions[1].Height = new GridLength(8);
            analysisColumn.RowDefinitions[2].Height = new GridLength(compact ? 150 : 156);
        }
    }

    private void P0ApplyAnalysisInstrument()
    {
        if (_analysisHost is null || _phasorScope is null)
            return;

        _analysisHost.Margin = new Thickness(10);
        _analysisHost.Background = P0ResourceBrush("UxInstrumentSurfaceBrush", "#101820");
        _analysisHost.ClipToBounds = true;
        SmvScope.Margin = new Thickness(0);
        _phasorScope.Margin = new Thickness(0);

        if (_p0AnalysisDivider is null)
        {
            _p0AnalysisDivider = new Border
            {
                Background = P0ResourceBrush("UxInstrumentDividerBrush", "#2A3944"),
                IsHitTestVisible = false
            };
            Grid.SetColumn(_p0AnalysisDivider, 1);
            _analysisHost.Children.Add(_p0AnalysisDivider);
        }

        var columns = _analysisHost.ColumnDefinitions;
        if (columns.Count >= 3)
        {
            var split = columns[0].Width.Value > 0.001 && columns[2].Width.Value > 0.001;
            columns[1].Width = new GridLength(split ? 1 : 0);
            _p0AnalysisDivider.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var button in _analysisModeButtons.Values)
        {
            button.Height = 26;
            button.MinHeight = 26;
            button.MaxHeight = 26;
            button.BorderThickness = new Thickness(0);
            button.Margin = new Thickness(0);
            button.FontSize = 9.2;
            button.FontWeight = FontWeights.SemiBold;
        }

        if (_phasorQuantityCombo is not null)
        {
            _phasorQuantityCombo.Width = 96;
            _phasorQuantityCombo.Height = 30;
            _phasorQuantityCombo.MinHeight = 30;
            _phasorQuantityCombo.MaxHeight = 30;
            _phasorQuantityCombo.Margin = new Thickness(7, 0, 0, 0);
        }
    }

    private void P0ApplyProtectionRail()
    {
        var operationBorder = P0VisualAncestors<Border>(ProtectionReasonText)
            .FirstOrDefault(border =>
                border.Parent is Grid grid &&
                grid.RowDefinitions.Count == 3 &&
                Grid.GetRow(border) == 2);

        if (operationBorder?.Parent is Grid leftWorkspace)
        {
            leftWorkspace.RowDefinitions[1].Height = new GridLength(8);
            leftWorkspace.RowDefinitions[2].Height = new GridLength(156);
        }

        if (ProtectionReasonText.Parent is StackPanel titleLine &&
            titleLine.Parent is Grid headerGrid &&
            headerGrid.Parent is Border headerBorder &&
            headerBorder.Parent is Grid operationGrid &&
            operationGrid.RowDefinitions.Count >= 2)
        {
            operationGrid.RowDefinitions[0].Height = new GridLength(38);
            headerBorder.Padding = new Thickness(12, 0, 12, 0);
        }

        ProtectionReasonText.Margin = new Thickness(6, 0, 0, 0);
        ProtectionReasonText.FontSize = 10.2;
        PermissionBadge.Padding = new Thickness(8, 2, 8, 2);
        PermissionText.FontSize = 9.1;

        var settingTexts = new[] { Phase50SettingText, Phase51SettingText, Earth50SettingText, Earth51SettingText };
        var stateTexts = new[] { Phase50StateText, Phase51StateText, Earth50StateText, Earth51StateText };
        var progressBars = new[] { Phase50Progress, Phase51Progress, Earth50Progress, Earth51Progress };

        foreach (var text in settingTexts)
        {
            text.FontSize = 10.1;
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            P0BindTextTooltip(text);
            if (text.Parent is Border tag)
            {
                tag.Background = Brushes.Transparent;
                tag.BorderBrush = Brushes.Transparent;
                tag.BorderThickness = new Thickness(0);
                tag.Padding = new Thickness(2, 0, 2, 0);
            }
        }

        foreach (var text in stateTexts)
        {
            text.FontSize = 10.1;
            text.Margin = new Thickness(2, 6, 0, 4);
        }

        foreach (var progress in progressBars)
        {
            progress.Height = 3;
            progress.Background = P0Brush("#E1E7EC");
        }

        EventTraceText.FontSize = 9.4;
        EventTraceText.LineHeight = 13;
        EventTraceText.TextWrapping = TextWrapping.NoWrap;
        EventTraceText.TextTrimming = TextTrimming.CharacterEllipsis;
        P0BindTextTooltip(EventTraceText);
    }

    private void P0ApplyReadability()
    {
        foreach (var text in new[]
                 {
                     StreamHealthText,
                     WaveformSubtitleText,
                     ProtectionReasonText,
                     RelayFooterText,
                     StatusText,
                     SampleCounterText,
                     FpsText,
                     ActiveSettingsStatusText,
                     SclStatusText
                 })
        {
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            P0BindTextTooltip(text);
        }

        WaveformSubtitleText.FontSize = 10.1;
        StreamHealthText.FontSize = 9.9;
        StatusText.FontSize = 10;
        ActiveSettingsStatusText.FontSize = 9.1;
        SclStatusText.FontSize = 9.1;
    }

    private void P0RefreshProtectionSemanticColors(bool force)
    {
        var signature = string.Join(
            '|',
            Phase50StateText.Text,
            Phase51StateText.Text,
            Earth50StateText.Text,
            Earth51StateText.Text);
        if (!force && string.Equals(signature, _p0ProtectionSemanticSignature, StringComparison.Ordinal))
            return;
        _p0ProtectionSemanticSignature = signature;

        P0ApplyElementSemanticColor(Phase50StateText, Phase50Progress);
        P0ApplyElementSemanticColor(Phase51StateText, Phase51Progress);
        P0ApplyElementSemanticColor(Earth50StateText, Earth50Progress);
        P0ApplyElementSemanticColor(Earth51StateText, Earth51Progress);
    }

    private void P0ApplyElementSemanticColor(TextBlock stateText, ProgressBar progress)
    {
        var state = stateText.Text ?? string.Empty;
        Brush foreground;
        if (state.Contains("OPERATED", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("TRIP", StringComparison.OrdinalIgnoreCase))
        {
            foreground = P0ResourceBrush("UxTripBrush", "#C44946");
        }
        else if (state.Contains("PICKUP", StringComparison.OrdinalIgnoreCase) ||
                 state.Contains("TIMING", StringComparison.OrdinalIgnoreCase))
        {
            foreground = P0ResourceBrush("UxWarningBrush", "#BD852B");
        }
        else if (state.Contains("BLOCK", StringComparison.OrdinalIgnoreCase) ||
                 state.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            foreground = P0ResourceBrush("UxMutedBrush", "#667887");
        }
        else
        {
            foreground = P0ResourceBrush("UxMutedBrush", "#667887");
        }

        stateText.Foreground = foreground;
        progress.Foreground = state.Contains("READY", StringComparison.OrdinalIgnoreCase)
            ? P0Brush("#C9D4DC")
            : foreground;
    }

    private void P0SemanticTimer_Tick(object? sender, EventArgs e)
    {
        _p0SemanticDivider++;
        if (_p0SemanticDivider < 5)
            return;
        _p0SemanticDivider = 0;
        P0RefreshProtectionSemanticColors(force: false);
    }

    private void P0Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        P0ApplyWorkspaceProportions();
        P0ApplyResponsiveDensity();
    }

    private void P0ApplyResponsiveDensity()
    {
        var compact = ActualWidth < 1380;
        OperatingModeCombo.Width = compact ? 142 : 148;
        ProtectionReasonText.MaxWidth = compact ? 430 : 620;
        RelayFooterText.MaxWidth = compact ? 290 : 380;
        SclStatusText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }

    private void P0AnalysisContextChanged(object sender, RoutedEventArgs e)
        => P0QueueAnalysisInstrumentRefresh();

    private void P0AnalysisContextChanged(object sender, SelectionChangedEventArgs e)
        => P0QueueAnalysisInstrumentRefresh();

    private void P0QueueAnalysisInstrumentRefresh()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(P0ApplyAnalysisInstrument));
    }

    private void P0ContextCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo)
            combo.ToolTip = P0SelectedComboText(combo);
    }

    private void P0RefreshContextTooltips()
    {
        SourceCombo.ToolTip = P0SelectedComboText(SourceCombo);
        AdapterCombo.ToolTip = P0SelectedComboText(AdapterCombo);
        StreamCombo.ToolTip = P0SelectedComboText(StreamCombo);
        ViewCombo.ToolTip = P0SelectedComboText(ViewCombo);
    }

    private static string P0SelectedComboText(ComboBox combo)
        => combo.SelectedItem switch
        {
            ComboBoxItem item => item.Content?.ToString() ?? string.Empty,
            null => combo.Text,
            _ => combo.SelectedItem.ToString() ?? combo.Text
        };

    private void P0BindTextTooltip(TextBlock text)
        => BindingOperations.SetBinding(
            text,
            FrameworkElement.ToolTipProperty,
            new Binding(nameof(TextBlock.Text))
            {
                Source = text,
                Mode = BindingMode.OneWay
            });

    private Brush P0ResourceBrush(string key, string fallback)
        => TryFindResource(key) as Brush ?? P0Brush(fallback);

    private static Brush P0Brush(string color)
        => (Brush)new BrushConverter().ConvertFromString(color)!;

    private static T? P0FindAncestor<T>(DependencyObject child) where T : DependencyObject
        => P0VisualAncestors<T>(child).FirstOrDefault();

    private static IEnumerable<T> P0VisualAncestors<T>(DependencyObject child) where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(child);
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is T typed)
                yield return typed;
        }
    }

    private static IEnumerable<T> P0VisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;
            foreach (var nested in P0VisualDescendants<T>(child))
                yield return nested;
        }
    }
}
