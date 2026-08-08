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
        var endRaise = snapshot.TapPosition >= settings.MaximumTap;
        var endLower = snapshot.TapPosition <= settings.MinimumTap;

        TapHero.Text = tap;
        MeasuredTap.Text = tap;
        TapRange.Text = $"{minimum} … {maximum} · N {neutral}";
        TapFeedback.Text = snapshot.PendingTapPosition is int pending
            ? $"TARGET {TapText(pending)} · {snapshot.MotorTravelRemainingSeconds:0.0}s"
            : endRaise
                ? "END RAISE · FEEDBACK VALID"
                : endLower
                    ? "END LOWER · FEEDBACK VALID"
                    : snapshot.TapPosition == settings.NeutralTap
                        ? "FEEDBACK VALID · NEUTRAL"
                        : $"FEEDBACK VALID · N {neutral}";
        ControlTapRange.Text = $"{minimum} … {maximum} · neutral {neutral} · {settings.TapStepPercent:0.###}%/step · EndR {(endRaise ? "ON" : "OFF")} · EndL {(endLower ? "ON" : "OFF")}";

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

        var limitState = endRaise ? " · END RAISE" : endLower ? " · END LOWER" : string.Empty;
        FooterReason.Text = snapshot.SourceEnergized
            ? snapshot.Reason + limitState
            : $"Simulated transformer · neutral tap {neutral} · source OFF{limitState}";
        BottomStatus.Text = snapshot.SourceEnergized
            ? $"{HomeAuthority.Text} · {HomeMode.Text} · TAP {tap}/{maximum}{limitState} · {StateText(snapshot.State).ToUpperInvariant()}"
            : $"SIMULATED TRANSFORMER · TAP {tap}/{maximum} · NEUTRAL {neutral}{limitState} · SOURCE OFF";
    }

    private static string TapText(int tap) => tap.ToString("00", CultureInfo.InvariantCulture);
}
