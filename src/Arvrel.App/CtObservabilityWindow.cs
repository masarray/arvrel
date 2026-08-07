using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.App.Controls;
using Arvrel.Application.Laboratory;

namespace Arvrel.App;

public partial class CtObservabilityWindow : Window
{
    private readonly ObservableCollection<CtObservationRow> _rows = new();
    private readonly Action _restartEvent;
    private readonly Action _resetState;
    private readonly Action _demagnetize;

    public CtObservabilityWindow(Action restartEvent, Action resetState, Action demagnetize)
    {
        _restartEvent = restartEvent ?? throw new ArgumentNullException(nameof(restartEvent));
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
        EventText.Text = snapshot.EventSummary;
        BoundaryText.Text = snapshot.EngineeringBoundary +
            "  Source preset and CT model are selected independently in the INJECT workspace.";
        RestartEventButton.IsEnabled = snapshot.CanRestartEvent;
        ResetButton.IsEnabled = snapshot.CanResetState;
        DemagnetizeButton.IsEnabled = snapshot.CanDemagnetize;

        var previousChannel = (Table.SelectedItem as CtObservationRow)?.Channel;
        _rows.Clear();
        foreach (var channel in snapshot.Channels)
            _rows.Add(CtObservationRow.From(channel));

        if (_rows.Count > 0)
            Table.SelectedItem = _rows.FirstOrDefault(row => row.Channel == previousChannel) ?? _rows[0];

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
            MetricFundamentalRmsText.Text = "—";
            MetricFundamentalErrorText.Text = "—";
            MetricPhaseText.Text = "—";
            MetricWaveText.Text = "—";
            return;
        }

        MetricChannelText.Text = $"{focus.Channel} · {focus.State}";
        MetricFluxText.Text = FormatPu(focus.MaximumAbsoluteFluxPerUnit);
        MetricFundamentalRmsText.Text = FormatRmsPair(focus.FundamentalIdealRmsA, focus.FundamentalSecondaryRmsA);
        MetricFundamentalErrorText.Text = FormatPercent(focus.FundamentalMagnitudeErrorPercent);
        MetricPhaseText.Text = FormatDegrees(focus.PhaseDisplacementDegrees);

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

    private void RestartEventButton_Click(object sender, RoutedEventArgs e)
        => _restartEvent();

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
        string FluxRange,
        string MaximumFlux,
        string TotalRms,
        string FundamentalRms,
        string FundamentalMagnitudeError,
        string PhaseDisplacement,
        string WaveformError,
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
                    "phase sum",
                    "phase sum",
                    "—",
                    "—",
                    "—",
                    "—",
                    "—");
            }

            var onset = source.Saturated && source.FirstSaturationAbsoluteSample >= 0
                ? FormattableString.Invariant($"{source.FirstSaturationMilliseconds:0.###} ms · #{source.FirstSaturationAbsoluteSample:N0}")
                : "—";
            return new CtObservationRow(
                source.Channel,
                source.State,
                FormattableString.Invariant($"{source.InitialFluxPerUnit:+0.##;-0.##;0} → {source.FinalFluxPerUnit:+0.##;-0.##;0} pu"),
                FormatPu(source.MaximumAbsoluteFluxPerUnit),
                FormattableString.Invariant($"{source.IdealRmsA:0.###} → {source.SecondaryRmsA:0.###} A"),
                FormatRmsPair(source.FundamentalIdealRmsA, source.FundamentalSecondaryRmsA),
                FormatPercent(source.FundamentalMagnitudeErrorPercent),
                FormatDegrees(source.PhaseDisplacementDegrees),
                FormattableString.Invariant($"{source.WaveformErrorPercent:0.##}%"),
                FormattableString.Invariant($"{source.MaximumSecondaryVoltageV:0.###} V"),
                onset);
        }
    }

    private static string FormatPu(double value)
        => FormattableString.Invariant($"{value:+0.###;-0.###;0} pu");

    private static string FormatRmsPair(double ideal, double secondary)
        => double.IsFinite(ideal) && double.IsFinite(secondary)
            ? FormattableString.Invariant($"{ideal:0.###} → {secondary:0.###} A")
            : "—";

    private static string FormatPercent(double value)
        => double.IsFinite(value)
            ? FormattableString.Invariant($"{value:+0.##;-0.##;0}%")
            : "—";

    private static string FormatDegrees(double value)
        => double.IsFinite(value)
            ? FormattableString.Invariant($"{value:+0.###;-0.###;0}°")
            : "—";
}
