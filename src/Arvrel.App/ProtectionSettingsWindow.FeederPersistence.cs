using Arvrel.Protection;

namespace Arvrel.App;

public partial class ProtectionSettingsWindow
{
    private void LoadFeederSettings(FeederProtectionSettings settings)
    {
        _undervoltageEnabled.IsChecked = settings.Undervoltage27Enabled;
        _undervoltagePickup.Text = Format(settings.Undervoltage27PickupV);
        _undervoltageDelay.Text = FormatTime(settings.Undervoltage27Delay);
        _undervoltageReset.Text = Format(settings.Undervoltage27ResetRatio);
        SelectFeederEnum(_undervoltageMode, settings.Undervoltage27Mode);
        SelectFeederEnum(_undervoltageLogic, settings.Undervoltage27Logic);

        _overvoltageEnabled.IsChecked = settings.Overvoltage59Enabled;
        _overvoltagePickup.Text = Format(settings.Overvoltage59PickupV);
        _overvoltageDelay.Text = FormatTime(settings.Overvoltage59Delay);
        _overvoltageDropout.Text = Format(settings.Overvoltage59DropoutRatio);
        SelectFeederEnum(_overvoltageMode, settings.Overvoltage59Mode);
        SelectFeederEnum(_overvoltageLogic, settings.Overvoltage59Logic);

        _residualOvervoltageEnabled.IsChecked = settings.ResidualOvervoltage59NEnabled;
        _residualOvervoltagePickup.Text = Format(settings.ResidualOvervoltage59NPickupV);
        _residualOvervoltageDelay.Text = FormatTime(settings.ResidualOvervoltage59NDelay);
        _residualOvervoltageDropout.Text = Format(settings.ResidualOvervoltage59NDropoutRatio);

        _directionalPhaseEnabled.IsChecked = settings.DirectionalPhase67Enabled;
        _directionalPhasePickup.Text = Format(settings.DirectionalPhase67PickupA);
        _directionalPhaseDelay.Text = FormatTime(settings.DirectionalPhase67Delay);
        _directionalPhaseDropout.Text = Format(settings.DirectionalPhase67DropoutRatio);
        _directionalPhaseMta.Text = Format(settings.DirectionalPhase67CharacteristicAngleDeg);
        _directionalPhaseMinimumVoltage.Text = Format(settings.DirectionalPhase67MinimumPolarizingVoltageV);
        SelectFeederEnum(_directionalPhaseSense, settings.DirectionalPhase67Sense);

        _directionalEarthEnabled.IsChecked = settings.DirectionalEarth67NEnabled;
        _directionalEarthPickup.Text = Format(settings.DirectionalEarth67NPickupA);
        _directionalEarthDelay.Text = FormatTime(settings.DirectionalEarth67NDelay);
        _directionalEarthDropout.Text = Format(settings.DirectionalEarth67NDropoutRatio);
        _directionalEarthMta.Text = Format(settings.DirectionalEarth67NCharacteristicAngleDeg);
        _directionalEarthMinimumVoltage.Text = Format(settings.DirectionalEarth67NMinimumPolarizingVoltageV);
        SelectFeederEnum(_directionalEarthSense, settings.DirectionalEarth67NSense);
    }

    private static void SelectFeederEnum<T>(System.Windows.Controls.ComboBox combo, T value) where T : struct, Enum
    {
        if (combo.ItemsSource is not IEnumerable<EnumChoice<T>> options)
            return;
        combo.SelectedItem = options.First(option => EqualityComparer<T>.Default.Equals(option.Value, value));
    }
}
