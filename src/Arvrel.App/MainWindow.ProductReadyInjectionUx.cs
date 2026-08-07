using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

/// <summary>
/// Product-ready ownership pass for the left analysis/source workspace.
///
/// The left side is the virtual test source plus evidence viewer. The physical
/// virtual-relay faceplate remains a separate equipment authority on the right.
/// This coordinator intentionally runs after the existing source, CT, advanced
/// injection, run/stop and P0 foundations so it can arrange their real controls
/// without duplicating any engineering behavior.
/// </summary>
public partial class MainWindow
{
    private bool _productReadyInjectionUxInitialized;
    private StackPanel? _productTopSourceActions;
    private Grid? _productInjectionToolbar;
    private StackPanel? _productInjectionFooterActions;
    private DependencyPropertyDescriptor? _productInjectionStatusDescriptor;

    internal void InitializeProductReadyInjectionUx()
    {
        if (_productReadyInjectionUxInitialized)
            return;

        if (!IsLoaded ||
            !_p0GlobalUxInitialized ||
            !_virtualInjectionInitialized ||
            !_virtualInjectionPersistenceInitialized ||
            !_virtualInjectionRunStopInitialized ||
            !_ctModelSelectorInitialized ||
            !_advancedInjectionInitialized ||
            _virtualInjectionView is null ||
            _virtualInjectionPresetCombo is null ||
            _virtualInjectionFrequencyText is null ||
            _virtualInjectionStatusBadge is null ||
            _virtualInjectionStatusText is null ||
            _ctModelPresetCombo is null ||
            _advancedInjectionButton is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeProductReadyInjectionUx));
            return;
        }

        var toolbar = _virtualInjectionView.Children
            .OfType<Grid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 0);
        var table = _virtualInjectionView.Children
            .OfType<DataGrid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 1);
        var clearSource = FindProductButton(_virtualInjectionView, "Clear source")
                          ?? FindProductButton(_virtualInjectionView, "Clear injection");

        if (toolbar is null ||
            table is null ||
            clearSource?.Parent is not StackPanel footerActions ||
            RunButton.Parent is not StackPanel topSourceActions)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeProductReadyInjectionUx));
            return;
        }

        _productReadyInjectionUxInitialized = true;
        _productInjectionToolbar = toolbar;
        _productInjectionFooterActions = footerActions;
        _productTopSourceActions = topSourceActions;

        ProductFixTopChrome();
        ProductArrangeInjectionToolbar(toolbar);
        ProductPolishInjectionTable(table);
        ProductCleanInjectionFooter(footerActions);
        ProductMakeWaveformViewerReadOnly();
        ProductWireQuietStatusBadge();
        ProductWireAnalysisChrome();

        SourceCombo.SelectionChanged += ProductReadySourceChanged;
        RunButton.Loaded += ProductReadyRunButton_Loaded;
        Closed += ProductReadyInjectionUx_Closed;

        PlaceRunButtonForActiveSource();
        RefreshProductReadyAnalysisChrome();
    }

    private void ProductFixTopChrome()
    {
        if (Content is Grid root && root.RowDefinitions.Count >= 4)
        {
            root.RowDefinitions[0].Height = new GridLength(48);
            root.RowDefinitions[1].Height = new GridLength(62);
            root.RowDefinitions[3].Height = new GridLength(26);
        }

        if (SourceCombo.Parent is Grid field && field.Parent is Grid contextToolbar)
            contextToolbar.Margin = new Thickness(12, 6, 12, 6);
    }

    private void ProductArrangeInjectionToolbar(Grid toolbar)
    {
        if (_virtualInjectionPresetCombo is null ||
            _virtualInjectionFrequencyText is null ||
            _virtualInjectionStatusBadge is null ||
            _ctModelPresetCombo is null ||
            _advancedInjectionButton is null)
            return;

        if (_virtualInjectionView is Grid injectionRoot && injectionRoot.RowDefinitions.Count >= 3)
        {
            injectionRoot.RowDefinitions[0].Height = new GridLength(42);
            injectionRoot.RowDefinitions[2].Height = new GridLength(38);
        }

        // Source editing owns only source parameters. Lifecycle now lives in the
        // analysis header, so the CT selector can use the remaining width without
        // being clipped by a Start/Stop button.
        toolbar.Margin = new Thickness(0, 0, 0, 8);
        toolbar.ColumnDefinitions.Clear();
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(178) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
            MinWidth = 220
        });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var sourceLabel = toolbar.Children
            .OfType<TextBlock>()
            .FirstOrDefault(text => string.Equals(text.Text, "PRESET", StringComparison.OrdinalIgnoreCase));
        if (sourceLabel is not null)
        {
            sourceLabel.Text = "SOURCE";
            sourceLabel.Margin = new Thickness(2, 0, 7, 0);
            sourceLabel.ToolTip = "Voltage/current source preset";
            Grid.SetColumn(sourceLabel, 0);
        }

        _virtualInjectionPresetCombo.Height = 30;
        _virtualInjectionPresetCombo.MinHeight = 30;
        _virtualInjectionPresetCombo.MaxHeight = 30;
        _virtualInjectionPresetCombo.Padding = new Thickness(9, 0, 8, 0);
        _virtualInjectionPresetCombo.VerticalContentAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_virtualInjectionPresetCombo, 1);

        var frequencyLabel = toolbar.Children
            .OfType<TextBlock>()
            .FirstOrDefault(text => string.Equals(text.Text, "FREQ", StringComparison.OrdinalIgnoreCase));
        if (frequencyLabel is not null)
        {
            frequencyLabel.Text = "FREQ";
            frequencyLabel.Margin = new Thickness(0, 0, 7, 0);
            frequencyLabel.ToolTip = "Common synchronous frequency in hertz";
            Grid.SetColumn(frequencyLabel, 3);
        }

        _virtualInjectionFrequencyText.Height = 30;
        _virtualInjectionFrequencyText.MinHeight = 30;
        _virtualInjectionFrequencyText.MaxHeight = 30;
        _virtualInjectionFrequencyText.Padding = new Thickness(7, 0, 7, 0);
        _virtualInjectionFrequencyText.VerticalContentAlignment = VerticalAlignment.Center;
        _virtualInjectionFrequencyText.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(_virtualInjectionFrequencyText, 4);

        if (_ctModelPresetCombo.Parent is StackPanel ctSelector)
        {
            ctSelector.Margin = new Thickness(0);
            ctSelector.HorizontalAlignment = HorizontalAlignment.Left;
            ctSelector.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ctSelector, 6);

            var ctLabel = ctSelector.Children.OfType<TextBlock>().FirstOrDefault();
            if (ctLabel is not null)
            {
                ctLabel.Text = "CT MODEL";
                ctLabel.Margin = new Thickness(0, 0, 7, 0);
            }

            _ctModelPresetCombo.Width = 176;
            _ctModelPresetCombo.Height = 30;
            _ctModelPresetCombo.MinHeight = 30;
            _ctModelPresetCombo.MaxHeight = 30;
            _ctModelPresetCombo.Padding = new Thickness(8, 0, 8, 0);
            _ctModelPresetCombo.VerticalContentAlignment = VerticalAlignment.Center;
        }

        if (_advancedInjectionButton.Parent is Panel advancedParent)
            advancedParent.Children.Remove(_advancedInjectionButton);

        _virtualInjectionStatusBadge.Margin = new Thickness(8, 0, 0, 0);
        _virtualInjectionStatusBadge.Padding = new Thickness(7, 3, 7, 3);
        _virtualInjectionStatusBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_virtualInjectionStatusBadge, 7);
    }

    private void ProductPolishInjectionTable(DataGrid table)
    {
        table.RowHeight = 34;
        table.ColumnHeaderHeight = 32;
        table.FontSize = 10.2;
        table.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        table.HorizontalGridLinesBrush = ProductBrush("#E3E9EE");
        table.VerticalGridLinesBrush = Brushes.Transparent;
        table.RowBackground = Brushes.White;
        table.AlternatingRowBackground = ProductBrush("#FAFBFC");
        table.BorderBrush = ProductBrush("#D9E2E8");
        table.BorderThickness = new Thickness(1);

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 0, 10, 0)));
        cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        cellStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        var selected = new Trigger
        {
            Property = DataGridCell.IsSelectedProperty,
            Value = true
        };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, ProductBrush("#E7F0F6")));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, ProductBrush("#17232D")));
        cellStyle.Triggers.Add(selected);
        table.CellStyle = cellStyle;

        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 0, 8, 0)));
        headerStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 9.6));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, ProductBrush("#5B6D7A")));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, ProductBrush("#F6F8FA")));
        headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, ProductBrush("#D9E2E8")));
        headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        table.ColumnHeaderStyle = headerStyle;

        if (table.Columns.Count < 6)
            return;

        table.Columns[0].Header = "Use";
        table.Columns[1].Header = "Signal";
        table.Columns[2].Header = "RMS";
        table.Columns[3].Header = "Angle";
        table.Columns[4].Header = "Unit";
        table.Columns[5].Header = "Origin";

        if (table.Columns[0] is DataGridCheckBoxColumn enabledColumn)
        {
            var checkStyle = new Style(typeof(CheckBox));
            checkStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            checkStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            enabledColumn.ElementStyle = checkStyle;
            enabledColumn.EditingElementStyle = checkStyle;
            enabledColumn.CellStyle = ProductCenteredCellStyle(cellStyle, zeroPadding: true);
        }

        ProductApplyTextColumn(table.Columns[1], TextAlignment.Left, cellStyle);
        ProductApplyTextColumn(table.Columns[2], TextAlignment.Right, cellStyle);
        ProductApplyTextColumn(table.Columns[3], TextAlignment.Right, cellStyle);
        ProductApplyTextColumn(table.Columns[4], TextAlignment.Center, cellStyle, centerCell: true);
        ProductApplyTextColumn(table.Columns[5], TextAlignment.Left, cellStyle);
    }

    private void ProductCleanInjectionFooter(StackPanel actions)
    {
        if (_virtualInjectionView is null || _advancedInjectionButton is null)
            return;

        RemoveProductButton(_virtualInjectionView, "Reset relay");

        if (_virtualInjectionView.RowDefinitions.Count >= 3)
            _virtualInjectionView.RowDefinitions[2].Height = new GridLength(38);

        // Formula/provenance text duplicated behavior already evident in the grid
        // and consumed the exact width needed by the source actions. CT state now
        // occupies this quiet footer position through CtObservability.
        if (_virtualInjectionProvenanceText is not null)
        {
            _virtualInjectionProvenanceText.Text = string.Empty;
            _virtualInjectionProvenanceText.Visibility = Visibility.Collapsed;
            _virtualInjectionProvenanceText.Margin = new Thickness(0);
        }

        _advancedInjectionButton.Content = "Details…";
        _advancedInjectionButton.ToolTip = "Open detailed source sequencing and engineering controls.";
        _advancedInjectionButton.Height = 28;
        _advancedInjectionButton.MinHeight = 28;
        _advancedInjectionButton.MaxHeight = 28;
        _advancedInjectionButton.MinWidth = 72;
        _advancedInjectionButton.Margin = new Thickness(0, 0, 5, 0);
        _advancedInjectionButton.Padding = new Thickness(8, 0, 8, 0);
        _advancedInjectionButton.HorizontalAlignment = HorizontalAlignment.Right;
        actions.Children.Add(_advancedInjectionButton);

        foreach (var button in actions.Children.OfType<Button>())
        {
            button.Height = 28;
            button.MinHeight = 28;
            button.MaxHeight = 28;
            button.Padding = new Thickness(8, 0, 8, 0);
            button.FontSize = 9.2;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }

        InjectFaultButton.Visibility = Visibility.Collapsed;

        if (DegradeSmvButton.Parent is Panel degradeParent)
            degradeParent.Children.Remove(DegradeSmvButton);
        ProductSetButtonText(DegradeSmvButton, "SMV quality…");
        DegradeSmvButton.ToolTip = "Simulate degraded SMV quality for protection trust testing.";
        DegradeSmvButton.Margin = new Thickness(0);
        DegradeSmvButton.MinWidth = 92;
        DegradeSmvButton.Height = 28;
        DegradeSmvButton.MinHeight = 28;
        DegradeSmvButton.MaxHeight = 28;
        actions.Children.Add(DegradeSmvButton);
    }

    private void ProductMakeWaveformViewerReadOnly()
    {
        InjectFaultButton.Visibility = Visibility.Collapsed;

        if (FrequencyText.Parent is Grid waveformFooterGrid &&
            waveformFooterGrid.Parent is Border waveformFooter)
        {
            RemoveProductButton(waveformFooter, "Reset");
        }
    }

    private void ProductWireQuietStatusBadge()
    {
        if (_virtualInjectionStatusText is null || _virtualInjectionStatusBadge is null)
            return;

        _productInjectionStatusDescriptor = DependencyPropertyDescriptor.FromProperty(
            TextBlock.TextProperty,
            typeof(TextBlock));
        _productInjectionStatusDescriptor?.AddValueChanged(
            _virtualInjectionStatusText,
            ProductInjectionStatusChanged);
        RefreshProductInjectionStatusBadge();
    }

    private void ProductInjectionStatusChanged(object? sender, EventArgs e)
        => RefreshProductInjectionStatusBadge();

    private void RefreshProductInjectionStatusBadge()
    {
        if (_virtualInjectionStatusText is null || _virtualInjectionStatusBadge is null)
            return;

        var state = _virtualInjectionStatusText.Text ?? string.Empty;
        var quietSteadyState =
            state.Equals("READY", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("RUNNING", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("STOPPED", StringComparison.OrdinalIgnoreCase);

        _virtualInjectionStatusBadge.Visibility = quietSteadyState
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ProductWireAnalysisChrome()
    {
        foreach (var button in _analysisModeButtons.Values)
            button.Click += ProductAnalysisModeChanged;
    }

    private void ProductAnalysisModeChanged(object sender, RoutedEventArgs e)
        => Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(RefreshProductReadyAnalysisChrome));

    private void RefreshProductReadyAnalysisChrome()
    {
        if (_analysisBadgeText?.Parent is Border analysisBadge)
        {
            analysisBadge.Visibility = _analysisWorkspaceMode == AnalysisWorkspaceMode.Injection
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // Start/Stop is the one lifecycle indicator. Repeating RUNNING beside the
        // title adds visual noise and made the header look cramped.
        StreamHealthText.Visibility = Visibility.Collapsed;
        StreamHealthText.MaxWidth = 0;
    }

    private void ProductReadySourceChanged(object sender, SelectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                PlaceRunButtonForActiveSource();
                RefreshProductReadyAnalysisChrome();
            }));
    }

    private void PlaceRunButtonForActiveSource()
    {
        if (!_productReadyInjectionUxInitialized ||
            _productTopSourceActions is null ||
            _productInjectionToolbar is null)
            return;

        if (RunButton.Parent is Panel currentParent)
            currentParent.Children.Remove(RunButton);

        if (SourceCombo.SelectedIndex == 0)
        {
            // Internal source lifecycle belongs to the analysis/source header. It
            // remains available in both WAVEFORM and INJECTION without consuming
            // width in the injection parameter row.
            if (!TryGetProductAnalysisControlsLine(out var controlsLine))
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.ContextIdle,
                    new Action(PlaceRunButtonForActiveSource));
                return;
            }

            RunButton.Height = 32;
            RunButton.MinHeight = 32;
            RunButton.MaxHeight = 32;
            RunButton.MinWidth = 112;
            RunButton.Padding = new Thickness(11, 0, 11, 0);
            RunButton.Margin = new Thickness(8, 0, 0, 0);
            RunButton.ToolTip = "Start or stop virtual injection. Relay state is not reset.";
            RunButton.HorizontalAlignment = HorizontalAlignment.Left;
            RunButton.VerticalAlignment = VerticalAlignment.Center;

            var insertAt = _phasorQuantityCombo is not null &&
                           ReferenceEquals(_phasorQuantityCombo.Parent, controlsLine)
                ? controlsLine.Children.IndexOf(_phasorQuantityCombo) + 1
                : controlsLine.Children.Count;
            controlsLine.Children.Insert(Math.Clamp(insertAt, 0, controlsLine.Children.Count), RunButton);
        }
        else
        {
            RunButton.Height = 34;
            RunButton.MinHeight = 34;
            RunButton.MaxHeight = 34;
            RunButton.MinWidth = 116;
            RunButton.Padding = new Thickness(13, 0, 13, 0);
            RunButton.Margin = new Thickness(2, 0, 0, 0);
            RunButton.ToolTip = "Start or stop the selected live/replay source.";
            _productTopSourceActions.Children.Add(RunButton);
        }

        RefreshProductRunButtonVisibility();
    }

    private bool TryGetProductAnalysisControlsLine(out StackPanel controlsLine)
    {
        controlsLine = null!;
        var analysisButton = _analysisModeButtons.Values.FirstOrDefault();
        if (analysisButton?.Parent is not StackPanel viewPanel ||
            viewPanel.Parent is not Border viewGroup ||
            viewGroup.Parent is not StackPanel line)
        {
            return false;
        }

        controlsLine = line;
        return true;
    }

    private void ProductReadyRunButton_Loaded(object sender, RoutedEventArgs e)
        => RefreshProductRunButtonVisibility();

    private void RefreshProductRunButtonVisibility()
    {
        if (SourceCombo.SelectedIndex != 0)
        {
            RunButton.Visibility = Visibility.Visible;
            return;
        }

        RunButton.Visibility = Window.GetWindow(RunButton) is AdvancedInjectionWindow
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ProductReadyInjectionUx_Closed(object? sender, EventArgs e)
    {
        SourceCombo.SelectionChanged -= ProductReadySourceChanged;
        RunButton.Loaded -= ProductReadyRunButton_Loaded;
        foreach (var button in _analysisModeButtons.Values)
            button.Click -= ProductAnalysisModeChanged;

        if (_virtualInjectionStatusText is not null && _productInjectionStatusDescriptor is not null)
        {
            _productInjectionStatusDescriptor.RemoveValueChanged(
                _virtualInjectionStatusText,
                ProductInjectionStatusChanged);
        }
        _productInjectionStatusDescriptor = null;
        Closed -= ProductReadyInjectionUx_Closed;
    }

    private static void ProductApplyTextColumn(
        DataGridColumn column,
        TextAlignment alignment,
        Style baseCellStyle,
        bool centerCell = false)
    {
        if (column is not DataGridTextColumn textColumn)
            return;

        var elementStyle = new Style(typeof(TextBlock));
        elementStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        elementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, alignment));
        elementStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        textColumn.ElementStyle = elementStyle;

        var editingStyle = new Style(typeof(TextBox));
        editingStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        editingStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, alignment));
        editingStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        editingStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        textColumn.EditingElementStyle = editingStyle;

        if (centerCell)
            column.CellStyle = ProductCenteredCellStyle(baseCellStyle, zeroPadding: false);
    }

    private static Style ProductCenteredCellStyle(Style baseStyle, bool zeroPadding)
    {
        var style = new Style(typeof(DataGridCell), baseStyle);
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        if (zeroPadding)
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        return style;
    }

    private static Button? FindProductButton(DependencyObject root, string label)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button button &&
                string.Equals(ProductButtonLabel(button), label, StringComparison.OrdinalIgnoreCase))
                return button;

            var nested = FindProductButton(child, label);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private static bool RemoveProductButton(DependencyObject root, string label)
    {
        var button = FindProductButton(root, label);
        if (button?.Parent is not Panel parent)
            return false;
        parent.Children.Remove(button);
        return true;
    }

    private static string ProductButtonLabel(Button button)
        => ProductElementText(button.Content).Trim();

    private static string ProductElementText(object? content)
    {
        return content switch
        {
            string text => text,
            TextBlock textBlock => textBlock.Text,
            ContentControl control => ProductElementText(control.Content),
            Panel panel => string.Join(
                " ",
                panel.Children
                    .OfType<UIElement>()
                    .Select(ProductElementText)
                    .Where(text => !string.IsNullOrWhiteSpace(text))),
            UIElement element => ProductElementText(element),
            _ => string.Empty
        };
    }

    private static string ProductElementText(UIElement element)
    {
        return element switch
        {
            TextBlock textBlock => textBlock.Text,
            ContentControl control => ProductElementText(control.Content),
            Panel panel => string.Join(
                " ",
                panel.Children
                    .OfType<UIElement>()
                    .Select(ProductElementText)
                    .Where(text => !string.IsNullOrWhiteSpace(text))),
            _ => string.Empty
        };
    }

    private static void ProductSetButtonText(Button button, string text)
    {
        if (button.Content is string)
        {
            button.Content = text;
            return;
        }

        if (button.Content is Panel panel)
        {
            var textBlock = panel.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBlock is not null)
                textBlock.Text = text;
        }
    }

    private static Brush ProductBrush(string hex)
        => (Brush)new BrushConverter().ConvertFromString(hex)!;
}