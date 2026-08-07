using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private const double InitialPickupSeconds = 0.28;
    private const int TrendHistorySamples = 120;
    private readonly Queue<double> _hmiVoltageHistory = new();
    private bool _hmiTickAttached;
    private bool _hmiLastSourceEnergized;
    private bool _hmiFastPickupActive;
    private int _hmiTrendTick;

    private void HmiLoaded(object sender, RoutedEventArgs e)
    {
        if (!_hmiTickAttached)
        {
            _timer.Tick += HmiSupport_Tick;
            _hmiTickAttached = true;
        }

        _hmiLastSourceEnergized = _sourceEnergized;
        _hmiFastPickupActive = false;
        _hmiTrendTick = 0;
        InitializeHmiNavigation();
        UpdateTrendGraphic();
        RefreshHmiNavigation();
    }

    private void HmiUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hmiTickAttached)
        {
            _timer.Tick -= HmiSupport_Tick;
            _hmiTickAttached = false;
        }

        ShutdownHmiNavigation();
    }

    private void HmiSupport_Tick(object? sender, EventArgs e)
    {
        // A real test set reaches its armed steady-state output quickly. The user
        // configured ramp remains authoritative for subsequent scenario steps;
        // this boost applies only to the OFF -> ENERGIZED transition.
        if (_sourceEnergized && !_hmiLastSourceEnergized)
            _hmiFastPickupActive = true;
        else if (!_sourceEnergized)
            _hmiFastPickupActive = false;

        _hmiLastSourceEnergized = _sourceEnergized;

        if (_hmiFastPickupActive && _running && _sourceEnergized)
            AdvanceInitialPickup();

        // Drain accepted SAS controls on the same dispatcher/authority thread as
        // the virtual AVR. The socket runtime never mutates the regulator engine.
        ProcessIec61850ControlRuntime();

        // The IEC 61850 model follows the same authoritative AVR snapshot used by
        // the HMI. No independent network-side simulation state is introduced.
        PublishIec61850Snapshot();
        RefreshHmiNavigation();

        if (!_sourceEnergized)
        {
            if (_hmiVoltageHistory.Count > 0)
                _hmiVoltageHistory.Clear();
            _hmiTrendTick = 0;
            UpdateTrendGraphic();
            return;
        }

        // Keep the trend light: sample at roughly 3 Hz although the simulation
        // renderer runs at 10 Hz. This is enough for an AVR regulation trend and
        // avoids unnecessary WPF geometry churn.
        _hmiTrendTick++;
        if (_hmiTrendTick % 3 == 0)
        {
            _hmiVoltageHistory.Enqueue(_snapshot.MeasuredVoltageV);
            while (_hmiVoltageHistory.Count > TrendHistorySamples)
                _hmiVoltageHistory.Dequeue();
        }

        UpdateTrendGraphic();
    }

    private void AdvanceInitialPickup()
    {
        var voltageRate = Math.Max(300.0, Math.Abs(_injectionTargetVoltageV) / InitialPickupSeconds);
        var currentRate = Math.Max(10.0, Math.Abs(_injectionTargetCurrentA) / InitialPickupSeconds);
        var dt = Math.Max(0.01, _timer.Interval.TotalSeconds);

        _injectedVoltageV = HmiMoveToward(_injectedVoltageV, _injectionTargetVoltageV, voltageRate * dt);
        _injectedCurrentA = HmiMoveToward(_injectedCurrentA, _injectionTargetCurrentA, currentRate * dt);

        var voltageDone = Math.Abs(_injectedVoltageV - _injectionTargetVoltageV) <= 0.01;
        var currentDone = Math.Abs(_injectedCurrentA - _injectionTargetCurrentA) <= 0.002;
        if (voltageDone && currentDone)
            _hmiFastPickupActive = false;
    }

    private void UpdateTrendGraphic()
    {
        if (TrendCanvas is null || TrendPolyline is null)
            return;

        var width = Math.Max(80.0, TrendCanvas.ActualWidth);
        var height = Math.Max(80.0, TrendCanvas.ActualHeight);
        var setpoint = _snapshot.EffectiveSetpointVoltageV > 0
            ? _snapshot.EffectiveSetpointVoltageV
            : _settings.SetpointVoltageV;
        var lowerBand = _snapshot.LowerBandVoltageV > 0
            ? _snapshot.LowerBandVoltageV
            : setpoint * (1 - _settings.TolerancePercent / 100.0);
        var upperBand = _snapshot.UpperBandVoltageV > 0
            ? _snapshot.UpperBandVoltageV
            : setpoint * (1 + _settings.TolerancePercent / 100.0);

        var nominalSpan = Math.Max(2.0, setpoint * Math.Max(0.02, _settings.TolerancePercent / 100.0 * 2.2));
        var minValue = setpoint - nominalSpan;
        var maxValue = setpoint + nominalSpan;

        if (_hmiVoltageHistory.Count > 0)
        {
            minValue = Math.Min(minValue, _hmiVoltageHistory.Min() - 0.5);
            maxValue = Math.Max(maxValue, _hmiVoltageHistory.Max() + 0.5);
        }

        if (maxValue - minValue < 0.1)
            maxValue = minValue + 0.1;

        double Y(double value) =>
            Math.Clamp(height - ((value - minValue) / (maxValue - minValue) * height), 0, height);

        SetHorizontalLine(TrendUpperLine, width, Y(upperBand));
        SetHorizontalLine(TrendSetpointLine, width, Y(setpoint));
        SetHorizontalLine(TrendLowerLine, width, Y(lowerBand));

        TrendLowerLabel.Text = $"{lowerBand:0.0} V";
        TrendSetpointLabel.Text = $"{setpoint:0.0} V";
        TrendUpperLabel.Text = $"{upperBand:0.0} V";

        var values = _hmiVoltageHistory.ToArray();
        var points = new PointCollection(values.Length);
        if (values.Length > 0)
        {
            var denominator = Math.Max(1, values.Length - 1);
            for (var i = 0; i < values.Length; i++)
            {
                var x = i / (double)denominator * width;
                points.Add(new Point(x, Y(values[i])));
            }

            var last = points[points.Count - 1];
            Canvas.SetLeft(TrendValueDot, Math.Clamp(last.X - TrendValueDot.Width / 2.0, 0, width - TrendValueDot.Width));
            Canvas.SetTop(TrendValueDot, Math.Clamp(last.Y - TrendValueDot.Height / 2.0, 0, height - TrendValueDot.Height));
            TrendValueDot.Visibility = Visibility.Visible;
        }
        else
        {
            TrendValueDot.Visibility = Visibility.Collapsed;
        }

        TrendPolyline.Points = points;
    }

    private static void SetHorizontalLine(System.Windows.Shapes.Line line, double width, double y)
    {
        line.X1 = 0;
        line.X2 = width;
        line.Y1 = y;
        line.Y2 = y;
    }

    private static double HmiMoveToward(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
            return target;
        return current + Math.Sign(target - current) * maxDelta;
    }
}
