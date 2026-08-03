using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Arvrel.App.Controls;

internal enum RelayIndicatorState
{
    Auto,
    Off,
    Green,
    Orange,
    Red
}

internal static class RelayIndicatorLampBehavior
{
    private static readonly DependencyProperty IsAttachedProperty = DependencyProperty.RegisterAttached(
        "IsAttached",
        typeof(bool),
        typeof(RelayIndicatorLampBehavior),
        new PropertyMetadata(false));

    private static readonly DependencyProperty PinnedStateProperty = DependencyProperty.RegisterAttached(
        "PinnedState",
        typeof(RelayIndicatorState),
        typeof(RelayIndicatorLampBehavior),
        new PropertyMetadata(RelayIndicatorState.Auto, OnPinnedStateChanged));

    private static readonly DependencyPropertyDescriptor FillDescriptor =
        DependencyPropertyDescriptor.FromProperty(Shape.FillProperty, typeof(Ellipse));

    private static readonly Color OffReference = Color.FromRgb(81, 96, 106);

    private static readonly Brush OffLampBrush = CreateLampBrush(
        Color.FromRgb(121, 132, 140),
        Color.FromRgb(64, 75, 82),
        Color.FromRgb(31, 39, 44),
        Color.FromRgb(12, 17, 20));

    private static readonly Brush GreenLampBrush = CreateLampBrush(
        Color.FromRgb(249, 255, 250),
        Color.FromRgb(64, 255, 121),
        Color.FromRgb(4, 153, 58),
        Color.FromRgb(1, 52, 22));

    private static readonly Brush OrangeLampBrush = CreateLampBrush(
        Color.FromRgb(255, 253, 239),
        Color.FromRgb(255, 196, 66),
        Color.FromRgb(210, 96, 0),
        Color.FromRgb(73, 25, 0));

    private static readonly Brush RedLampBrush = CreateLampBrush(
        Color.FromRgb(255, 255, 255),
        Color.FromRgb(255, 42, 48),
        Color.FromRgb(230, 0, 24),
        Color.FromRgb(82, 0, 7));

