using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Arvrel.Desktop.Controls;

public enum RelayLcdPage
{
    Measure,
    Events,
    Records,
    Setup,
    Diagnostics
}

public sealed partial class RelayLcdControl : UserControl
{
    private readonly Control? _measurePage;
    private readonly Control? _eventsPage;
    private readonly Control? _recordPage;
    private readonly Control? _setupPage;
    private readonly Control? _diagnosticsPage;
    private readonly TextBlock? _headerState;

    public RelayLcdControl()
    {
        InitializeComponent();
        _measurePage = this.FindControl<Control>("MeasurePage");
        _eventsPage = this.FindControl<Control>("EventsPage");
        _recordPage = this.FindControl<Control>("RecordPage");
        _setupPage = this.FindControl<Control>("SetupPage");
        _diagnosticsPage = this.FindControl<Control>("DiagnosticsPage");
        _headerState = this.FindControl<TextBlock>("HeaderStateText");
        ShowPage(RelayLcdPage.Measure);
    }

    public RelayLcdPage CurrentPage { get; private set; }

    public void ShowPage(RelayLcdPage page)
    {
        CurrentPage = page;
        if (_measurePage is not null)
            _measurePage.IsVisible = page == RelayLcdPage.Measure;
        if (_eventsPage is not null)
            _eventsPage.IsVisible = page == RelayLcdPage.Events;
        if (_recordPage is not null)
            _recordPage.IsVisible = page == RelayLcdPage.Records;
        if (_setupPage is not null)
            _setupPage.IsVisible = page == RelayLcdPage.Setup;
        if (_diagnosticsPage is not null)
            _diagnosticsPage.IsVisible = page == RelayLcdPage.Diagnostics;
        if (_headerState is not null)
            _headerState.Text = page switch
            {
                RelayLcdPage.Events => "EVENT LOG",
                RelayLcdPage.Records => "TRIP RECORD",
                RelayLcdPage.Setup => "SETUP",
                RelayLcdPage.Diagnostics => "DIAGNOSTICS",
                _ => "SV READY"
            };
    }

    public void NextPage()
        => ShowPage((RelayLcdPage)(((int)CurrentPage + 1) % Enum.GetValues<RelayLcdPage>().Length));

    public void PreviousPage()
    {
        var count = Enum.GetValues<RelayLcdPage>().Length;
        ShowPage((RelayLcdPage)(((int)CurrentPage + count - 1) % count));
    }

    private void MeasureTab_Click(object? sender, RoutedEventArgs e)
        => ShowPage(RelayLcdPage.Measure);

    private void EventsTab_Click(object? sender, RoutedEventArgs e)
        => ShowPage(RelayLcdPage.Events);

    private void RecordsTab_Click(object? sender, RoutedEventArgs e)
        => ShowPage(RelayLcdPage.Records);

    private void SetupTab_Click(object? sender, RoutedEventArgs e)
        => ShowPage(RelayLcdPage.Setup);
}
