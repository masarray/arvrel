using System.Globalization;
using System.Windows;
using Arvrel.ProcessBus;

namespace Arvrel.App;

public partial class MeasurementContextWindow : Window
{
    public MeasurementContextWindow(SmvMeasurementContext context)
    {
        InitializeComponent();
        PrimaryText.Text = context.CtPrimaryA.ToString("0.###", CultureInfo.InvariantCulture);
        SecondaryText.Text = context.CtSecondaryA.ToString("0.###", CultureInfo.InvariantCulture);
        FrequencyText.Text = context.NominalFrequencyHz.ToString("0.###", CultureInfo.InvariantCulture);
        UpdateRatio();
        PrimaryText.TextChanged += (_, _) => UpdateRatio();
        SecondaryText.TextChanged += (_, _) => UpdateRatio();
    }

    public SmvMeasurementContext? Result { get; private set; }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var context, out var error))
        {
            ValidationText.Text = error;
            return;
        }

        Result = context;
        DialogResult = true;
    }

    private void UpdateRatio()
    {
        if (TryRead(out var context, out _))
            RatioText.Text = $"CT ratio {context.PrimaryRatio:0.###}:1 · secondary protection domain · primary display multiplier {context.PrimaryRatio:0.###}";
        else
            RatioText.Text = "Enter positive numeric CT values and a nominal frequency from 45 to 65 Hz.";
    }

    private bool TryRead(out SmvMeasurementContext context, out string error)
    {
        context = new SmvMeasurementContext();
        error = string.Empty;
        if (!double.TryParse(PrimaryText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var primary) || primary <= 0)
        {
            error = "CT primary must be a positive number.";
            return false;
        }
        if (!double.TryParse(SecondaryText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var secondary) || secondary <= 0)
        {
            error = "CT secondary must be a positive number.";
            return false;
        }
        if (!double.TryParse(FrequencyText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var frequency) || frequency is < 45 or > 65)
        {
            error = "Frequency must be between 45 and 65 Hz.";
            return false;
        }

        context = new SmvMeasurementContext
        {
            CtPrimaryA = primary,
            CtSecondaryA = secondary,
            NominalFrequencyHz = frequency
        };
        return true;
    }
}