    private static readonly Brush OffStrokeBrush = Freeze(new SolidColorBrush(Color.FromRgb(42, 51, 57)));
    private static readonly Brush GreenStrokeBrush = Freeze(new SolidColorBrush(Color.FromRgb(151, 255, 181)));
    private static readonly Brush OrangeStrokeBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 224, 141)));
    private static readonly Brush RedStrokeBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 196, 190)));

    private static readonly Effect GreenGlow = CreateGlow(Color.FromRgb(31, 255, 99), 15);
    private static readonly Effect OrangeGlow = CreateGlow(Color.FromRgb(255, 157, 20), 15);
    private static readonly Effect RedGlow = CreateGlow(Color.FromRgb(255, 18, 34), 19);

    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(Ellipse),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));

        EventManager.RegisterClassHandler(
            typeof(Ellipse),
            FrameworkElement.UnloadedEvent,
            new RoutedEventHandler(OnUnloaded));
    }

    internal static RelayIndicatorState GetPinnedState(Ellipse ellipse)
        => (RelayIndicatorState)ellipse.GetValue(PinnedStateProperty);

    internal static void SetPinnedState(Ellipse ellipse, RelayIndicatorState state)
        => ellipse.SetValue(PinnedStateProperty, state);

    private static void OnPinnedStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is Ellipse ellipse && ellipse.IsLoaded)
            ApplyVisualState(ellipse);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Ellipse ellipse || !IsRelayIndicator(ellipse) || (bool)ellipse.GetValue(IsAttachedProperty))
            return;

        ellipse.SetValue(IsAttachedProperty, true);
        FillDescriptor.AddValueChanged(ellipse, OnFillChanged);
        ApplyVisualState(ellipse);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Ellipse ellipse || !(bool)ellipse.GetValue(IsAttachedProperty))
            return;

        FillDescriptor.RemoveValueChanged(ellipse, OnFillChanged);
        ellipse.SetValue(IsAttachedProperty, false);
    }

    private static void OnFillChanged(object? sender, EventArgs e)
    {
        if (sender is Ellipse ellipse)
            ApplyVisualState(ellipse);
    }

    private static void ApplyVisualState(Ellipse ellipse)
    {
        var pinned = GetPinnedState(ellipse);
        if (pinned != RelayIndicatorState.Auto)
        {
            ApplyResolvedState(ellipse, pinned);
            return;
        }

        if (ellipse.Fill is RadialGradientBrush)
            return;

        var active = IsActive(ellipse.Fill);
        var state = active ? ResolveActiveState(ellipse.Name, ellipse.Fill) : RelayIndicatorState.Off;
        ApplyResolvedState(ellipse, state);
    }

    private static void ApplyResolvedState(Ellipse ellipse, RelayIndicatorState state)
    {
        switch (state)
        {
            case RelayIndicatorState.Green:
                SetLamp(ellipse, GreenLampBrush, GreenStrokeBrush, GreenGlow, active: true);
                break;
            case RelayIndicatorState.Orange:
                SetLamp(ellipse, OrangeLampBrush, OrangeStrokeBrush, OrangeGlow, active: true);
                break;
            case RelayIndicatorState.Red:
                SetLamp(ellipse, RedLampBrush, RedStrokeBrush, RedGlow, active: true);
                break;
            default:
                SetLamp(ellipse, OffLampBrush, OffStrokeBrush, null, active: false);
                break;
        }
    }

    private static void SetLamp(Ellipse ellipse, Brush fill, Brush stroke, Effect? effect, bool active)
    {
        if (!ReferenceEquals(ellipse.Fill, fill))
            ellipse.Fill = fill;
        ellipse.Stroke = stroke;
        ellipse.StrokeThickness = active ? 1.25 : 1;
        ellipse.Opacity = active ? 1 : 0.88;
        ellipse.Effect = effect;
    }

    private static RelayIndicatorState ResolveActiveState(string name, Brush source)
    {
        return name switch
        {
            "PickupLed" => RelayIndicatorState.Orange,
            "TripLed" or "BlockLed" => RelayIndicatorState.Red,
            "HealthyLed" or "TopHealthLed" => IsHealthyColor(source) ? RelayIndicatorState.Green : RelayIndicatorState.Red,
            "PhaseALed" or "PhaseBLed" or "PhaseCLed" or "EarthLed" =>
                IsTripColor(source) ? RelayIndicatorState.Red : RelayIndicatorState.Orange,
            _ => RelayIndicatorState.Green
        };
    }

    private static bool IsRelayIndicator(Ellipse ellipse)
        => ellipse.Name.EndsWith("Led", StringComparison.Ordinal);

    private static bool IsActive(Brush? brush)
    {
        if (brush is not SolidColorBrush solid)
            return brush is not null;

        var color = solid.Color;
        var distance = Math.Sqrt(
            Math.Pow(color.R - OffReference.R, 2) +
            Math.Pow(color.G - OffReference.G, 2) +
            Math.Pow(color.B - OffReference.B, 2));
        return distance > 42;
    }

    private static bool IsHealthyColor(Brush? brush)
        => brush is SolidColorBrush solid && solid.Color.G > solid.Color.R && solid.Color.G > solid.Color.B;

    private static bool IsTripColor(Brush? brush)
        => brush is SolidColorBrush solid &&
           solid.Color.R >= 170 &&
           solid.Color.G < 100 &&
           solid.Color.B < 115;

    private static Brush CreateLampBrush(Color highlight, Color core, Color edge, Color outerRim)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.29, 0.24),
            RadiusX = 0.72,
            RadiusY = 0.72
        };
        brush.GradientStops.Add(new GradientStop(highlight, 0));
        brush.GradientStops.Add(new GradientStop(core, 0.28));
        brush.GradientStops.Add(new GradientStop(edge, 0.72));
        brush.GradientStops.Add(new GradientStop(outerRim, 1));
        return Freeze(brush);
    }

    private static Effect CreateGlow(Color color, double blurRadius)
        => Freeze(new DropShadowEffect
        {
            BlurRadius = blurRadius,
            Color = color,
            Direction = 0,
            Opacity = 1,
            ShadowDepth = 0,
            RenderingBias = RenderingBias.Quality
        });

    private static T Freeze<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }
}
