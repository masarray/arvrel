using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private enum AnalysisWorkspaceMode
    {
        Waveform,
        Dual,
        Phasor
    }

    private readonly Dictionary<AnalysisWorkspaceMode, Button> _analysisModeButtons = new();
    private bool _phasorWorkspaceInitialized;
    private Grid? _analysisHost;
    private PhasorScope? _phasorScope;
    private ComboBox? _phasorQuantityCombo;
    private DispatcherTimer? _phasorRefreshTimer;
    private AnalysisWorkspaceMode _analysisWorkspaceMode = AnalysisWorkspaceMode.Dual;
    private PhasorDisplayMode _phasorDisplayMode = PhasorDisplayMode.Current;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        InitializePhasorWorkspace();
    }

    private void InitializePhasorWorkspace()
    {
        if (_phasorWorkspaceInitialized)
            return;
        _phasorWorkspaceInitialized = true;

        if (SmvScope.Parent is not Grid waveformPanel)
            return;

        var originalMargin = SmvScope.Margin;
        waveformPanel.Children.Remove(SmvScope);

        _analysisHost = new Grid
        {
            Margin = originalMargin,
            ClipToBounds = true
        };
        _analysisHost.ColumnDefinitions.Add(new ColumnDefinition());
        _analysisHost.ColumnDefinitions.Add(new ColumnDefinition());
        _analysisHost.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetRow(_analysisHost, 1);
        waveformPanel.Children.Add(_analysisHost);

        SmvScope.Margin = new Thickness(0);
        Grid.SetColumn(SmvScope, 0);
        _analysisHost.Children.Add(SmvScope);

        _phasorScope = new PhasorScope
        {
            MinWidth = 210,
            Frame = PhasorDisplayFrame.Unavailable(
                _phasorDisplayMode,
                "Waiting for a complete 4I+4V one-cycle phasor window.")
        };
        Grid.SetColumn(_phasorScope, 2);
        _analysisHost.Children.Add(_phasorScope);

        InstallAnalysisHeaderControls();
        ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Dual, announce: false);

        _phasorRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _phasorRefreshTimer.Tick += (_, _) => RefreshPhasorFrame();
        _phasorRefreshTimer.Start();
        Closed += (_, _) => _phasorRefreshTimer?.Stop();
        RefreshPhasorFrame();
    }

    private void InstallAnalysisHeaderControls()
    {
        if (StreamHealthText.Parent is not StackPanel titleLine ||
            titleLine.Parent is not StackPanel titleBlock ||
            titleBlock.Parent is not Grid headerGrid)
            return;

        var measurementSummary = headerGrid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(child => !ReferenceEquals(child, titleBlock));
        if (measurementSummary is null)
            return;

        // Rebuild the header into three deliberate zones. The earlier implementation
        // appended controls to the title line, which made the title, stream state,
        // segmented buttons, combo box and measurements compete for one baseline.
        headerGrid.ColumnDefinitions.Clear();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
            MinWidth = 238
        });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(titleBlock, 0);
        titleBlock.Width = 232;
        titleBlock.VerticalAlignment = VerticalAlignment.Center;
        titleBlock.HorizontalAlignment = HorizontalAlignment.Left;

        titleLine.VerticalAlignment = VerticalAlignment.Center;
        foreach (var child in titleLine.Children.OfType<FrameworkElement>())
            child.VerticalAlignment = VerticalAlignment.Center;

        StreamHealthText.MaxWidth = 92;
        StreamHealthText.TextTrimming = TextTrimming.CharacterEllipsis;
        StreamHealthText.VerticalAlignment = VerticalAlignment.Center;

        var subtitle = titleBlock.Children.OfType<TextBlock>().FirstOrDefault();
        if (subtitle is not null)
        {
            subtitle.Width = 232;
            subtitle.Margin = new Thickness(0, 3, 0, 0);
            subtitle.VerticalAlignment = VerticalAlignment.Center;
            subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
            subtitle.TextWrapping = TextWrapping.NoWrap;
        }

        Grid.SetColumn(measurementSummary, 2);
        measurementSummary.VerticalAlignment = VerticalAlignment.Center;
        measurementSummary.HorizontalAlignment = HorizontalAlignment.Right;
        measurementSummary.Margin = new Thickness(14, 0, 0, 0);

        var measurementGroups = measurementSummary.Children.OfType<StackPanel>().ToArray();
        for (var index = 0; index < measurementGroups.Length; index++)
        {
            var group = measurementGroups[index];
            group.VerticalAlignment = VerticalAlignment.Center;
            group.HorizontalAlignment = HorizontalAlignment.Left;
            group.MinWidth = index == measurementGroups.Length - 1 ? 62 : 47;
            group.Margin = new Thickness(0, 0, index == measurementGroups.Length - 1 ? 0 : 11, 0);

            foreach (var text in group.Children.OfType<TextBlock>())
            {
                text.VerticalAlignment = VerticalAlignment.Center;
                text.HorizontalAlignment = HorizontalAlignment.Left;
                text.TextTrimming = TextTrimming.CharacterEllipsis;
            }
        }

        var controlsLine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };
        Grid.SetColumn(controlsLine, 1);
        headerGrid.Children.Add(controlsLine);

        var viewGroup = new Border
        {
            Height = 32,
            Background = FindResource("PanelBrush") as Brush,
            BorderBrush = FindResource("LineBrush") as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var viewPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        viewGroup.Child = viewPanel;
        controlsLine.Children.Add(viewGroup);

        AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Waveform, "WAVE");
        AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Dual, "DUAL");
        AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Phasor, "PHASOR");

        _phasorQuantityCombo = new ComboBox
        {
            Width = 108,
            Height = 32,
            MinHeight = 32,
            MaxHeight = 32,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(8, 0, 8, 0),
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            ToolTip = "Select current, voltage, or symmetrical-component phasors"
        };
        _phasorQuantityCombo.Items.Add("Current I");
        _phasorQuantityCombo.Items.Add("Voltage U");
        _phasorQuantityCombo.Items.Add("Sequence");
        _phasorQuantityCombo.SelectedIndex = 0;
        _phasorQuantityCombo.SelectionChanged += (_, _) =>
        {
            _phasorDisplayMode = _phasorQuantityCombo.SelectedIndex switch
            {
                1 => PhasorDisplayMode.Voltage,
                2 => PhasorDisplayMode.Sequence,
                _ => PhasorDisplayMode.Current
            };
            RefreshPhasorFrame();
        };
        controlsLine.Children.Add(_phasorQuantityCombo);
    }

    private void AddAnalysisModeButton(Panel parent, AnalysisWorkspaceMode mode, string label)
    {
        var button = new Button
        {
            Style = (Style)FindResource("CompactButton"),
            Content = label,
            MinWidth = mode == AnalysisWorkspaceMode.Phasor ? 54 : 42,
            Height = 26,
            MinHeight = 26,
            MaxHeight = 26,
            Padding = new Thickness(7, 0, 7, 0),
            Margin = new Thickness(0),
            BorderThickness = new Thickness(0),
            FontSize = 9.1,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = mode switch
            {
                AnalysisWorkspaceMode.Waveform => "Use the full analysis area for the stationary waveform",
                AnalysisWorkspaceMode.Dual => "Show waveform and compact phasor together",
                _ => "Use the full analysis area for the phasor diagram"
            }
        };
        button.Click += (_, _) => ApplyAnalysisWorkspaceMode(mode, announce: true);
        parent.Children.Add(button);
        _analysisModeButtons[mode] = button;
    }

    private void ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode mode, bool announce)
    {
        if (_analysisHost is null || _phasorScope is null)
            return;

        _analysisWorkspaceMode = mode;
        var columns = _analysisHost.ColumnDefinitions;
        switch (mode)
        {
            case AnalysisWorkspaceMode.Waveform:
                columns[0].Width = new GridLength(1, GridUnitType.Star);
                columns[1].Width = new GridLength(0);
                columns[2].Width = new GridLength(0);
                SmvScope.Visibility = Visibility.Visible;
                _phasorScope.Visibility = Visibility.Collapsed;
                break;

            case AnalysisWorkspaceMode.Phasor:
                columns[0].Width = new GridLength(0);
                columns[1].Width = new GridLength(0);
                columns[2].Width = new GridLength(1, GridUnitType.Star);
                SmvScope.Visibility = Visibility.Collapsed;
                _phasorScope.Visibility = Visibility.Visible;
                break;

            default:
                columns[0].Width = new GridLength(2.35, GridUnitType.Star);
                columns[1].Width = new GridLength(9);
                columns[2].Width = new GridLength(0.85, GridUnitType.Star);
                SmvScope.Visibility = Visibility.Visible;
                _phasorScope.Visibility = Visibility.Visible;
                break;
        }

        var accent = new SolidColorBrush(Color.FromRgb(46, 111, 158));
        var accentSoft = new SolidColorBrush(Color.FromRgb(232, 241, 247));
        var inactiveText = new SolidColorBrush(Color.FromRgb(101, 117, 134));
        foreach (var pair in _analysisModeButtons)
        {
            var selected = pair.Key == mode;
            pair.Value.Background = selected ? accentSoft : Brushes.Transparent;
            pair.Value.Foreground = selected ? accent : inactiveText;
        }

        if (announce)
        {
            var description = mode switch
            {
                AnalysisWorkspaceMode.Waveform => "Waveform focus",
                AnalysisWorkspaceMode.Phasor => "Phasor focus",
                _ => "Waveform + compact phasor"
            };
            StatusText.Text = $"Analysis view · {description}. Phasor reference follows VA = 0° when voltage is available.";
        }
    }

    private void RefreshPhasorFrame()
    {
        if (_phasorScope is null)
            return;

        PhasorMeasurementSet? phasors;
        try
        {
            if (SourceCombo.SelectedIndex == 0)
            {
                phasors = _scenario.Advance(TimeSpan.Zero, _pickupPosition, _tripPosition).Measurement.Phasors;
            }
            else
            {
                phasors = _processBus
                    .GetSnapshot(_selectedStreamKey, ViewCombo.SelectedIndex == 1)
                    .Measurement
                    .Phasors;
            }
        }
        catch (InvalidOperationException)
        {
            phasors = null;
        }

        _phasorScope.Frame = PhasorDisplayProjector.Project(phasors, _phasorDisplayMode);
        _phasorQuantityCombo?.SetCurrentValue(
            ToolTipProperty,
            _phasorScope.Frame.IsAvailable
                ? $"{_phasorScope.Frame.ReferenceLabel} · {_phasorScope.Frame.Status}"
                : _phasorScope.Frame.Status);
    }
}
