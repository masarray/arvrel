using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private readonly ObservableCollection<TransformerInjectionRow> _transformerInjectionRows = new();
    private TransformerVirtualInjectionRuntime? _transformerVirtualInjectionRuntime;
    private Grid? _transformerInjectionView;
    private ComboBox? _transformerInjectionPresetCombo;
    private ComboBox? _transformerInjectionDisplaySideCombo;
    private TextBox? _transformerInjectionFrequencyText;
    private TextBlock? _transformerInjectionStatusText;
    private DispatcherTimer? _transformerInjectionApplyTimer;
    private DispatcherTimer? _transformerInjectionRuntimeTimer;
    private bool _transformerInjectionInitialized;
    private bool _transformerInjectionSync;
    private DateTimeOffset _transformerInjectionLastAdvance = DateTimeOffset.UtcNow;

    private bool IsTransformerInternalInjectionActive =>
        _transformerOcrWorkspaceMounted && SourceCombo.SelectedIndex == 0;

    private bool IsActiveVirtualInjectionRunning => IsTransformerInternalInjectionActive
        ? _transformerVirtualInjectionRuntime?.IsRunning == true
        : _scenario.IsRunning;

    private void InitializeTransformerVirtualInjection()
    {
        if (_transformerInjectionInitialized || _analysisHost is null)
            return;

        _transformerInjectionInitialized = true;
        _transformerVirtualInjectionRuntime = new TransformerVirtualInjectionRuntime();
        _transformerVirtualInjectionRuntime.SnapshotChanged += TransformerVirtualInjectionRuntime_SnapshotChanged;

        foreach (var (side, channel, isNeutral) in TransformerInjectionRow.StandardRows)
        {
            var row = new TransformerInjectionRow(side, channel, isNeutral);
            row.PropertyChanged += TransformerInjectionRow_PropertyChanged;
            _transformerInjectionRows.Add(row);
        }

        _transformerInjectionView = BuildTransformerInjectionView();
        _transformerInjectionView.Visibility = Visibility.Collapsed;
        Grid.SetColumn(_transformerInjectionView, 0);
        Panel.SetZIndex(_transformerInjectionView, 6);
        _analysisHost.Children.Add(_transformerInjectionView);

        _transformerInjectionApplyTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _transformerInjectionApplyTimer.Tick += (_, _) =>
        {
            _transformerInjectionApplyTimer.Stop();
            _ = TryApplyTransformerInjectionEditor(announce: false);
        };

        _transformerInjectionRuntimeTimer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        _transformerInjectionRuntimeTimer.Tick += TransformerInjectionRuntimeTimer_Tick;
        _transformerInjectionRuntimeTimer.Start();

        SyncTransformerInjectionEditor(_transformerVirtualInjectionRuntime.ActiveProfile);
        RenderTransformerInjectionAnalysis();
        Closed += (_, _) =>
        {
            _transformerInjectionApplyTimer?.Stop();
            _transformerInjectionRuntimeTimer?.Stop();
        };
    }

    private Grid BuildTransformerInjectionView()
    {
        var root = new Grid
        {
            Background = FindResource("PanelBrush") as Brush,
            ClipToBounds = true
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(43) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(37) });

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(184) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        toolbar.Children.Add(new TextBlock
        {
            Text = "TEST VECTOR",
            Style = FindResource("SectionLabel") as Style,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        _transformerInjectionPresetCombo = new ComboBox
        {
            Height = 32,
            ItemsSource = new[] { "Balanced through load", "Internal A fault", "REF HV / NGR", "REF LV / NGR" },
            SelectedIndex = 0,
            ToolTip = "Load an editable two-sided transformer current test vector."
        };
        _transformerInjectionPresetCombo.SelectionChanged += TransformerInjectionPresetCombo_SelectionChanged;
        Grid.SetColumn(_transformerInjectionPresetCombo, 1);
        toolbar.Children.Add(_transformerInjectionPresetCombo);

        var frequencyLabel = new TextBlock
        {
            Text = "FREQ",
            Style = FindResource("SectionLabel") as Style,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(frequencyLabel, 3);
        toolbar.Children.Add(frequencyLabel);
        _transformerInjectionFrequencyText = new TextBox
        {
            Height = 32,
            Text = "50",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _transformerInjectionFrequencyText.TextChanged += (_, _) => ScheduleTransformerInjectionApply();
        Grid.SetColumn(_transformerInjectionFrequencyText, 4);
        toolbar.Children.Add(_transformerInjectionFrequencyText);

        var sideLabel = new TextBlock
        {
            Text = "DISPLAY",
            Style = FindResource("SectionLabel") as Style,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(sideLabel, 6);
        toolbar.Children.Add(sideLabel);
        _transformerInjectionDisplaySideCombo = new ComboBox
        {
            Height = 32,
            ItemsSource = new[] { "HV / Primary", "LV / Secondary" },
            SelectedIndex = 0,
            ToolTip = "Choose which injected side is projected to the shared waveform and phasor instruments."
        };
        _transformerInjectionDisplaySideCombo.SelectionChanged += (_, _) => RenderTransformerInjectionAnalysis();
        Grid.SetColumn(_transformerInjectionDisplaySideCombo, 7);
        toolbar.Children.Add(_transformerInjectionDisplaySideCombo);

        _transformerInjectionStatusText = new TextBlock
        {
            Text = "READY",
            Foreground = HealthyBrush,
            FontSize = 9.2,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var statusBadge = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(4),
            Background = BrushFrom("#EAF5EC"),
            BorderBrush = BrushFrom("#B9D8BF"),
            BorderThickness = new Thickness(1),
            Child = _transformerInjectionStatusText,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statusBadge, 9);
        toolbar.Children.Add(statusBadge);

        var table = new DataGrid
        {
            ItemsSource = _transformerInjectionRows,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = false,
            CanUserResizeRows = false,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = FindResource("LineBrush") as Brush,
            VerticalGridLinesBrush = Brushes.Transparent,
            AlternatingRowBackground = BrushFrom("#F8FAFC"),
            RowBackground = Brushes.White,
            BorderBrush = FindResource("LineBrush") as Brush,
            BorderThickness = new Thickness(1),
            RowHeight = 31,
            ColumnHeaderHeight = 30,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.Cell
        };
        table.Columns.Add(new DataGridTextColumn
        {
            Header = "Side",
            Width = new DataGridLength(84),
            IsReadOnly = true,
            Binding = new Binding(nameof(TransformerInjectionRow.Side))
        });
        table.Columns.Add(new DataGridTextColumn
        {
            Header = "Current channel",
            Width = new DataGridLength(1.3, DataGridLengthUnitType.Star),
            IsReadOnly = true,
            Binding = new Binding(nameof(TransformerInjectionRow.Channel))
        });
        table.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "On",
            Width = new DataGridLength(50),
            Binding = new Binding(nameof(TransformerInjectionRow.Enabled))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        });
        table.Columns.Add(CreateTransformerInjectionColumn("RMS secondary A", nameof(TransformerInjectionRow.RmsText), 1.1));
        table.Columns.Add(CreateTransformerInjectionColumn("Angle (°)", nameof(TransformerInjectionRow.AngleText), 0.9));
        table.Columns.Add(new DataGridTextColumn
        {
            Header = "Purpose",
            Width = new DataGridLength(1.25, DataGridLengthUnitType.Star),
            IsReadOnly = true,
            Binding = new Binding(nameof(TransformerInjectionRow.Purpose))
        });
        Grid.SetRow(table, 1);
        root.Children.Add(table);

        var footer = new Grid { Margin = new Thickness(0, 7, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        footer.Children.Add(new TextBlock
        {
            Text = "HV/LV are independent synchronized secondary-current sources · IN/NGR is an independent neutral CT channel, never calculated 3I0",
            Foreground = FindResource("MutedBrush") as Brush,
            FontSize = 9.4,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var resetVector = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "Stable through load",
            Margin = new Thickness(0, 0, 5, 0)
        };
        resetVector.Click += (_, _) => ApplyTransformerInjectionPreset("Balanced through load", announce: true);
        actions.Children.Add(resetVector);
        var resetRelay = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "Reset 87T"
        };
        resetRelay.Click += (_, _) => ResetTransformerVirtualInjection();
        actions.Children.Add(resetRelay);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);

        return root;
    }

    private static DataGridTextColumn CreateTransformerInjectionColumn(string header, string property, double width)
        => new()
        {
            Header = header,
            Width = new DataGridLength(width, DataGridLengthUnitType.Star),
            Binding = new Binding(property)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        };

    private void TransformerInjectionRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => ScheduleTransformerInjectionApply();

    private void ScheduleTransformerInjectionApply()
    {
        if (_transformerInjectionSync || _transformerInjectionApplyTimer is null)
            return;
        if (_transformerInjectionStatusText is not null)
        {
            _transformerInjectionStatusText.Text = "EDITING";
            _transformerInjectionStatusText.Foreground = WarningBrush;
        }
        _transformerInjectionApplyTimer.Stop();
        _transformerInjectionApplyTimer.Start();
    }

    private bool TryApplyTransformerInjectionEditor(bool announce)
    {
        if (_transformerVirtualInjectionRuntime is null || _transformerInjectionFrequencyText is null)
            return false;
        if (!double.TryParse(_transformerInjectionFrequencyText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var frequency) ||
            frequency is < 45 or > 65)
        {
            SetTransformerInjectionStatus("INVALID FREQ", TripBrush);
            return false;
        }

        try
        {
            var hvA = InjectionChannel("HV", "IA");
            var hvB = InjectionChannel("HV", "IB");
            var hvC = InjectionChannel("HV", "IC");
            var hvN = InjectionChannel("HV", "IN / NGR");
            var lvA = InjectionChannel("LV", "IA");
            var lvB = InjectionChannel("LV", "IB");
            var lvC = InjectionChannel("LV", "IC");
            var lvN = InjectionChannel("LV", "IN / NGR");
            var name = _transformerInjectionPresetCombo?.SelectedItem?.ToString() ?? "Custom transformer injection";
            var profile = new TransformerVirtualInjectionProfile(name, frequency, hvA, hvB, hvC, hvN, lvA, lvB, lvC, lvN);
            profile.Validate();
            _transformerVirtualInjectionRuntime.ApplyProfile(profile);
            SetTransformerInjectionStatus(_transformerVirtualInjectionRuntime.IsRunning ? "RUNNING" : "READY", HealthyBrush);
            if (_transformerVirtualInjectionRuntime.IsRunning)
                PublishTransformerInjectionSnapshot(_transformerVirtualInjectionRuntime.Advance(TimeSpan.FromMilliseconds(20)));
            RenderTransformerInjectionAnalysis();
            if (announce)
                StatusText.Text = "Transformer test vector applied atomically to the synchronized HV/LV virtual source.";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            SetTransformerInjectionStatus("INVALID", TripBrush);
            StatusText.Text = ex.Message;
            return false;
        }
    }

    private TransformerVirtualInjectionChannel InjectionChannel(string side, string channel)
    {
        var row = _transformerInjectionRows.Single(item => item.Side == side && item.Channel == channel);
        if (!double.TryParse(row.RmsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var rms) || rms < 0)
            throw new FormatException($"{side} {channel} RMS current is invalid.");
        if (!double.TryParse(row.AngleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle))
            throw new FormatException($"{side} {channel} angle is invalid.");
        return new TransformerVirtualInjectionChannel(rms, angle, row.Enabled);
    }

    private void TransformerInjectionPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_transformerInjectionSync || _transformerInjectionPresetCombo?.SelectedItem is not string preset)
            return;
        ApplyTransformerInjectionPreset(preset, announce: true);
    }

    private void ApplyTransformerInjectionPreset(string preset, bool announce)
    {
        if (_transformerVirtualInjectionRuntime is null)
            return;

        var stable = TransformerVirtualInjectionProfile.BalancedThroughLoad(_transformerVirtualInjectionRuntime.Configuration);
        TransformerVirtualInjectionProfile profile = preset switch
        {
            "Internal A fault" => stable with
            {
                Name = "Internal A fault",
                HighVoltageA = stable.HighVoltageA with { RmsA = stable.HighVoltageA.RmsA * 2.5 },
                LowVoltageA = stable.LowVoltageA with { RmsA = 0 }
            },
            "REF HV / NGR" => stable with
            {
                Name = "REF HV / NGR",
                HighVoltageNeutral = new TransformerVirtualInjectionChannel(1.0, 0, true)
            },
            "REF LV / NGR" => stable with
            {
                Name = "REF LV / NGR",
                LowVoltageNeutral = new TransformerVirtualInjectionChannel(1.0, 0, true)
            },
            _ => stable
        };
        _transformerVirtualInjectionRuntime.ApplyProfile(profile);
        SyncTransformerInjectionEditor(profile);
        if (_transformerVirtualInjectionRuntime.IsRunning)
            PublishTransformerInjectionSnapshot(_transformerVirtualInjectionRuntime.Advance(TimeSpan.FromMilliseconds(20)));
        RenderTransformerInjectionAnalysis();
        if (announce)
            StatusText.Text = $"Transformer injection preset '{profile.Name}' loaded. Values remain independently editable on HV and LV sides.";
    }

    private void SyncTransformerInjectionEditor(TransformerVirtualInjectionProfile profile)
    {
        _transformerInjectionSync = true;
        try
        {
            ApplyRow("HV", "IA", profile.HighVoltageA);
            ApplyRow("HV", "IB", profile.HighVoltageB);
            ApplyRow("HV", "IC", profile.HighVoltageC);
            ApplyRow("HV", "IN / NGR", profile.HighVoltageNeutral);
            ApplyRow("LV", "IA", profile.LowVoltageA);
            ApplyRow("LV", "IB", profile.LowVoltageB);
            ApplyRow("LV", "IC", profile.LowVoltageC);
            ApplyRow("LV", "IN / NGR", profile.LowVoltageNeutral);
            if (_transformerInjectionFrequencyText is not null)
                _transformerInjectionFrequencyText.Text = profile.FrequencyHz.ToString("0.###", CultureInfo.InvariantCulture);
            if (_transformerInjectionPresetCombo is not null)
                _transformerInjectionPresetCombo.SelectedItem = profile.Name;
            SetTransformerInjectionStatus(_transformerVirtualInjectionRuntime?.IsRunning == true ? "RUNNING" : "READY", HealthyBrush);
        }
        finally
        {
            _transformerInjectionSync = false;
        }
    }

    private void ApplyRow(string side, string channel, TransformerVirtualInjectionChannel source)
    {
        var row = _transformerInjectionRows.Single(item => item.Side == side && item.Channel == channel);
        row.Enabled = source.Enabled;
        row.RmsText = source.RmsA.ToString("0.###", CultureInfo.InvariantCulture);
        row.AngleText = source.AngleDegrees.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private void SetTransformerInjectionStatus(string text, Brush brush)
    {
        if (_transformerInjectionStatusText is null)
            return;
        _transformerInjectionStatusText.Text = text;
        _transformerInjectionStatusText.Foreground = brush;
    }

    private bool StartTransformerVirtualInjection(bool announce)
    {
        InitializeTransformerVirtualInjection();
        if (_transformerVirtualInjectionRuntime is null || !TryApplyTransformerInjectionEditor(announce: false))
            return false;
        var snapshot = _transformerVirtualInjectionRuntime.Start();
        _transformerInjectionLastAdvance = DateTimeOffset.UtcNow;
        PublishTransformerInjectionSnapshot(snapshot);
        SetTransformerInjectionStatus("RUNNING", HealthyBrush);
        RenderTransformerInjectionAnalysis();
        AddEvent("87T INJ START", _transformerVirtualInjectionRuntime.ActiveProfile.Name);
        if (announce)
            StatusText.Text = "Transformer paired secondary injection energized: HV + LV + independent NGR/neutral channels.";
        return true;
    }

    private bool StopTransformerVirtualInjection(bool announce)
    {
        if (_transformerVirtualInjectionRuntime is null)
            return false;
        var wasRunning = _transformerVirtualInjectionRuntime.IsRunning;
        var snapshot = _transformerVirtualInjectionRuntime.Stop();
        PublishTransformerInjectionSnapshot(snapshot);
        SetTransformerInjectionStatus("STOPPED", FindResource("MutedBrush") as Brush ?? Brushes.Gray);
        RenderTransformerInjectionAnalysis();
        if (wasRunning)
            AddEvent("87T INJ STOP", "HV/LV virtual current outputs forced to zero");
        if (announce)
            StatusText.Text = "Transformer virtual current outputs stopped at 0 A; configured test vector retained.";
        return wasRunning;
    }

    private void ResetTransformerVirtualInjection()
    {
        if (_transformerVirtualInjectionRuntime is null)
            return;
        PublishTransformerInjectionSnapshot(_transformerVirtualInjectionRuntime.Reset());
        AddEvent("87T RESET", "Internal transformer protection runtime reset");
        StatusText.Text = "Transformer internal test runtime, timers and virtual trip latch reset.";
    }

    private void TransformerInjectionRuntimeTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsTransformerInternalInjectionActive || _transformerVirtualInjectionRuntime?.IsRunning != true)
            return;
        var now = DateTimeOffset.UtcNow;
        var elapsed = now - _transformerInjectionLastAdvance;
        _transformerInjectionLastAdvance = now;
        var snapshot = _transformerVirtualInjectionRuntime.Advance(elapsed);
        PublishTransformerInjectionSnapshot(snapshot);
        RenderTransformerInjectionAnalysis();
    }

    private void TransformerVirtualInjectionRuntime_SnapshotChanged(object? sender, TransformerProtectionRuntimeSnapshotChangedEventArgs e)
    {
        if (IsTransformerInternalInjectionActive)
            PublishTransformerInjectionSnapshot(e.Snapshot);
    }

    private void PublishTransformerInjectionSnapshot(TransformerProtectionRuntimeSnapshot snapshot)
    {
        _transformerLastSnapshot = snapshot;
        _transformerFaceplatePresenter?.UpdateSnapshot(snapshot);
        RenderTransformerOperationWorkspace();
    }

    private void SetTransformerInjectionWorkspaceActive(bool active)
    {
        InitializeTransformerVirtualInjection();
        if (_transformerInjectionView is null)
            return;

        if (active)
        {
            if (_virtualInjectionView is not null)
                _virtualInjectionView.Visibility = Visibility.Collapsed;
            _transformerInjectionView.Visibility = _analysisWorkspaceMode == AnalysisWorkspaceMode.Injection
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (SourceCombo.SelectedIndex == 0)
                RenderTransformerInjectionAnalysis();
        }
        else
        {
            _transformerInjectionView.Visibility = Visibility.Collapsed;
            StopTransformerVirtualInjection(announce: false);
        }
    }

    private void RefreshTransformerInjectionDrawerVisibility()
    {
        if (_transformerInjectionView is null || !_transformerOcrWorkspaceMounted)
            return;
        if (_virtualInjectionView is not null)
            _virtualInjectionView.Visibility = Visibility.Collapsed;
        _transformerInjectionView.Visibility =
            SourceCombo.SelectedIndex == 0 && _analysisWorkspaceMode == AnalysisWorkspaceMode.Injection && !IsAdvancedInjectionOpen
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void RenderTransformerInjectionAnalysis()
    {
        if (!IsTransformerInternalInjectionActive || _transformerVirtualInjectionRuntime is null)
            return;

        RefreshTransformerInjectionDrawerVisibility();
        var hv = _transformerInjectionDisplaySideCombo?.SelectedIndex != 1;
        var side = _transformerVirtualInjectionRuntime.SnapshotForSide(hv);
        var waveform = side.Waveform;
        SmvScope.Frame = new WaveformFrame(
            waveform.PhaseA,
            waveform.PhaseB,
            waveform.PhaseC,
            waveform.Residual,
            waveform.FrequencyHz,
            double.NaN,
            double.NaN)
        {
            SampleRateHz = waveform.FrequencyHz * side.SamplesPerCycle,
            NominalSamplesPerCycle = side.SamplesPerCycle
        };

        IaValueText.Text = $"{side.Measurement.PhaseA:0.000} A";
        IbValueText.Text = $"{side.Measurement.PhaseB:0.000} A";
        IcValueText.Text = $"{side.Measurement.PhaseC:0.000} A";
        ResidualValueText.Text = $"{side.Measurement.Residual:0.000} A";
        FrequencyText.Text = $"{waveform.FrequencyHz:0.000} Hz";
        SamplesPerCycleText.Text = $"  ·  {side.SamplesPerCycle} samples/cycle";
        SampleCounterText.Text = "  ·  paired HV/LV";
        SyncText.Text = "  ·  smpSynch 2";
        SyncText.Foreground = HealthyBrush;
        FpsText.Text = "  ·  VIRTUAL 87T";
        StreamHealthText.Text = _transformerVirtualInjectionRuntime.IsRunning ? "RUNNING" : "STOPPED";
        StreamHealthText.Foreground = _transformerVirtualInjectionRuntime.IsRunning ? HealthyBrush : FindResource("MutedBrush") as Brush;
        WaveformSubtitleText.Text = hv
            ? "Transformer HV / primary-side secondary injection"
            : "Transformer LV / secondary-side secondary injection";

        if (_phasorScope is not null)
        {
            var mode = _phasorDisplayMode == PhasorDisplayMode.Voltage
                ? PhasorDisplayMode.Current
                : _phasorDisplayMode;
            _phasorScope.Frame = PhasorDisplayProjector.Project(side.Measurement.Phasors, mode);
            _lastPhasorPresentationSignature = null;
        }
    }
}

internal sealed class TransformerInjectionRow : INotifyPropertyChanged
{
    public static IReadOnlyList<(string Side, string Channel, bool IsNeutral)> StandardRows { get; } =
    [
        ("HV", "IA", false), ("HV", "IB", false), ("HV", "IC", false), ("HV", "IN / NGR", true),
        ("LV", "IA", false), ("LV", "IB", false), ("LV", "IC", false), ("LV", "IN / NGR", true)
    ];

    private bool _enabled;
    private string _rmsText = "0";
    private string _angleText = "0";

    public TransformerInjectionRow(string side, string channel, bool isNeutral)
    {
        Side = side;
        Channel = channel;
        IsNeutral = isNeutral;
        _enabled = !isNeutral;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Side { get; }
    public string Channel { get; }
    public bool IsNeutral { get; }
    public string Purpose => IsNeutral ? "Independent NCT / NGR" : "Phase CT secondary";

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public string RmsText
    {
        get => _rmsText;
        set => Set(ref _rmsText, value);
    }

    public string AngleText
    {
        get => _angleText;
        set => Set(ref _angleText, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
