using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrP0HmiControl
{
    private bool _trendSmoothingInitialized;
    private double _smoothedTrendVoltage;

    internal void ApplyLowFpsTrendSmoothing(AvrSnapshot snapshot)
    {
        if (!snapshot.SourceEnergized)
        {
            _trendSmoothingInitialized = false;
            _smoothedTrendVoltage = 0;
            return;
        }

        // The process history is still sampled only by the existing divider
        // (~3.3 Hz). We smooth the Y value only when a real trend sample is
        // accepted; control logic, numeric LCD values and MMS values remain raw.
        if (_trendDivider % 3 == 0 && _trendHistory.Count > 0)
        {
            if (!_trendSmoothingInitialized)
            {
                _smoothedTrendVoltage = snapshot.MeasuredVoltageV;
                _trendSmoothingInitialized = true;
            }
            else
            {
                const double alpha = 0.34;
                _smoothedTrendVoltage += (snapshot.MeasuredVoltageV - _smoothedTrendVoltage) * alpha;
            }

            var samples = _trendHistory.ToArray();
            samples[^1] = _smoothedTrendVoltage;
            _trendHistory.Clear();
            foreach (var value in samples)
                _trendHistory.Enqueue(value);
        }

        // Horizontal motion is interpolated only at the existing UI tick
        // (~10 Hz). No animation clock, no 60-FPS loop and no extra timer.
        RenderFixedStepTrend();
    }

    private void RenderFixedStepTrend()
    {
        if (!IsLoaded || CompactTrendCanvas.ActualWidth <= 1 || CompactTrendCanvas.ActualHeight <= 1)
            return;

        var w = CompactTrendCanvas.ActualWidth;
        var h = CompactTrendCanvas.ActualHeight;
        var setpoint = _snapshot.EffectiveSetpointVoltageV > 0 ? _snapshot.EffectiveSetpointVoltageV : _settings.SetpointVoltageV;
        var lower = _snapshot.LowerBandVoltageV > 0 ? _snapshot.LowerBandVoltageV : setpoint * (1 - _settings.TolerancePercent / 100.0);
        var upper = _snapshot.UpperBandVoltageV > 0 ? _snapshot.UpperBandVoltageV : setpoint * (1 + _settings.TolerancePercent / 100.0);
        var values = _trendHistory.ToArray();
        var min = Math.Min(lower - 0.5, values.Length > 0 ? values.Min() - 0.5 : lower - 0.5);
        var max = Math.Max(upper + 0.5, values.Length > 0 ? values.Max() + 0.5 : upper + 0.5);
        if (max - min < 0.1)
            max = min + 0.1;

        double Y(double v) => Math.Clamp(h - ((v - min) / (max - min) * h), 0, h);
        SetLine(TrendUpper, w, Y(upper));
        SetLine(TrendSet, w, Y(setpoint));
        SetLine(TrendLower, w, Y(lower));
        TrendScale.Text = $"{lower:0.0}     {setpoint:0.0}     {upper:0.0} V";

        const int capacity = 90;
        var dx = w / Math.Max(1, capacity - 1);
        var phase = (_trendDivider % 3) / 3.0; // 0, 1/3, 2/3 between real samples.
        var scroll = phase * dx;
        var points = new PointCollection(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            var ageFromNewest = values.Length - 1 - i;
            var x = w - ageFromNewest * dx - scroll;
            if (x < -dx)
                continue;
            points.Add(new Point(Math.Max(0, x), Y(values[i])));
        }

        TrendPath.Points = points;
        if (points.Count > 0)
        {
            var p = points[^1];
            Canvas.SetLeft(TrendDot, Math.Clamp(p.X - 3.5, 0, Math.Max(0, w - 7)));
            Canvas.SetTop(TrendDot, Math.Clamp(p.Y - 3.5, 0, Math.Max(0, h - 7)));
            TrendDot.Visibility = Visibility.Visible;
        }
        else
        {
            TrendDot.Visibility = Visibility.Collapsed;
        }
    }
}
