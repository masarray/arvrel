using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Arvrel.Application.Laboratory;
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

    private static readonly FieldInfo? CurrentTickField = typeof(MainWindowViewModel).GetField(
        "_currentTick",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly RelayAnnunciationLatch _annunciationLatch = new();
    private MainWindowViewModel? _viewModel;

    public VirtualRelayControl()
    {
        InitializeComponent();
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
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => RefreshAnnunciation();

    private void RefreshAnnunciation()
    {
        if (_viewModel is null ||
            CurrentTickField?.GetValue(_viewModel) is not InternalLabTick tick)
        {
            ClearAnnunciation();
            return;
        }

        var indication = _annunciationLatch.Observe(tick.Protection);
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
}
