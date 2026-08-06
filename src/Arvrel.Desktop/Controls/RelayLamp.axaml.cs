using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arvrel.Protection;

namespace Arvrel.Desktop.Controls;

public sealed partial class RelayLamp : UserControl
{
    private static readonly Color OffColor = Color.Parse("#52636D");
    private static readonly Color PickupColor = Color.Parse("#E1AA38");
    private static readonly Color TripColor = Color.Parse("#E34D53");

    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<RelayLamp, bool>(nameof(IsOn));

    public static readonly StyledProperty<Color> ActiveColorProperty =
        AvaloniaProperty.Register<RelayLamp, Color>(
            nameof(ActiveColor),
            Color.Parse("#45B768"));

    public static readonly StyledProperty<RelayLampState?> LampStateProperty =
        AvaloniaProperty.Register<RelayLamp, RelayLampState?>(nameof(LampState));

    public RelayLamp()
    {
        InitializeComponent();
        UpdateOptics();
    }

    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public Color ActiveColor
    {
        get => GetValue(ActiveColorProperty);
        set => SetValue(ActiveColorProperty, value);
    }

    public RelayLampState? LampState
    {
        get => GetValue(LampStateProperty);
        set => SetValue(LampStateProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOnProperty ||
            change.Property == ActiveColorProperty ||
            change.Property == LampStateProperty)
        {
            UpdateOptics();
        }
    }

    private void UpdateOptics()
    {
        if (Lens is null || Halo is null)
            return;

        if (LampState is { } state)
        {
            switch (state)
            {
                case RelayLampState.Pickup:
                    SetActive(PickupColor);
                    return;
                case RelayLampState.Trip:
                    SetActive(TripColor);
                    return;
                default:
                    SetOff();
                    return;
            }
        }

        if (!IsOn)
        {
            SetOff();
            return;
        }

        SetActive(ActiveColor);
    }

    private void SetOff()
    {
        Lens.Fill = new SolidColorBrush(OffColor);
        Halo.Fill = Brushes.Transparent;
        Halo.Opacity = 0;
    }

    private void SetActive(Color color)
    {
        Lens.Fill = new SolidColorBrush(color);
        Halo.Fill = new SolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B));
        Halo.Opacity = 0.72;
    }
}
