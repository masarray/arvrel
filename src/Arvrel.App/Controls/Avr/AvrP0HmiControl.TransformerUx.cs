using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrP0HmiControl
{
    internal void ApplyTransformerConvention(AvrSnapshot snapshot, AvrSettings settings)
    {
        var tap = TapText(snapshot.TapPosition);
        var neutral = TapText(settings.NeutralTap);
        var minimum = TapText(settings.MinimumTap);
        var maximum = TapText(settings.MaximumTap);

        TapHero.Text = tap;
        MeasuredTap.Text = tap;
        TapRange.Text = $"{minimum} … {maximum} · N {neutral}";
        TapFeedback.Text = snapshot.PendingTapPosition is int pending
            ? $"TARGET {TapText(pending)} · {snapshot.MotorTravelRemainingSeconds:0.0}s"
            : snapshot.TapPosition == settings.NeutralTap
                ? "FEEDBACK VALID · NEUTRAL"
                : $"FEEDBACK VALID · N {neutral}";
        ControlTapRange.Text = $"{minimum} … {maximum} · neutral {neutral} · {settings.TapStepPercent:0.###}%/step";

        // Fixed hardware display: preserve breathing room around the actual-voltage
        // unit/setpoint column rather than letting the hero value collide with it.
        TapHero.FontSize = 52;
        TapHero.MinWidth = 90;
        TapHero.TextAlignment = TextAlignment.Left;
        VoltageHero.FontSize = 34;
        HomePage.Margin = new Thickness(7, 5, 7, 5);
        TrendScale.FontSize = 7.0;

        if (VoltageHero.Parent is StackPanel voltageLine &&
            voltageLine.Parent is StackPanel voltageBlock &&
            voltageBlock.Parent is Grid voltageTopGrid &&
            voltageTopGrid.Parent is Grid measurementGrid)
        {
            measurementGrid.VerticalAlignment = VerticalAlignment.Center;
        }

        FooterReason.Text = snapshot.SourceEnergized
            ? snapshot.Reason
            : $"Simulated transformer · neutral tap {neutral} · source OFF";
        BottomStatus.Text = snapshot.SourceEnergized
            ? $"{HomeAuthority.Text} · {HomeMode.Text} · TAP {tap}/{maximum} · {StateText(snapshot.State).ToUpperInvariant()}"
            : $"SIMULATED TRANSFORMER · TAP {tap}/{maximum} · NEUTRAL {neutral} · SOURCE OFF";
    }

    private static string TapText(int tap) => tap.ToString("00", CultureInfo.InvariantCulture);
}
