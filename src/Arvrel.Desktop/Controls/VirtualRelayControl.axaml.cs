using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Arvrel.Desktop.ViewModels;
using Arvrel.Protection;

namespace Arvrel.Desktop.Controls;

public sealed partial class VirtualRelayControl : UserControl
{
    public static readonly StyledProperty<bool> PhaseAActiveProperty =
        AvaloniaProperty.Register<VirtualRelayControl, bool>(nameof(PhaseAActive));

    public static readonly StyledProperty<bool> PhaseBActiveProperty =
        AvaloniaProperty.Register<VirtualRelayControl, bool>(nameof(PhaseBActive));

    public static readonly StyledProperty<bool> PhaseCActiveProperty =
        AvaloniaProperty.Register<VirtualRelayControl, bool>(nameof(PhaseCActive));

    public static readonly StyledProperty<bool> EarthActiveProperty =
        AvaloniaProperty.Register<VirtualRelayControl, bool>(nameof(EarthActive));

    public static readonly StyledProperty<RelayLampTone> PhaseAToneProperty =
        AvaloniaProperty.Register<VirtualRelayControl, RelayLampTone>(nameof(PhaseATone), RelayLampTone.Amber);

    public static readonly StyledProperty<RelayLampTone> PhaseBToneProperty =
        AvaloniaProperty.Register<VirtualRelayControl, RelayLampTone>(nameof(PhaseBTone), RelayLampTone.Amber);

    public static readonly StyledProperty<RelayLampTone> PhaseCToneProperty =
        AvaloniaProperty.Register<VirtualRelayControl, RelayLampTone>(nameof(PhaseCTone), RelayLampTone.Amber);

    public static readonly StyledProperty<RelayLampTone> EarthToneProperty =
        AvaloniaProperty.Register<VirtualRelayControl, RelayLampTone>(nameof(EarthTone), RelayLampTone.Amber);

    private readonly RelayAnnunciationLatch _annunciationLatch = new();
    private readonly RelayLcdControl? _relayLcd;
    private MainWindowViewModel? _viewModel;
    private string? _annunciationSourceIdentity;

    public VirtualRelayControl()
    {
        InitializeComponent();
        _relayLcd = this.FindControl<RelayLcdControl>("RelayLcd");
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => DetachViewModel();
        AttachViewModel(DataContext as MainWindowViewModel);
    }

    public bool PhaseAActive
    {
        get => GetValue(PhaseAActiveProperty);
        private set => SetValue(PhaseAActiveProperty, value);
    }

    public bool PhaseBActive
    {
        get => GetValue(PhaseBActiveProperty);
        private set => SetValue(PhaseBActiveProperty, value);
    }

    public bool PhaseCActive
    {
        get => GetValue(PhaseCActiveProperty);
        private set => SetValue(PhaseCActiveProperty, value);
    }

    public bool EarthActive
    {
        get => GetValue(EarthActiveProperty);
        private set => SetValue(EarthActiveProperty, value);
    }

    public RelayLampTone PhaseATone
    {
        get => GetValue(PhaseAToneProperty);
        private set => SetValue(PhaseAToneProperty, value);
    }

    public RelayLampTone PhaseBTone
    {
        get => GetValue(PhaseBToneProperty);
        private set => SetValue(PhaseBToneProperty, value);
    }

    public RelayLampTone PhaseCTone
    {
        get => GetValue(PhaseCToneProperty);
        private set => SetValue(PhaseCToneProperty, value);
    }

    public RelayLampTone EarthTone
    {
        get => GetValue(EarthToneProperty);
        private set => SetValue(EarthToneProperty, value);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
        => AttachViewModel(DataContext as MainWindowViewModel);

    private void AttachViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
            return;

        DetachViewModel();
        _viewModel = viewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        RefreshAnnunciation();
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = null;
        _annunciationSourceIdentity = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => RefreshAnnunciation();

    private void RefreshAnnunciation()
    {
        if (_viewModel is null)
        {
            ClearAnnunciation();
            return;
        }

        var presentation = _viewModel.CurrentPresentationSnapshot;
        var sourceIdentity = $"{presentation.SourceMode}|{presentation.SourceIdentity}";
        if (!string.Equals(_annunciationSourceIdentity, sourceIdentity, StringComparison.Ordinal))
        {
            _annunciationSourceIdentity = sourceIdentity;
            _annunciationLatch.Reset();
        }

        var indication = _annunciationLatch.Observe(_viewModel.CurrentProtectionSnapshot);
        ApplyLamp(indication.PhaseA, value => PhaseAActive = value, value => PhaseATone = value);
        ApplyLamp(indication.PhaseB, value => PhaseBActive = value, value => PhaseBTone = value);
        ApplyLamp(indication.PhaseC, value => PhaseCActive = value, value => PhaseCTone = value);
        ApplyLamp(indication.Earth, value => EarthActive = value, value => EarthTone = value);
    }

    private void ClearAnnunciation()
    {
        _annunciationLatch.Reset();
        PhaseAActive = false;
        PhaseBActive = false;
        PhaseCActive = false;
        EarthActive = false;
        PhaseATone = RelayLampTone.Amber;
        PhaseBTone = RelayLampTone.Amber;
        PhaseCTone = RelayLampTone.Amber;
        EarthTone = RelayLampTone.Amber;
    }

    private static void ApplyLamp(
        RelayLampState state,
        Action<bool> setActive,
        Action<RelayLampTone> setTone)
    {
        setActive(state != RelayLampState.Off);
        setTone(state == RelayLampState.Trip ? RelayLampTone.Red : RelayLampTone.Amber);
    }

    private void F1Button_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Measure);

    private void F2Button_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Events);

    private void F3Button_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Records);

    private void F4Button_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Setup);

    private void F5Button_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Diagnostics);

    private void HomeButton_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Measure);

    private void MenuButton_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Setup);

    private void BackButton_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Measure);

    private void StarButton_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.ShowPage(RelayLcdPage.Records);

    private void PreviousPageButton_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.PreviousPage();

    private void NextPageButton_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.NextPage();

    private void OkButton_Click(object? sender, RoutedEventArgs e)
        => _relayLcd?.NextPage();
}
