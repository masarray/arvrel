using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class ProtectionSettingsWindow
{
    private bool _familiarOvercurrentUxApplied;

    private void ApplyFamiliarOvercurrentUx()
    {
        if (_familiarOvercurrentUxApplied)
            return;

        _familiarOvercurrentUxApplied = true;
        Width = Math.Max(Width, 1120);
        MinWidth = Math.Max(MinWidth, 1000);

        Phase51Enabled.Content = "51P phase overcurrent I>";
        Phase51Enabled.ToolTip = "Time-delayed phase overcurrent element, using IEC inverse or definite-time operation.";
        Earth51Enabled.Content = "51N earth-fault overcurrent I0>";
        Earth51Enabled.ToolTip = "Time-delayed residual/neutral overcurrent element, using IEC inverse or definite-time operation.";

        ConfigureTimeOvercurrentRows(
            Phase51PickupText,
            Phase51CurveCombo,
            Phase51TmsText,
            Phase51DefiniteText,
            Phase51MinimumText,
            Phase51DropoutText,
            Phase51ResetCombo,
            Phase51ResetTimeText,
            Phase51KText,
            pickupLabel: "Pickup current I>",
            pickupHelp: "Secondary RMS phase-current threshold at which 51P starts. Primary equivalent is shown below the card.");

        ConfigureTimeOvercurrentRows(
            Earth51PickupText,
            Earth51CurveCombo,
            Earth51TmsText,
            Earth51DefiniteText,
            Earth51MinimumText,
            Earth51DropoutText,
            Earth51ResetCombo,
            Earth51ResetTimeText,
            Earth51KText,
            pickupLabel: "Earth pickup I0>",
            pickupHelp: "Secondary residual/neutral-current threshold at which 51N starts. ARVREL uses mapped IN or calculated IA+IB+IC.");

        Phase51CurveCombo.SelectionChanged += FamiliarCurve_SelectionChanged;
        Earth51CurveCombo.SelectionChanged += FamiliarCurve_SelectionChanged;
        UpdateAdvancedCurveRows();

        Phase51PreviewText.ToolTip = "Calculated operate times at 2, 5 and 10 multiples of pickup. This is the quickest way to verify the selected curve and TMS.";
        Earth51PreviewText.ToolTip = Phase51PreviewText.ToolTip;
        ValidationText.TextTrimming = TextTrimming.CharacterEllipsis;
        BindingOperations.SetBinding(
            ValidationText,
            FrameworkElement.ToolTipProperty,
            new Binding(nameof(TextBlock.Text))
            {
                Source = ValidationText,
                Mode = BindingMode.OneWay
            });
    }

    private void ConfigureTimeOvercurrentRows(
        TextBox pickup,
        ComboBox curve,
        TextBox tms,
        TextBox definiteDelay,
        TextBox minimumTime,
        TextBox dropout,
        ComboBox resetMode,
        TextBox resetTime,
        TextBox userCurveK,
        string pickupLabel,
        string pickupHelp)
    {
        RenameRow(pickup, pickupLabel, pickupHelp);
        RenameRow(curve, "Curve type", "IEC curve family. Standard inverse, very inverse, extremely inverse and long-time inverse follow the active IEC-form calculator.");
        RenameRow(tms, "Time multiplier (TMS)", "Time Multiplier Setting. Increasing TMS proportionally increases inverse-curve operate time.");
        RenameRow(definiteDelay, "Definite time tI>", "Used when Curve type is Definite Time. Enter milliseconds by default, or add an 's' suffix for seconds.");
        RenameRow(minimumTime, "Minimum operate tMin", "Lower time limit applied after the curve calculation. Enter milliseconds by default, or add an 's' suffix.");
        RenameRow(dropout, "Drop-off / pick-up ratio", "Current reset threshold as a ratio of pickup. Example: 0.95 resets below 95% of pickup.");
        RenameRow(resetMode, "Reset mode", "Instantaneous reset, definite-time reset, or inverse-memory reset.");
        RenameRow(resetTime, "Reset time tReset", "Reset delay/memory time. Enter milliseconds by default, or add an 's' suffix for seconds.");
        RenameRow(userCurveK, "Advanced curve K / α / C", "Visible only for User-defined IEC-form curve. Standard relay users normally leave this hidden.");

        curve.Width = 238;
        resetMode.Width = 238;
        foreach (var field in new[] { pickup, tms, definiteDelay, minimumTime, dropout, resetTime })
            field.Width = 118;
    }

    private static void RenameRow(FrameworkElement field, string label, string help)
    {
        if (field.Parent is not Grid row)
            return;

        if (row.ColumnDefinitions.Count > 0)
            row.ColumnDefinitions[0].Width = new GridLength(190);

        var labelControl = row.Children
            .OfType<TextBlock>()
            .FirstOrDefault(text => Grid.GetColumn(text) == 0);
        if (labelControl is not null)
        {
            labelControl.Text = label;
            labelControl.TextTrimming = TextTrimming.CharacterEllipsis;
            labelControl.ToolTip = help;
        }

        field.ToolTip = help;
    }

    private void FamiliarCurve_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateAdvancedCurveRows();

    private void UpdateAdvancedCurveRows()
    {
        SetAdvancedCurveRow(
            Phase51KText,
            Phase51CurveCombo.SelectedItem is EnumOption<IecCurveFamily> phase && phase.Value == IecCurveFamily.UserDefined);
        SetAdvancedCurveRow(
            Earth51KText,
            Earth51CurveCombo.SelectedItem is EnumOption<IecCurveFamily> earth && earth.Value == IecCurveFamily.UserDefined);
    }

    private static void SetAdvancedCurveRow(FrameworkElement field, bool visible)
    {
        if (field.Parent is not StackPanel panel || panel.Parent is not Grid row)
            return;

        row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (row.Parent is Grid card && Grid.GetRow(row) is var rowIndex && rowIndex >= 0 && rowIndex < card.RowDefinitions.Count)
            card.RowDefinitions[rowIndex].Height = visible ? new GridLength(34) : new GridLength(0);
    }
}
