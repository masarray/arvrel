using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Arvrel.App.Controls;
using Arvrel.Application.Laboratory;

namespace Arvrel.App;

internal sealed class CtObservabilityWindow : Window
{
    private readonly ObservableCollection<CtObservationRow> _rows = new();
    private readonly TextBlock _badgeText;
    private readonly Border _badge;
    private readonly TextBlock _statusText;
    private readonly TextBlock _settingsText;
    private readonly TextBlock _runtimeText;
    private readonly TextBlock _boundaryText;
    private readonly DataGrid _table;
    private readonly CtComparisonScope _scope;
    private readonly Button _resetButton;
    private readonly Button _demagnetizeButton;
    private readonly Action _resetState;
    private readonly Action _demagnetize;

    public CtObservabilityWindow(Action resetState, Action demagnetize)
    {
        _resetState = resetState ?? throw new ArgumentNullException(nameof(resetState));
        _demagnetize = demagnetize ?? throw new ArgumentNullException(nameof(demagnetize));

        Title = "ARVREL · CT Observability";
        Width = 1040;
        Height = 690;
        MinWidth = 820;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 247, 249));

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(header);

        _badgeText = new TextBlock
        {
            Text = "CT IDEAL",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _badge = new Border
        {
            Padding = new Thickness(10, 6, 10, 6),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = _badgeText,
            VerticalAlignment = VerticalAlignment.Top
        };
        header.Children.Add(_badge);

        var headerText = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(headerText, 1);
        header.Children.Add(headerText);
        headerText.Children.Add(new TextBlock
        {
            Text = "CURRENT TRANSFORMER OBSERVABILITY",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(35, 49, 60))
        });
        _statusText = new TextBlock
        {
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(75, 91, 104)),
            TextWrapping = TextWrapping.Wrap
        };
        headerText.Children.Add(_statusText);

        var facts = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 220, 227)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(facts, 1);
        root.Children.Add(facts);
        var factsStack = new StackPanel();
        facts.Child = factsStack;
        _settingsText = FactText();
        _runtimeText = FactText();
        factsStack.Children.Add(_settingsText);
        factsStack.Children.Add(_runtimeText);

        _table = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = false,
            CanUserSortColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(247, 250, 252)),
            RowBackground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 220, 227)),
            BorderThickness = new Thickness(1),
            RowHeight = 31,
            ColumnHeaderHeight = 31,
            Margin = new Thickness(0, 0, 0, 10)
        };
        AddColumn("Channel", nameof(CtObservationRow.Channel), 72);
        AddColumn("State", nameof(CtObservationRow.State), 104);
        AddColumn("Flux start", nameof(CtObservationRow.InitialFlux), 86);
        AddColumn("Flux end", nameof(CtObservationRow.FinalFlux), 86);
        AddColumn("Flux max", nameof(CtObservationRow.MaximumFlux), 86);
        AddColumn("Ideal → sec RMS", nameof(CtObservationRow.Rms), 130);
        AddColumn("Ratio err", nameof(CtObservationRow.RatioError), 86);
        AddColumn("Wave err", nameof(CtObservationRow.WaveformError), 86);
        AddColumn("Excitation", nameof(CtObservationRow.Excitation), 92);
        AddColumn("V sec max", nameof(CtObservationRow.SecondaryVoltage), 92);
        AddColumn("Onset", nameof(CtObservationRow.Onset), 108);
        Grid.SetRow(_table, 2);
        root.Children.Add(_table);

        _scope = new CtComparisonScope();
        _table.SelectionChanged += (_, _) =>
        {
            if (_table.SelectedItem is CtObservationRow selected)
                _scope.SelectedChannel = selected.Channel;
        };
        Grid.SetRow(_scope, 3);
        root.Children.Add(_scope);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        _boundaryText = new TextBlock
        {
            FontSize = 9.5,
            Foreground = new SolidColorBrush(Color.FromRgb(108, 122, 133)),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        footer.Children.Add(_boundaryText);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);

        _resetButton = ActionButton("Reset CT state", "Reapply configured signed remanence while keeping source phase and DC time continuous.");
        _resetButton.Click += (_, _) => _resetState();
        actions.Children.Add(_resetButton);

        _demagnetizeButton = ActionButton("Demagnetize CT", "Set runtime CT flux to zero without changing configured remanence or source event time.");
        _demagnetizeButton.Margin = new Thickness(6, 0, 0, 0);
        _demagnetizeButton.Click += (_, _) => _demagnetize();
        actions.Children.Add(_demagnetizeButton);
    }

    public void Update(CtObservabilitySnapshot snapshot, CtComparisonFrame frame)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(frame);

        _badgeText.Text = snapshot.BadgeText;
        SetBadge(snapshot.Status);
        _statusText.Text = snapshot.StatusText;
        _settingsText.Text = snapshot.SettingsSummary;
        _runtimeText.Text = snapshot.RuntimeSummary;
        _boundaryText.Text = snapshot.EngineeringBoundary;
        _resetButton.IsEnabled = snapshot.CanResetState;
        _demagnetizeButton.IsEnabled = snapshot.CanDemagnetize;

        _rows.Clear();
        foreach (var channel in snapshot.Channels)
            _rows.Add(CtObservationRow.From(channel));
        if (_rows.Count > 0)
            _table.SelectedIndex = Math.Clamp(_table.SelectedIndex, 0, _rows.Count - 1);

        _scope.Frame = frame;
        if (_table.SelectedItem is CtObservationRow selected)
            _scope.SelectedChannel = selected.Channel;
    }

    private void AddColumn(string header, string path, double width)
        => _table.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            Binding = new Binding(path)
        });

    private void SetBadge(CtObservabilityStatus status)
    {
        var (foreground, background, border) = status switch
        {
            CtObservabilityStatus.Saturated => ("#B43F3A", "#FCEAEA", "#E5B6B3"),
            CtObservabilityStatus.Nonlinear => ("#A36B13", "#FBF2E3", "#E2C58F"),
            _ => ("#3F8750", "#EAF5EC", "#B9D8BF")
        };
        _badgeText.Foreground = ColorBrush(foreground);
        _badge.Background = ColorBrush(background);
        _badge.BorderBrush = ColorBrush(border);
    }

    private static TextBlock FactText()
        => new()
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10.2,
            Foreground = new SolidColorBrush(Color.FromRgb(63, 79, 91)),
            Margin = new Thickness(0, 1, 0, 1),
            TextWrapping = TextWrapping.Wrap
        };

    private static Button ActionButton(string text, string tooltip)
        => new()
        {
            Content = text,
            MinWidth = 126,
            Height = 30,
            Padding = new Thickness(10, 3, 10, 3),
            ToolTip = tooltip
        };

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

        private static string FormatPu(double value)
            => FormattableString.Invariant($"{value:+0.###;-0.###;0} pu");
    }
}
