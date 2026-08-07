using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.Application.Laboratory;

namespace Arvrel.App;

internal partial class CtObservabilityWindow : Window
{
    private readonly ObservableCollection<CtObservationRow> _rows = new();
    private readonly Action _resetState;
    private readonly Action _demagnetize;

    public CtObservabilityWindow(Action resetState, Action demagnetize)
    {
        _resetState = resetState ?? throw new ArgumentNullException(nameof(resetState));
        _demagnetize = demagnetize ?? throw new ArgumentNullException(nameof(demagnetize));

        InitializeComponent();
        Table.ItemsSource = _rows;
        Table.SelectionChanged += Table_SelectionChanged;
    }

    public void Update(CtObservabilitySnapshot snapshot, CtComparisonFrame frame)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(frame);

        BadgeText.Text = snapshot.BadgeText;
        SetBadge(snapshot.Status);
        StatusText.Text = snapshot.StatusText;
        SettingsText.Text = snapshot.SettingsSummary;
        RuntimeText.Text = snapshot.RuntimeSummary;
        BoundaryText.Text = snapshot.EngineeringBoundary;
        ResetButton.IsEnabled = snapshot.CanResetState;
        DemagnetizeButton.IsEnabled = snapshot.CanDemagnetize;

        var previousChannel = (Table.SelectedItem as CtObservationRow)?.Channel;
        _rows.Clear();
        foreach (var channel in snapshot.Channels)
            _rows.Add(CtObservationRow.From(channel));

        if (_rows.Count > 0)
        {
            Table.SelectedItem = _rows.FirstOrDefault(row => row.Channel == previousChannel) ?? _rows[0];
        }

        Scope.Frame = frame;
        if (Table.SelectedItem is CtObservationRow selected)
            Scope.SelectedChannel = selected.Channel;

        UpdateCriticalMetrics(snapshot);
    }

    private void UpdateCriticalMetrics(CtObservabilitySnapshot snapshot)
    {
        var focus = snapshot.Channels.FirstOrDefault(channel => channel.Available && channel.Saturated)
                    ?? snapshot.Channels.FirstOrDefault(channel => channel.Available);
        if (focus is null)
        {
            MetricChannelText.Text = "—";
            MetricFluxText.Text = "—";
            MetricRmsText.Text = "—";
            MetricRatioText.Text = "—";
            MetricWaveText.Text = "—";
            return;
        }

        MetricChannelText.Text = $"{focus.Channel} · {focus.State}";
        MetricFluxText.Text = FormatPu(focus.MaximumAbsoluteFluxPerUnit);
        MetricRmsText.Text = FormattableString.Invariant($"{focus.SecondaryRmsA:0.###} A");
        MetricRatioText.Text = FormattableString.Invariant($"{focus.RmsMagnitudeErrorPercent:+0.##;-0.##;0}%");

        var wave = FormattableString.Invariant($"{focus.WaveformErrorPercent:0.##}%");
        MetricWaveText.Text = focus.Saturated && focus.FirstSaturationAbsoluteSample >= 0
            ? FormattableString.Invariant($"{wave} · {focus.FirstSaturationMilliseconds:0.###} ms")
            : wave;
    }

    private void Table_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Table.SelectedItem is CtObservationRow selected)
            Scope.SelectedChannel = selected.Channel;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
        => _resetState();

    private void DemagnetizeButton_Click(object sender, RoutedEventArgs e)
        => _demagnetize();

    private void SetBadge(CtObservabilityStatus status)
    {
        var (foreground, background, border) = status switch
        {
            CtObservabilityStatus.Saturated => ("#B43F3A", "#FCEAEA", "#E5B6B3"),
            CtObservabilityStatus.Nonlinear => ("#A36B13", "#FBF2E3", "#E2C58F"),
            _ => ("#3F8750", "#EAF5EC", "#B9D8BF")
        };
        BadgeText.Foreground = ColorBrush(foreground);
        Badge.Background = ColorBrush(background);
        Badge.BorderBrush = ColorBrush(border);
    }

    private static Brush ColorBrush(string color)
        => (Brush)new BrushConverter().ConvertFromString(color)!;

    private sealed record CtObservationRow(
        string Channel,
        string State,
        string InitialFlux,
        string FinalFlux,
        string MaximumFlux,
        string Rms,
        string RatioError,
        string WaveformError,
        string Excitation,
        string SecondaryVoltage,
        string Onset)
    {
        public static CtObservationRow From(CtChannelObservation source)
        {
            if (!source.Available)
            {
                return new CtObservationRow(
                    source.Channel,
                    source.State,
                    "—",
                    "—",
                    "—",
                    "phase sum",
                    "—",
                    "—",
                    "—",
                    "—",
                    "—");
            }

            var onset = source.Saturated && source.FirstSaturationAbsoluteSample >= 0
                ? FormattableString.Invariant($"{source.FirstSaturationMilliseconds:0.###} ms / #{source.FirstSaturationAbsoluteSample:N0}")
                : "—";
            return new CtObservationRow(
                source.Channel,
                source.State,
                FormatPu(source.InitialFluxPerUnit),
                FormatPu(source.FinalFluxPerUnit),
                FormatPu(source.MaximumAbsoluteFluxPerUnit),
                FormattableString.Invariant($"{source.IdealRmsA:0.###} → {source.SecondaryRmsA:0.###} A"),
                FormattableString.Invariant($"{source.RmsMagnitudeErrorPercent:+0.##;-0.##;0}%"),
                FormattableString.Invariant($"{source.WaveformErrorPercent:0.##}%"),
                FormattableString.Invariant($"{source.MaximumExcitationCurrentA:0.###} A"),
                FormattableString.Invariant($"{source.MaximumSecondaryVoltageV:0.###} V"),
                onset);
        }
    }

    private static string FormatPu(double value)
        => FormattableString.Invariant($"{value:+0.###;-0.###;0} pu");
}
