using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class ProtectionSettingsWindow
{
    private bool _feederTabInstalled;

    private CheckBox _undervoltageEnabled = null!;
    private TextBox _undervoltagePickup = null!;
    private TextBox _undervoltageDelay = null!;
    private TextBox _undervoltageReset = null!;
    private ComboBox _undervoltageMode = null!;
    private ComboBox _undervoltageLogic = null!;

    private CheckBox _overvoltageEnabled = null!;
    private TextBox _overvoltagePickup = null!;
    private TextBox _overvoltageDelay = null!;
    private TextBox _overvoltageDropout = null!;
    private ComboBox _overvoltageMode = null!;
    private ComboBox _overvoltageLogic = null!;

    private CheckBox _residualOvervoltageEnabled = null!;
    private TextBox _residualOvervoltagePickup = null!;
    private TextBox _residualOvervoltageDelay = null!;
    private TextBox _residualOvervoltageDropout = null!;

    private CheckBox _directionalPhaseEnabled = null!;
    private TextBox _directionalPhasePickup = null!;
    private TextBox _directionalPhaseDelay = null!;
    private TextBox _directionalPhaseDropout = null!;
    private TextBox _directionalPhaseMta = null!;
    private TextBox _directionalPhaseMinimumVoltage = null!;
    private ComboBox _directionalPhaseSense = null!;

    private CheckBox _directionalEarthEnabled = null!;
    private TextBox _directionalEarthPickup = null!;
    private TextBox _directionalEarthDelay = null!;
    private TextBox _directionalEarthDropout = null!;
    private TextBox _directionalEarthMta = null!;
    private TextBox _directionalEarthMinimumVoltage = null!;
    private ComboBox _directionalEarthSense = null!;

    [ModuleInitializer]
    internal static void RegisterFeederSettingsIntegration()
    {
        EventManager.RegisterClassHandler(
            typeof(ProtectionSettingsWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnProtectionSettingsLoaded));
    }

    private static void OnProtectionSettingsLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ProtectionSettingsWindow window)
            window.InstallFeederSettingsTab();
    }

    private void InstallFeederSettingsTab()
    {
        if (_feederTabInstalled)
            return;
        _feederTabInstalled = true;

        var active = (Owner as MainWindow)?.ActiveProtectionSettingsForEditor.Feeder ?? new FeederProtectionSettings();
        SettingsTabs.Items.Add(BuildFeederTab(active));
        CtContextText.Text =
            $"CT {_measurementContext.CtPrimaryA:0.###}/{_measurementContext.CtSecondaryA:0.###} A · " +
            $"VT {_measurementContext.VtPrimaryV:0.###}/{_measurementContext.VtSecondaryV:0.###} V · " +
            $"{_measurementContext.NominalFrequencyHz:0.###} Hz";
        Closing += ApplyFeederSettingsOnClosing;
    }

    private TabItem BuildFeederTab(FeederProtectionSettings settings)
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new Border
        {
            Style = (Style)FindResource("InfoStrip"),
            Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBlock
            {
                Text = "67P uses positive-sequence V1/I1 polarization. 67N uses residual 3V0/3I0 polarization. Voltage and directional elements remain secure when a complete phasor window or minimum polarizing voltage is unavailable.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
                FontSize = 10.4,
                LineHeight = 15
            }
        });

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var directionalStack = new StackPanel();
        directionalStack.Children.Add(BuildDirectionalPhaseCard(settings));
        directionalStack.Children.Add(BuildDirectionalEarthCard(settings));
        Grid.SetColumn(directionalStack, 0);
        columns.Children.Add(directionalStack);

        var voltageStack = new StackPanel();
        voltageStack.Children.Add(BuildUndervoltageCard(settings));
        voltageStack.Children.Add(BuildOvervoltageCard(settings));
        voltageStack.Children.Add(BuildResidualOvervoltageCard(settings));
        Grid.SetColumn(voltageStack, 2);
        columns.Children.Add(voltageStack);

        panel.Children.Add(columns);
        return new TabItem
        {
            Header = "Feeder protection",
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = panel
            }
        };
    }

    private Border BuildDirectionalPhaseCard(FeederProtectionSettings settings)
    {
        _directionalPhaseEnabled = Header("67P directional phase overcurrent", settings.DirectionalPhase67Enabled);
        _directionalPhasePickup = Field(settings.DirectionalPhase67PickupA);
        _directionalPhaseDelay = TimeField(settings.DirectionalPhase67Delay);
        _directionalPhaseDropout = Field(settings.DirectionalPhase67DropoutRatio);
        _directionalPhaseMta = Field(settings.DirectionalPhase67CharacteristicAngleDeg);
        _directionalPhaseMinimumVoltage = Field(settings.DirectionalPhase67MinimumPolarizingVoltageV);
        _directionalPhaseSense = EnumField(
            new[]
            {
                new EnumChoice<DirectionalSense>(DirectionalSense.Forward, "Forward"),
                new EnumChoice<DirectionalSense>(DirectionalSense.Reverse, "Reverse")
            },
            settings.DirectionalPhase67Sense);

        return Card(
            _directionalPhaseEnabled,
            "V1 / I1",
            Row("Pickup current", _directionalPhasePickup),
            Row("Definite delay", _directionalPhaseDelay),
            Row("Direction", _directionalPhaseSense),
            Row("Characteristic angle", _directionalPhaseMta),
            Row("Minimum V1", _directionalPhaseMinimumVoltage),
            Row("Dropout ratio", _directionalPhaseDropout));
    }

    private Border BuildDirectionalEarthCard(FeederProtectionSettings settings)
    {
        _directionalEarthEnabled = Header("67N directional earth fault", settings.DirectionalEarth67NEnabled);
        _directionalEarthPickup = Field(settings.DirectionalEarth67NPickupA);
        _directionalEarthDelay = TimeField(settings.DirectionalEarth67NDelay);
        _directionalEarthDropout = Field(settings.DirectionalEarth67NDropoutRatio);
        _directionalEarthMta = Field(settings.DirectionalEarth67NCharacteristicAngleDeg);
        _directionalEarthMinimumVoltage = Field(settings.DirectionalEarth67NMinimumPolarizingVoltageV);
        _directionalEarthSense = EnumField(
            new[]
            {
                new EnumChoice<DirectionalSense>(DirectionalSense.Forward, "Forward"),
                new EnumChoice<DirectionalSense>(DirectionalSense.Reverse, "Reverse")
            },
            settings.DirectionalEarth67NSense);

        var card = Card(
            _directionalEarthEnabled,
            "3V0 / 3I0",
            Row("Pickup residual current", _directionalEarthPickup),
            Row("Definite delay", _directionalEarthDelay),
            Row("Direction", _directionalEarthSense),
            Row("Characteristic angle", _directionalEarthMta),
            Row("Minimum 3V0", _directionalEarthMinimumVoltage),
            Row("Dropout ratio", _directionalEarthDropout));
        card.Margin = new Thickness(0, 12, 0, 0);
        return card;
    }

    private Border BuildUndervoltageCard(FeederProtectionSettings settings)
    {
        _undervoltageEnabled = Header("27 undervoltage", settings.Undervoltage27Enabled);
        _undervoltagePickup = Field(settings.Undervoltage27PickupV);
        _undervoltageDelay = TimeField(settings.Undervoltage27Delay);
        _undervoltageReset = Field(settings.Undervoltage27ResetRatio);
        _undervoltageMode = VoltageModeField(settings.Undervoltage27Mode);
        _undervoltageLogic = VoltageLogicField(settings.Undervoltage27Logic);

        return Card(
            _undervoltageEnabled,
            "V<",
            Row("Pickup voltage", _undervoltagePickup),
            Row("Definite delay", _undervoltageDelay),
            Row("Measurement", _undervoltageMode),
            Row("Phase logic", _undervoltageLogic),
            Row("Reset ratio", _undervoltageReset));
    }

    private Border BuildOvervoltageCard(FeederProtectionSettings settings)
    {
        _overvoltageEnabled = Header("59 overvoltage", settings.Overvoltage59Enabled);
        _overvoltagePickup = Field(settings.Overvoltage59PickupV);
        _overvoltageDelay = TimeField(settings.Overvoltage59Delay);
        _overvoltageDropout = Field(settings.Overvoltage59DropoutRatio);
        _overvoltageMode = VoltageModeField(settings.Overvoltage59Mode);
        _overvoltageLogic = VoltageLogicField(settings.Overvoltage59Logic);

        var card = Card(
            _overvoltageEnabled,
            "V>",
            Row("Pickup voltage", _overvoltagePickup),
            Row("Definite delay", _overvoltageDelay),
            Row("Measurement", _overvoltageMode),
            Row("Phase logic", _overvoltageLogic),
            Row("Dropout ratio", _overvoltageDropout));
        card.Margin = new Thickness(0, 12, 0, 0);
        return card;
    }

    private Border BuildResidualOvervoltageCard(FeederProtectionSettings settings)
    {
        _residualOvervoltageEnabled = Header("59N residual overvoltage", settings.ResidualOvervoltage59NEnabled);
        _residualOvervoltagePickup = Field(settings.ResidualOvervoltage59NPickupV);
        _residualOvervoltageDelay = TimeField(settings.ResidualOvervoltage59NDelay);
        _residualOvervoltageDropout = Field(settings.ResidualOvervoltage59NDropoutRatio);

        var card = Card(
            _residualOvervoltageEnabled,
            "3V0",
            Row("Pickup residual voltage", _residualOvervoltagePickup),
            Row("Definite delay", _residualOvervoltageDelay),
            Row("Dropout ratio", _residualOvervoltageDropout));
        card.Margin = new Thickness(0, 12, 0, 0);
        return card;
    }

    private Border Card(CheckBox header, string badge, params Grid[] rows)
    {
        var stack = new StackPanel();
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(header);
        var badgeBorder = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 244, 248)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = badge,
                Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush"),
                FontSize = 8.8,
                FontWeight = FontWeights.SemiBold
            }
        };
        Grid.SetColumn(badgeBorder, 1);
        headerGrid.Children.Add(badgeBorder);
        stack.Children.Add(headerGrid);
        foreach (var row in rows)
            stack.Children.Add(row);

        return new Border
        {
            Style = (Style)FindResource("SettingsCard"),
            Child = stack
        };
    }

    private Grid Row(string label, Control control)
    {
        var row = new Grid { Height = 34 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("FieldLabel")
        });
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private CheckBox Header(string text, bool value) => new()
    {
        Content = text,
        IsChecked = value,
        Style = (Style)FindResource("ElementHeader")
    };

    private TextBox Field(double value) => new()
    {
        Text = value.ToString("0.####", CultureInfo.InvariantCulture),
        Style = (Style)FindResource("SettingTextBox")
    };

    private TextBox TimeField(TimeSpan value) => new()
    {
        Text = value.TotalMilliseconds < 1000
            ? $"{value.TotalMilliseconds:0.###} ms"
            : $"{value.TotalSeconds:0.###} s",
        Style = (Style)FindResource("SettingTextBox")
    };

    private ComboBox VoltageModeField(VoltageMeasurementMode selected) => EnumField(
        new[]
        {
            new EnumChoice<VoltageMeasurementMode>(VoltageMeasurementMode.PhaseToNeutral, "Phase-to-neutral"),
            new EnumChoice<VoltageMeasurementMode>(VoltageMeasurementMode.PhaseToPhase, "Phase-to-phase"),
            new EnumChoice<VoltageMeasurementMode>(VoltageMeasurementMode.PositiveSequence, "Positive sequence V1")
        },
        selected);

    private ComboBox VoltageLogicField(VoltageSelectionLogic selected) => EnumField(
        new[]
        {
            new EnumChoice<VoltageSelectionLogic>(VoltageSelectionLogic.OneOfThree, "1 of 3 phases"),
            new EnumChoice<VoltageSelectionLogic>(VoltageSelectionLogic.TwoOfThree, "2 of 3 phases"),
            new EnumChoice<VoltageSelectionLogic>(VoltageSelectionLogic.ThreeOfThree, "3 of 3 phases")
        },
        selected);

    private ComboBox EnumField<T>(IReadOnlyList<EnumChoice<T>> options, T selected) where T : struct, Enum
    {
        var combo = new ComboBox
        {
            Style = (Style)FindResource("SettingCombo"),
            ItemsSource = options
        };
        combo.SelectedItem = options.First(option => EqualityComparer<T>.Default.Equals(option.Value, selected));
        return combo;
    }

    private void ApplyFeederSettingsOnClosing(object? sender, CancelEventArgs e)
    {
        if (DialogResult != true || Result is null)
            return;

        try
        {
            var feeder = BuildFeederSettings();
            feeder.Validate();
            Result = Result with { Feeder = feeder };
            Result.Validate();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            e.Cancel = true;
            MessageBox.Show(this, ex.Message, "Invalid feeder protection settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            Dispatcher.BeginInvoke(() => DialogResult = null);
        }
    }

    private FeederProtectionSettings BuildFeederSettings() => new()
    {
        Undervoltage27Enabled = _undervoltageEnabled.IsChecked == true,
        Undervoltage27PickupV = ReadNumber(_undervoltagePickup, "27 pickup voltage"),
        Undervoltage27Delay = ReadTime(_undervoltageDelay, "27 delay"),
        Undervoltage27ResetRatio = ReadNumber(_undervoltageReset, "27 reset ratio"),
        Undervoltage27Mode = ReadEnum<VoltageMeasurementMode>(_undervoltageMode, "27 measurement mode"),
        Undervoltage27Logic = ReadEnum<VoltageSelectionLogic>(_undervoltageLogic, "27 phase logic"),

        Overvoltage59Enabled = _overvoltageEnabled.IsChecked == true,
        Overvoltage59PickupV = ReadNumber(_overvoltagePickup, "59 pickup voltage"),
        Overvoltage59Delay = ReadTime(_overvoltageDelay, "59 delay"),
        Overvoltage59DropoutRatio = ReadNumber(_overvoltageDropout, "59 dropout ratio"),
        Overvoltage59Mode = ReadEnum<VoltageMeasurementMode>(_overvoltageMode, "59 measurement mode"),
        Overvoltage59Logic = ReadEnum<VoltageSelectionLogic>(_overvoltageLogic, "59 phase logic"),

        ResidualOvervoltage59NEnabled = _residualOvervoltageEnabled.IsChecked == true,
        ResidualOvervoltage59NPickupV = ReadNumber(_residualOvervoltagePickup, "59N pickup voltage"),
        ResidualOvervoltage59NDelay = ReadTime(_residualOvervoltageDelay, "59N delay"),
        ResidualOvervoltage59NDropoutRatio = ReadNumber(_residualOvervoltageDropout, "59N dropout ratio"),

        DirectionalPhase67Enabled = _directionalPhaseEnabled.IsChecked == true,
        DirectionalPhase67PickupA = ReadNumber(_directionalPhasePickup, "67P pickup current"),
        DirectionalPhase67Delay = ReadTime(_directionalPhaseDelay, "67P delay"),
        DirectionalPhase67DropoutRatio = ReadNumber(_directionalPhaseDropout, "67P dropout ratio"),
        DirectionalPhase67CharacteristicAngleDeg = ReadNumber(_directionalPhaseMta, "67P characteristic angle", allowNegative: true),
        DirectionalPhase67MinimumPolarizingVoltageV = ReadNumber(_directionalPhaseMinimumVoltage, "67P minimum polarizing voltage"),
        DirectionalPhase67Sense = ReadEnum<DirectionalSense>(_directionalPhaseSense, "67P direction"),

        DirectionalEarth67NEnabled = _directionalEarthEnabled.IsChecked == true,
        DirectionalEarth67NPickupA = ReadNumber(_directionalEarthPickup, "67N pickup current"),
        DirectionalEarth67NDelay = ReadTime(_directionalEarthDelay, "67N delay"),
        DirectionalEarth67NDropoutRatio = ReadNumber(_directionalEarthDropout, "67N dropout ratio"),
        DirectionalEarth67NCharacteristicAngleDeg = ReadNumber(_directionalEarthMta, "67N characteristic angle", allowNegative: true),
        DirectionalEarth67NMinimumPolarizingVoltageV = ReadNumber(_directionalEarthMinimumVoltage, "67N minimum polarizing voltage"),
        DirectionalEarth67NSense = ReadEnum<DirectionalSense>(_directionalEarthSense, "67N direction")
    };

    private static double ReadNumber(TextBox field, string name, bool allowNegative = false)
    {
        if (!double.TryParse(field.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            !double.TryParse(field.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            throw new FormatException($"{name} must be a number.");
        if (!double.IsFinite(value) || (!allowNegative && value <= 0))
            throw new FormatException($"{name} must be {(allowNegative ? "a finite angle" : "greater than zero")}.");
        return value;
    }

    private static TimeSpan ReadTime(TextBox field, string name)
    {
        var value = field.Text.Trim().ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
        var multiplier = 1.0;
        if (value.EndsWith("ms", StringComparison.Ordinal))
            value = value[..^2];
        else if (value.EndsWith('s'))
        {
            value = value[..^1];
            multiplier = 1000;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number < 0)
            throw new FormatException($"{name} must be a positive time in ms or s.");
        return TimeSpan.FromMilliseconds(number * multiplier);
    }

    private static T ReadEnum<T>(ComboBox field, string name) where T : struct, Enum
    {
        if (field.SelectedItem is EnumChoice<T> selected)
            return selected.Value;
        throw new FormatException($"{name} must be selected.");
    }

    private sealed record EnumChoice<T>(T Value, string Label) where T : struct, Enum
    {
        public override string ToString() => Label;
    }
}
