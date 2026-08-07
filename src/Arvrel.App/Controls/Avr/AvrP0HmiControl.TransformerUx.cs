using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrP0HmiControl
{
    /// <summary>
    /// Applies the operator-facing transformer convention used by the AVR lab:
    /// tap positions are physical positions 1..N, with the neutral position
    /// shown explicitly instead of a signed offset around zero.
    /// </summary>
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

        // Keep the hierarchy compact inside the fixed physical chassis.
        TapHero.FontSize = 54;
        TapHero.MinWidth = 94;
        TapHero.TextAlignment = TextAlignment.Left;
        VoltageHero.FontSize = 38;
        HomePage.Margin = new Thickness(8, 6, 8, 5);
        TrendScale.FontSize = 7.0;

        // The electrical-value card previously sat at the top of a taller card,
        // leaving an untidy dead area below it. Center its content vertically so
        // Tap and U remain visually balanced at the device's fixed aspect ratio.
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