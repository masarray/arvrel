using System.Windows.Input;
using Arvrel.Desktop.Infrastructure;
using Arvrel.Protection;

namespace Arvrel.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private enum FaceplatePage
    {
        Measure,
        Events,
        Records,
        Setup
    }

    private readonly RelayAnnunciationLatch _relayAnnunciationLatch = new();
    private RelayAnnunciationSnapshot _relayAnnunciation = new(
        PickupActive: false,
        TripLatched: false,
        PhaseA: RelayLampState.Off,
        PhaseB: RelayLampState.Off,
        PhaseC: RelayLampState.Off,
        Earth: RelayLampState.Off);
    private FaceplatePage _faceplatePage;
    private string _faceplateNavigationText = "MEASURE · HOME";
    private RelayCommand? _faceplateMeasureCommand;
    private RelayCommand? _faceplateEventsCommand;
    private RelayCommand? _faceplateRecordsCommand;
    private RelayCommand? _faceplateSetupCommand;
    private RelayCommand? _faceplateHomeCommand;
    private RelayCommand? _faceplateMenuCommand;
    private RelayCommand? _faceplateBackCommand;
    private RelayCommand? _faceplateFavoriteCommand;
    private RelayCommand? _faceplatePreviousCommand;
    private RelayCommand? _faceplateNextCommand;
    private RelayCommand? _faceplateOkCommand;

    public RelayLampState PhaseAAnnunciation => _relayAnnunciation.PhaseA;
    public RelayLampState PhaseBAnnunciation => _relayAnnunciation.PhaseB;
    public RelayLampState PhaseCAnnunciation => _relayAnnunciation.PhaseC;
    public RelayLampState EarthAnnunciation => _relayAnnunciation.Earth;

    public string FaceplatePageName => _faceplatePage.ToString().ToUpperInvariant();
    public string FaceplateNavigationText => _faceplateNavigationText;

    public string FaceplateRow1Label => _faceplatePage switch
    {
        FaceplatePage.Measure => "IA",
        FaceplatePage.Events => "E1",
        FaceplatePage.Records => "STATE",
        _ => "GROUP"
    };

    public string FaceplateRow2Label => _faceplatePage switch
    {
        FaceplatePage.Measure => "IB",
        FaceplatePage.Events => "E2",
        FaceplatePage.Records => "CAUSE",
        _ => "PROFILE"
    };

    public string FaceplateRow3Label => _faceplatePage switch
    {
        FaceplatePage.Measure => "IC",
        FaceplatePage.Events => "E3",
        FaceplatePage.Records => "INJ",
        _ => "FREQ"
    };

    public string FaceplateRow4Label => _faceplatePage switch
    {
        FaceplatePage.Measure => "3I0",
        FaceplatePage.Events => "E4",
        FaceplatePage.Records => "SET",
        _ => "TRUST"
    };

    public string FaceplateRow1Value => _faceplatePage switch
    {
        FaceplatePage.Measure => PhaseAText,
        FaceplatePage.Events => EventAt(0),
        FaceplatePage.Records => TripLatched ? "TRIP LATCHED" : PickupActive ? "PICKUP" : "READY",
        _ => SettingsGroupText
    };

    public string FaceplateRow2Value => _faceplatePage switch
    {
        FaceplatePage.Measure => PhaseBText,
        FaceplatePage.Events => EventAt(1),
        FaceplatePage.Records => Compact(_currentTick.Protection.ActiveElement, 24),
        _ => Compact(ProfileNameText, 24)
    };

    public string FaceplateRow3Value => _faceplatePage switch
    {
        FaceplatePage.Measure => PhaseCText,
        FaceplatePage.Events => EventAt(2),
        FaceplatePage.Records => InjectionFingerprintText,
        _ => FrequencyTextDisplay
    };

    public string FaceplateRow4Value => _faceplatePage switch
    {
        FaceplatePage.Measure => ResidualText,
        FaceplatePage.Events => EventAt(3),
        FaceplatePage.Records => SettingsFingerprintText,
        _ => AllowsTrip ? "TRIP PERMITTED" : "TRIP BLOCKED"
    };

    public string FaceplateDetailText => _faceplatePage switch
    {
        FaceplatePage.Measure => DecisionReason,
        FaceplatePage.Events => $"{Events.Count} recent operator events · newest first",
        FaceplatePage.Records => TripLatched
            ? DecisionReason
            : "No latched operation evidence. Pickup and trip causes are captured by the portable relay core.",
        _ => $"{SourceModeText} · {ProvenanceText}"
    };

    public ICommand FaceplateMeasureCommand =>
        _faceplateMeasureCommand ??= new RelayCommand(() => SelectFaceplatePage(FaceplatePage.Measure, "F1"));

    public ICommand FaceplateEventsCommand =>
        _faceplateEventsCommand ??= new RelayCommand(() => SelectFaceplatePage(FaceplatePage.Events, "F2"));

    public ICommand FaceplateRecordsCommand =>
        _faceplateRecordsCommand ??= new RelayCommand(() => SelectFaceplatePage(FaceplatePage.Records, "F3"));

    public ICommand FaceplateSetupCommand =>
        _faceplateSetupCommand ??= new RelayCommand(() => SelectFaceplatePage(FaceplatePage.Setup, "F4"));

    public ICommand FaceplateHomeCommand =>
        _faceplateHomeCommand ??= new RelayCommand(() => SelectFaceplatePage(FaceplatePage.Measure, "HOME"));

    public ICommand FaceplateMenuCommand =>
        _faceplateMenuCommand ??= new RelayCommand(() => SelectFaceplatePage(FaceplatePage.Setup, "MENU"));

    public ICommand FaceplateBackCommand =>
        _faceplateBackCommand ??= new RelayCommand(() => MoveFaceplatePage(-1, "BACK"));

    public ICommand FaceplateFavoriteCommand =>
        _faceplateFavoriteCommand ??= new RelayCommand(() => SelectFaceplatePage(FaceplatePage.Events, "FAVORITE"));

    public ICommand FaceplatePreviousCommand =>
        _faceplatePreviousCommand ??= new RelayCommand(() => MoveFaceplatePage(-1, "PREVIOUS"));

    public ICommand FaceplateNextCommand =>
        _faceplateNextCommand ??= new RelayCommand(() => MoveFaceplatePage(1, "NEXT"));

    public ICommand FaceplateOkCommand =>
        _faceplateOkCommand ??= new RelayCommand(ConfirmFaceplatePage);

    private void UpdateFaceplateState(ProtectionSnapshot snapshot)
        => _relayAnnunciation = _relayAnnunciationLatch.Observe(snapshot);

    private void SelectFaceplatePage(FaceplatePage page, string source)
    {
        _faceplatePage = page;
        _faceplateNavigationText = $"{FaceplatePageName} · {source}";
        OnPropertyChanged(string.Empty);
    }

    private void MoveFaceplatePage(int direction, string source)
    {
        var pageCount = Enum.GetValues<FaceplatePage>().Length;
        var next = ((int)_faceplatePage + direction + pageCount) % pageCount;
        SelectFaceplatePage((FaceplatePage)next, source);
    }

    private void ConfirmFaceplatePage()
    {
        _faceplateNavigationText = $"{FaceplatePageName} · VIEW CONFIRMED";
        AddEvent("FACEPLATE", $"{FaceplatePageName} operator view selected");
        OnPropertyChanged(string.Empty);
    }

    private string EventAt(int index)
    {
        if (index < 0 || index >= Events.Count)
            return "—";
        return Compact(Events[index], 34);
    }

    private static string Compact(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";
        return value.Length <= maximumLength
            ? value
            : string.Concat(value.AsSpan(0, maximumLength - 1), "…");
    }
}
