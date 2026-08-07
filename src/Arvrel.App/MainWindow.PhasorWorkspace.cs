using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private enum AnalysisWorkspaceMode
    {
        Injection,
        Waveform,
        // Kept as compatibility aliases for older lifecycle calls. The product
        // surface now intentionally exposes only Waveform and Injection.
        Dual,
        Phasor
    }

    private readonly Dictionary<AnalysisWorkspaceMode, Button> _analysisModeButtons = new();
    private bool _phasorWorkspaceInitialized;
    private Grid? _analysisHost;
    private PhasorScope? _phasorScope;
    private ComboBox? _phasorQuantityCombo;
    private DispatcherTimer? _phasorRefreshTimer;
    private AnalysisWorkspaceMode _analysisWorkspaceMode = AnalysisWorkspaceMode.Waveform;
    private PhasorDisplayMode _phasorDisplayMode = PhasorDisplayMode.Current;
    private TextBlock? _analysisTitleText;
    private TextBlock? _analysisBadgeText;
    private string? _lastPhasorPresentationSignature;
    private TranslateTransform? _injectionDrawerTransform;

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
        Panel.SetZIndex(SmvScope, 0);
        _analysisHost.Children.Add(SmvScope);

        _phasorScope = new PhasorScope
        {
            MinWidth = 210,
            Frame = PhasorDisplayFrame.Unavailable(
                _phasorDisplayMode,
                "Waiting for a complete 4I+4V one-cycle phasor window.")
        };
        Grid.SetColumn(_phasorScope, 2);
        Panel.SetZIndex(_phasorScope, 0);
        _analysisHost.Children.Add(_phasorScope);

        InitializeVirtualInjectionEditor();
        if (_virtualInjectionView is not null)
        {
            _injectionDrawerTransform = new TranslateTransform();
            _virtualInjectionView.RenderTransform = _injectionDrawerTransform;
            _virtualInjectionView.RenderTransformOrigin = new Point(0, 0.5);
            Panel.SetZIndex(_virtualInjectionView, 4);
        }

        InstallAnalysisHeaderControls();
        UpdateAnalysisSourceMode(announce: false);

        // Ten frames per second is sufficient for an engineering instrument.
        // More importantly, RefreshPhasorFrame only assigns a new frame when
        // the projected vectors or status actually change.
        _phasorRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
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

        _analysisTitleText = titleLine.Children.OfType<TextBlock>().FirstOrDefault();
        _analysisBadgeText = titleLine.Children
            .OfType<Border>()
            .Select(border => border.Child)
            .OfType<TextBlock>()
            .FirstOrDefault();

        var measurementSummary = headerGrid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(child => !ReferenceEquals(child, titleBlock));
        if (measurementSummary is null)
            return;

        // Three zones only: title, compact view/source controls, measurements.
        // The previous four-mode tab strip consumed enough width to clip the
        // first control at common laptop/DPI sizes.
        headerGrid.ColumnDefinitions.Clear();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
            MinWidth = 360
        });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(titleBlock, 0);
        titleBlock.Width = 210;
        titleBlock.VerticalAlignment = VerticalAlignment.Center;
        titleBlock.HorizontalAlignment = HorizontalAlignment.Left;

        titleLine.VerticalAlignment = VerticalAlignment.Center;
        foreach (var child in titleLine.Children.OfType<FrameworkElement>())
            child.VerticalAlignment = VerticalAlignment.Center;

        StreamHealthText.MaxWidth = 72;
        StreamHealthText.TextTrimming = TextTrimming.CharacterEllipsis;
        StreamHealthText.VerticalAlignment = VerticalAlignment.Center;

        var subtitle = titleBlock.Children.OfType<TextBlock>().FirstOrDefault();
        if (subtitle is not null)
        {
            subtitle.Width = 210;
            subtitle.Margin = new Thickness(0, 3, 0, 0);
            subtitle.VerticalAlignment = VerticalAlignment.Center;
            subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
            subtitle.TextWrapping = TextWrapping.NoWrap;
        }

        Grid.SetColumn(measurementSummary, 2);
        measurementSummary.VerticalAlignment = VerticalAlignment.Center;
        measurementSummary.HorizontalAlignment = HorizontalAlignment.Right;
        measurementSummary.Margin = new Thickness(10, 0, 0, 0);

        var measurementGroups = measurementSummary.Children.OfType<StackPanel>().ToArray();
        for (var index = 0; index < measurementGroups.Length; index++)
        {
            var group = measurementGroups[index];
            group.VerticalAlignment = VerticalAlignment.Center;
            group.HorizontalAlignment = HorizontalAlignment.Left;
            group.MinWidth = index == measurementGroups.Length - 1 ? 60 : 45;
            group.Margin = new Thickness(0, 0, index == measurementGroups.Length - 1 ? 0 : 9, 0);

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
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0)
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

        // Phasor is now permanent on the right. The only user decision is what
        // occupies the large left pane: waveform evidence or the injection drawer.
        AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Waveform, "WAVEFORM");
        AddAnalysisModeButton(viewPanel, AnalysisWorkspaceMode.Injection, "INJECTION");

        _phasorQuantityCombo = new ComboBox
        {
            Width = 104,
            Height = 32,
            MinHeight = 32,
            MaxHeight = 32,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(8, 0, 8, 0),
            FontSize = 10.2,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            ToolTip = "Select the quantity shown by the phasor panel on the right"
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
            _lastPhasorPresentationSignature = null;
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
            MinWidth = mode == AnalysisWorkspaceMode.Injection ? 76 : 78,
            Height = 26,
            MinHeight = 26,
            MaxHeight = 26,
            Padding = new Thickness(9, 0, 9, 0),
            Margin = new Thickness(0),
            BorderThickness = new Thickness(0),
            FontSize = 9.1,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = mode == AnalysisWorkspaceMode.Injection
                ? "Slide in the virtual injection editor; live phasor remains visible on the right"
                : "Show waveform evidence; live phasor remains visible on the right"
        };
        button.Click += (_, _) => ApplyAnalysisWorkspaceMode(mode, announce: true);
        parent.Children.Add(button);
        _analysisModeButtons[mode] = button;
    }

    private void UpdateAnalysisSourceMode(bool announce)
    {
        if (!_phasorWorkspaceInitialized)
            return;

        var internalMode = SourceCombo.SelectedIndex == 0;
        var injectionAvailable = internalMode && !IsAdvancedInjectionOpen;
        if (_analysisModeButtons.TryGetValue(AnalysisWorkspaceMode.Injection, out var injectionButton))
        {
            var desiredVisibility = injectionAvailable ? Visibility.Visible : Visibility.Collapsed;
            if (injectionButton.Visibility != desiredVisibility)
                injectionButton.Visibility = desiredVisibility;
        }

        if (!injectionAvailable && _analysisWorkspaceMode == AnalysisWorkspaceMode.Injection)
            ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Waveform, announce);
        else if (internalMode)
            ApplyAnalysisWorkspaceMode(_analysisWorkspaceMode, announce);
        else
            ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode.Waveform, announce: false);
    }

    private void ApplyAnalysisWorkspaceMode(AnalysisWorkspaceMode mode, bool announce)
    {
        if (_analysisHost is null || _phasorScope is null)
            return;

        // Legacy callers still request DUAL/PHASOR. Product UX intentionally
        // normalizes both to WAVEFORM because the phasor is permanently visible.
        mode = mode == AnalysisWorkspaceMode.Injection
            ? AnalysisWorkspaceMode.Injection
            : AnalysisWorkspaceMode.Waveform;

        var injectionAvailable = SourceCombo.SelectedIndex == 0 && !IsAdvancedInjectionOpen;
        if (mode == AnalysisWorkspaceMode.Injection && !injectionAvailable)
            mode = AnalysisWorkspaceMode.Waveform;

        var previousMode = _analysisWorkspaceMode;
        _analysisWorkspaceMode = mode;

        // Permanent analysis geometry: large evidence/source pane on the left,
        // narrow gap, compact live phasor on the right.
        var columns = _analysisHost.ColumnDefinitions;
        columns[0].Width = new GridLength(2.35, GridUnitType.Star);
        columns[1].Width = new GridLength(9);
        columns[2].Width = new GridLength(0.85, GridUnitType.Star);
        SmvScope.Visibility = Visibility.Visible;
        _phasorScope.Visibility = Visibility.Visible;

        var editorIsInMainWorkspace = _virtualInjectionView is not null &&
                                      ReferenceEquals(_virtualInjectionView.Parent, _analysisHost);
        if (editorIsInMainWorkspace)
        {
            var showDrawer = mode == AnalysisWorkspaceMode.Injection;
            SetInjectionDrawerVisible(
                showDrawer,
                animate: previousMode != mode && IsVisible);
        }

        var accent = new SolidColorBrush(Color.FromRgb(46, 111, 158));
        var accentSoft = new SolidColorBrush(Color.FromRgb(232, 241, 247));
        var inactiveText = new SolidColorBrush(Color.FromRgb(101, 117, 134));
        foreach (var pair in _analysisModeButtons)
        {
            var selected = pair.Key == mode;
            var background = selected ? accentSoft : Brushes.Transparent;
            var foreground = selected ? accent : inactiveText;
            if (!Equals(pair.Value.Background, background))
                pair.Value.Background = background;
            if (!Equals(pair.Value.Foreground, foreground))
                pair.Value.Foreground = foreground;
        }

        UpdateAnalysisHeader(mode);
        if (announce)
        {
            StatusText.Text = mode == AnalysisWorkspaceMode.Injection
                ? "Analysis view · Injection editor + live phasor."
                : "Analysis view · Waveform + live phasor.";
        }
    }

    private void SetInjectionDrawerVisible(bool show, bool animate)
    {
        if (_analysisHost is null ||
            _virtualInjectionView is null ||
            _injectionDrawerTransform is null ||
            !ReferenceEquals(_virtualInjectionView.Parent, _analysisHost))
            return;

        var drawerWidth = Math.Max(480, _analysisHost.ColumnDefinitions[0].ActualWidth);
        _injectionDrawerTransform.BeginAnimation(TranslateTransform.XProperty, null);

        if (!animate)
        {
            _injectionDrawerTransform.X = show ? 0 : -drawerWidth;
            _virtualInjectionView.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        if (show)
        {
            _virtualInjectionView.Visibility = Visibility.Visible;
            _injectionDrawerTransform.X = -drawerWidth;
            var slideIn = new DoubleAnimation
            {
                From = -drawerWidth,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            slideIn.Completed += (_, _) =>
            {
                if (_injectionDrawerTransform is null)
                    return;
                _injectionDrawerTransform.BeginAnimation(TranslateTransform.XProperty, null);
                _injectionDrawerTransform.X = 0;
                if (_analysisWorkspaceMode != AnalysisWorkspaceMode.Injection &&
                    _virtualInjectionView is not null)
                    _virtualInjectionView.Visibility = Visibility.Collapsed;
            };
            _injectionDrawerTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            return;
        }

        if (_virtualInjectionView.Visibility != Visibility.Visible)
        {
            _injectionDrawerTransform.X = -drawerWidth;
            return;
        }

        var slideOut = new DoubleAnimation
        {
            From = _injectionDrawerTransform.X,
            To = -drawerWidth,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop
        };
        slideOut.Completed += (_, _) =>
        {
            if (_injectionDrawerTransform is null || _virtualInjectionView is null)
                return;
            _injectionDrawerTransform.BeginAnimation(TranslateTransform.XProperty, null);
            if (_analysisWorkspaceMode == AnalysisWorkspaceMode.Injection)
            {
                _injectionDrawerTransform.X = 0;
                _virtualInjectionView.Visibility = Visibility.Visible;
            }
            else
            {
                _injectionDrawerTransform.X = -drawerWidth;
                _virtualInjectionView.Visibility = Visibility.Collapsed;
            }
        };
        _injectionDrawerTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
    }

    private void UpdateAnalysisHeader(AnalysisWorkspaceMode mode)
    {
        if (_analysisTitleText is not null)
            SetTextIfChanged(
                _analysisTitleText,
                mode == AnalysisWorkspaceMode.Injection ? "Virtual injection" : "SMV waveform");

        if (_analysisBadgeText is not null)
            SetTextIfChanged(
                _analysisBadgeText,
                mode == AnalysisWorkspaceMode.Injection ? "SOURCE" : "2 CYCLES");
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

        var frame = PhasorDisplayProjector.Project(phasors, _phasorDisplayMode);
        var signature = BuildPhasorPresentationSignature(frame);
        if (!string.Equals(_lastPhasorPresentationSignature, signature, StringComparison.Ordinal))
        {
            _phasorScope.Frame = frame;
            _lastPhasorPresentationSignature = signature;
        }

        var toolTip = frame.IsAvailable
            ? $"{frame.ReferenceLabel} · {frame.Status}"
            : frame.Status;
        if (_phasorQuantityCombo is not null && !Equals(_phasorQuantityCombo.ToolTip, toolTip))
            _phasorQuantityCombo.ToolTip = toolTip;
    }

    private static string BuildPhasorPresentationSignature(PhasorDisplayFrame frame)
    {
        var vectors = string.Join(
            ";",
            frame.Vectors.Select(vector => FormattableString.Invariant(
                $"{vector.Key}:{vector.Magnitude:R}:{vector.AngleDegrees:R}:{vector.QuantityKind}:{vector.IsResidual}:{vector.IsNegativeSequence}")));
        return FormattableString.Invariant(
            $"{frame.Mode}|{frame.IsAvailable}|{frame.ReferenceLabel}|{frame.Status}|{frame.MaximumCurrent:R}|{frame.MaximumVoltage:R}|{frame.PositiveSequenceAngleDifferenceDegrees:R}|{vectors}");
    }
}
