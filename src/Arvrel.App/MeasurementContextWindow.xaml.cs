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
        VoltagePrimaryText.Text = context.VtPrimaryV.ToString("0.###", CultureInfo.InvariantCulture);
        VoltageSecondaryText.Text = context.VtSecondaryV.ToString("0.###", CultureInfo.InvariantCulture);
        FrequencyText.Text = context.NominalFrequencyHz.ToString("0.###", CultureInfo.InvariantCulture);
        UpdateRatio();
        PrimaryText.TextChanged += (_, _) => UpdateRatio();
        SecondaryText.TextChanged += (_, _) => UpdateRatio();
        VoltagePrimaryText.TextChanged += (_, _) => UpdateRatio();
        VoltageSecondaryText.TextChanged += (_, _) => UpdateRatio();
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
        {
            RatioText.Text =
                $"CT {context.CtPrimaryA:0.###}/{context.CtSecondaryA:0.###} A · ratio {context.PrimaryRatio:0.###}:1\n" +
                $"VT {context.VtPrimaryV:0.###}/{context.VtSecondaryV:0.###} V · ratio {context.VoltagePrimaryRatio:0.###}:1\n" +
                $"Protection domain: secondary A/V · {context.NominalFrequencyHz:0.###} Hz";
        }
        else
        {
            RatioText.Text = "Enter positive CT and VT values and a nominal frequency from 45 to 65 Hz.";
        }
    }

    private bool TryRead(out SmvMeasurementContext context, out string error)
    {
        context = new SmvMeasurementContext();
        error = string.Empty;
        if (!TryPositive(PrimaryText.Text, out var primary))
        {
            error = "CT primary must be a positive number.";
            return false;
        }
        if (!TryPositive(SecondaryText.Text, out var secondary))
        {
            error = "CT secondary must be a positive number.";
            return false;
        }
        if (!TryPositive(VoltagePrimaryText.Text, out var voltagePrimary))
        {
            error = "VT primary must be a positive number.";
            return false;
        }
        if (!TryPositive(VoltageSecondaryText.Text, out var voltageSecondary))
        {
            error = "VT secondary must be a positive number.";
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
            VtPrimaryV = voltagePrimary,
            VtSecondaryV = voltageSecondary,
            NominalFrequencyHz = frequency
        };
        return true;
    }

    private static bool TryPositive(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
           double.IsFinite(value) &&
           value > 0;
}
