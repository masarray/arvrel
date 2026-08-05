using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Services;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private readonly ObservableCollection<VirtualInjectionRow> _virtualInjectionRows = new();
    private bool _virtualInjectionInitialized;
    private bool _virtualInjectionEditorSync;
    private Grid? _virtualInjectionView;
    private ComboBox? _virtualInjectionPresetCombo;
    private TextBox? _virtualInjectionFrequencyText;
    private TextBlock? _virtualInjectionStatusText;
    private Border? _virtualInjectionStatusBadge;
    private TextBlock? _virtualInjectionProvenanceText;
    private DispatcherTimer? _virtualInjectionApplyTimer;
    private string _pendingInjectionName = "Custom injection";

    private void InitializeVirtualInjectionEditor()
    {
        if (_virtualInjectionInitialized || _analysisHost is null)
            return;
        _virtualInjectionInitialized = true;

        foreach (var signal in Enum.GetValues<VirtualInjectionSignal>())
        {
            var row = new VirtualInjectionRow(signal);
            row.PropertyChanged += VirtualInjectionRow_PropertyChanged;
            _virtualInjectionRows.Add(row);
        }

        _virtualInjectionView = BuildVirtualInjectionView();
        _virtualInjectionView.Visibility = Visibility.Collapsed;
        Grid.SetColumn(_virtualInjectionView, 0);
        _analysisHost.Children.Add(_virtualInjectionView);

        _virtualInjectionApplyTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(240)
        };
        _virtualInjectionApplyTimer.Tick += VirtualInjectionApplyTimer_Tick;
        Closed += (_, _) => _virtualInjectionApplyTimer?.Stop();

        SyncVirtualInjectionEditorFromProfile(_scenario.ActiveProfile);
    }

    private Grid BuildVirtualInjectionView()
    {
        var root = new Grid
        {
            Background = FindResource("PanelBrush") as Brush,
            ClipToBounds = true
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(43) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

        var toolbar = new Grid
        {
            Margin = new Thickness(0, 0, 0, 7)
        };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        var presetLabel = new TextBlock
        {
            Text = "PRESET",
            Style = FindResource("SectionLabel") as Style,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        toolbar.Children.Add(presetLabel);

        _virtualInjectionPresetCombo = new ComboBox
        {
            ItemsSource = VirtualInjectionPresets.Names,
            Height = 32,
            MinHeight = 32,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Load a visible 4I+4V starting point; presets populate the editable table and do not bypass it."
        };
        _virtualInjectionPresetCombo.SelectionChanged += VirtualInjectionPresetCombo_SelectionChanged;
        Grid.SetColumn(_virtualInjectionPresetCombo, 1);
        toolbar.Children.Add(_virtualInjectionPresetCombo);

        var frequencyLabel = new TextBlock
        {
            Text = "FREQ",
            Style = FindResource("SectionLabel") as Style,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(frequencyLabel, 3);
        toolbar.Children.Add(frequencyLabel);

        _virtualInjectionFrequencyText = new TextBox
        {
            Height = 32,
            MinHeight = 32,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            ToolTip = $"Common synchronous frequency, {VirtualInjectionProfile.MinimumFrequencyHz:0}–{VirtualInjectionProfile.MaximumFrequencyHz:0} Hz."
        };
        _virtualInjectionFrequencyText.TextChanged += VirtualInjectionFrequencyText_TextChanged;
        Grid.SetColumn(_virtualInjectionFrequencyText, 4);
        toolbar.Children.Add(_virtualInjectionFrequencyText);

        _virtualInjectionStatusText = new TextBlock
        {
            Text = "READY",
            FontSize = 9.2,
            FontWeight = FontWeights.SemiBold,
            Foreground = HealthyBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        _virtualInjectionStatusBadge = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(4),
            Background = BrushFrom("#EAF5EC"),
            BorderBrush = BrushFrom("#B9D8BF"),
            BorderThickness = new Thickness(1),
            Child = _virtualInjectionStatusText,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_virtualInjectionStatusBadge, 6);
        toolbar.Children.Add(_virtualInjectionStatusBadge);

        var table = new DataGrid
        {
            ItemsSource = _virtualInjectionRows,
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
            RowHeight = 32,
            ColumnHeaderHeight = 30,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.Cell,
            Margin = new Thickness(0),
            IsReadOnly = false
        };
        table.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "On",
            Width = new DataGridLength(48),
            Binding = new Binding(nameof(VirtualInjectionRow.IsEnabled))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        });
        table.Columns.Add(new DataGridTextColumn
        {
            Header = "Signal",
            Width = new DataGridLength(1.1, DataGridLengthUnitType.Star),
            IsReadOnly = true,
            Binding = new Binding(nameof(VirtualInjectionRow.SignalLabel))
        });
        table.Columns.Add(CreateInjectionEditColumn("RMS value", nameof(VirtualInjectionRow.ValueText), 1.25));
        table.Columns.Add(CreateInjectionEditColumn("Angle (°)", nameof(VirtualInjectionRow.AngleText), 1.05));
        table.Columns.Add(new DataGridTextColumn
        {
            Header = "Unit",
            Width = new DataGridLength(58),
            IsReadOnly = true,
            Binding = new Binding(nameof(VirtualInjectionRow.Unit))
        });
        table.Columns.Add(new DataGridTextColumn
        {
            Header = "Provenance",
            Width = new DataGridLength(1.15, DataGridLengthUnitType.Star),
            IsReadOnly = true,
            Binding = new Binding(nameof(VirtualInjectionRow.Provenance))
        });
        InitializeVirtualInjectionAngleContextMenu(table);
        Grid.SetRow(table, 1);
        root.Children.Add(table);

        var footer = new Grid
        {
            Margin = new Thickness(0, 7, 0, 0)
        };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _virtualInjectionProvenanceText = new TextBlock
        {
            Text = "IN/VN disabled → residual quantities calculated from phase sums",
            Foreground = FindResource("MutedBrush") as Brush,
            FontSize = 9.8,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = "Unchecked IN or VN uses IA+IB+IC or VA+VB+VC. Checked neutral rows become explicit virtual channels."
        };
        footer.Children.Add(_virtualInjectionProvenanceText);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);

        var clearButton = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "Clear injection",
            Margin = new Thickness(0, 0, 5, 0),
            ToolTip = "Return the virtual source to balanced nominal quantities without clearing a latched trip."
        };
        clearButton.Click += (_, _) => ApplyVirtualInjectionPreset("Normal balanced", announce: true);
        actions.Children.Add(clearButton);

        var resetRelayButton = new Button
        {
            Style = FindResource("CompactButton") as Style,
            Content = "Reset relay",
            ToolTip = "Clear protection timers, trip latch, and evidence cursors while keeping the active injection profile."
        };
        resetRelayButton.Click += (_, _) => ResetVirtualInjectionRelay();
        actions.Children.Add(resetRelayButton);

        return root;
    }

    private static DataGridTextColumn CreateInjectionEditColumn(string header, string property, double starWidth)
    {
        var binding = new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            ValidatesOnDataErrors = true,
            NotifyOnValidationError = true
        };
        return new DataGridTextColumn
        {
            Header = header,
            Width = new DataGridLength(starWidth, DataGridLengthUnitType.Star),
            Binding = binding
        };
    }

    private void VirtualInjectionRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_virtualInjectionEditorSync)
            return;
        _pendingInjectionName = "Custom injection";
        ScheduleVirtualInjectionApply();
    }

    private void VirtualInjectionFrequencyText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_virtualInjectionEditorSync)
            return;
        _pendingInjectionName = "Custom injection";
        ScheduleVirtualInjectionApply();
    }

    private void VirtualInjectionPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_virtualInjectionEditorSync ||
            _virtualInjectionPresetCombo?.SelectedItem is not string preset)
            return;
        ApplyVirtualInjectionPreset(preset, announce: true);
    }

    private void ScheduleVirtualInjectionApply()
    {
        if (!_virtualInjectionInitialized || _virtualInjectionApplyTimer is null)
            return;
        SetVirtualInjectionStatus("EDITING", WarningBrush, "#FBF2E3", "#E2C58F");
        _virtualInjectionApplyTimer.Stop();
        _virtualInjectionApplyTimer.Start();
    }

    private void VirtualInjectionApplyTimer_Tick(object? sender, EventArgs e)
    {
        _virtualInjectionApplyTimer?.Stop();
        TryApplyVirtualInjectionEditor();
    }

    private bool TryApplyVirtualInjectionEditor()
    {
        if (_virtualInjectionFrequencyText is null)
            return false;

        if (!VirtualInjectionRow.TryParseEngineeringDouble(_virtualInjectionFrequencyText.Text, out var frequency) ||
            !double.IsFinite(frequency) ||
            frequency < VirtualInjectionProfile.MinimumFrequencyHz ||
            frequency > VirtualInjectionProfile.MaximumFrequencyHz)
        {
            SetVirtualInjectionInvalid($"Frequency must be {VirtualInjectionProfile.MinimumFrequencyHz:0}–{VirtualInjectionProfile.MaximumFrequencyHz:0} Hz.");
            _virtualInjectionFrequencyText.BorderBrush = TripBrush;
            return false;
        }
        _virtualInjectionFrequencyText.ClearValue(Control.BorderBrushProperty);

        var channels = new Dictionary<VirtualInjectionSignal, VirtualInjectionChannel>();
        foreach (var row in _virtualInjectionRows)
        {
            if (!row.TryCreateChannel(out var channel, out var error))
            {
                SetVirtualInjectionInvalid(error);
                return false;
            }
            channels[row.Signal] = channel;
        }

        try
        {
            var profile = new VirtualInjectionProfile(
                _pendingInjectionName,
                frequency,
                channels[VirtualInjectionSignal.PhaseAVoltage],
                channels[VirtualInjectionSignal.PhaseBVoltage],
                channels[VirtualInjectionSignal.PhaseCVoltage],
                channels[VirtualInjectionSignal.NeutralVoltage],
                channels[VirtualInjectionSignal.PhaseACurrent],
                channels[VirtualInjectionSignal.PhaseBCurrent],
                channels[VirtualInjectionSignal.PhaseCCurrent],
                channels[VirtualInjectionSignal.NeutralCurrent]).Normalize();

            var changed = _scenario.ApplyProfile(profile);
            UpdateVirtualInjectionProvenance();
            if (changed)
            {
                SetVirtualInjectionStatus("APPLIED · REBUILDING", WarningBrush, "#FBF2E3", "#E2C58F");
                AddEvent("INJECTION", $"{profile.Name} · {profile.Fingerprint()[..12]}");
                StatusText.Text = "Virtual injection applied atomically. Protection pickup is restrained until one coherent cycle is rebuilt.";
            }
            else
            {
                SetVirtualInjectionStatus("READY", HealthyBrush, "#EAF5EC", "#B9D8BF");
            }

            RenderInitialFrame();
            RefreshPhasorFrame();
            return true;
        }
        catch (ArgumentException ex)
        {
            SetVirtualInjectionInvalid(ex.Message);
            return false;
        }
    }

    private void ApplyVirtualInjectionPreset(string preset, bool announce)
    {
        var frequency = _scenario.ActiveProfile.FrequencyHz;
        if (_virtualInjectionFrequencyText is not null &&
            VirtualInjectionRow.TryParseEngineeringDouble(_virtualInjectionFrequencyText.Text, out var entered) &&
            entered is >= VirtualInjectionProfile.MinimumFrequencyHz and <= VirtualInjectionProfile.MaximumFrequencyHz)
            frequency = entered;

        var profile = VirtualInjectionPresets.Create(preset, frequency);
        var changed = _scenario.ApplyProfile(profile);
        SyncVirtualInjectionEditorFromProfile(profile);
        if (changed)
        {
            SetVirtualInjectionStatus("APPLIED · REBUILDING", WarningBrush, "#FBF2E3", "#E2C58F");
            AddEvent("INJECTION", $"Preset {preset} · {profile.Fingerprint()[..12]}");
        }
        RenderInitialFrame();
        RefreshPhasorFrame();
        if (announce)
            StatusText.Text = $"Virtual injection preset '{preset}' applied. Values remain editable and auto-apply after validation.";
    }

    private void SyncVirtualInjectionEditorFromProfile(VirtualInjectionProfile profile)
    {
        if (!_virtualInjectionInitialized)
            return;

        _virtualInjectionEditorSync = true;
        try
        {
            foreach (var row in _virtualInjectionRows)
                row.Apply(profile.Channel(row.Signal));
            if (_virtualInjectionFrequencyText is not null)
                _virtualInjectionFrequencyText.Text = profile.FrequencyHz.ToString("0.###", CultureInfo.InvariantCulture);
            if (_virtualInjectionPresetCombo is not null)
                _virtualInjectionPresetCombo.SelectedItem = VirtualInjectionPresets.Names.Contains(profile.Name)
                    ? profile.Name
                    : null;
            _pendingInjectionName = profile.Name;
            UpdateVirtualInjectionProvenance();
            SetVirtualInjectionStatus(
                _scenario.WindowStatus == "coherent" ? "READY" : "APPLIED · REBUILDING",
                _scenario.WindowStatus == "coherent" ? HealthyBrush : WarningBrush,
                _scenario.WindowStatus == "coherent" ? "#EAF5EC" : "#FBF2E3",
                _scenario.WindowStatus == "coherent" ? "#B9D8BF" : "#E2C58F");
        }
        finally
        {
            _virtualInjectionEditorSync = false;
        }
    }

    private void UpdateVirtualInjectionProvenance()
    {
        if (_virtualInjectionProvenanceText is null)
            return;
        var current = _virtualInjectionRows.FirstOrDefault(row => row.Signal == VirtualInjectionSignal.NeutralCurrent);
        var voltage = _virtualInjectionRows.FirstOrDefault(row => row.Signal == VirtualInjectionSignal.NeutralVoltage);
        var currentText = current?.IsEnabled == true ? "IN explicit" : "3I0 = IA+IB+IC";
        var voltageText = voltage?.IsEnabled == true ? "VN explicit" : "3V0 = VA+VB+VC";
        _virtualInjectionProvenanceText.Text = $"{currentText} · {voltageText} · common synchronous frequency";
    }

    private void SetVirtualInjectionInvalid(string error)
    {
        SetVirtualInjectionStatus("INVALID · LAST VALID ACTIVE", TripBrush, "#FCEAEA", "#E5B6B3");
        if (_virtualInjectionStatusBadge is not null)
            _virtualInjectionStatusBadge.ToolTip = error;
        StatusText.Text = error;
    }

    private void SetVirtualInjectionStatus(string text, Brush foreground, string background, string border)
    {
        if (_virtualInjectionStatusText is not null)
        {
            _virtualInjectionStatusText.Text = text;
            _virtualInjectionStatusText.Foreground = foreground;
        }
        if (_virtualInjectionStatusBadge is not null)
        {
            _virtualInjectionStatusBadge.Background = BrushFrom(background);
            _virtualInjectionStatusBadge.BorderBrush = BrushFrom(border);
            _virtualInjectionStatusBadge.ToolTip = text;
        }
    }

    private void ResetVirtualInjectionRelay()
    {
        ResetTransitionMarkers();
        _internalEngine.Reset();
        _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
        RenderInitialFrame();
        AddEvent("RESET", "Relay state reset; injection retained");
        StatusText.Text = $"Relay timers and trip latch reset. Injection '{_scenario.ActiveProfile.Name}' remains active.";
    }
}
