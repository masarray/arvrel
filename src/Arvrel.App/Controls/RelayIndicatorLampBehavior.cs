using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Arvrel.App.Controls;

internal static class RelayIndicatorLampBehavior
{
    private static readonly DependencyProperty IsAttachedProperty = DependencyProperty.RegisterAttached(
        "IsAttached",
        typeof(bool),
        typeof(RelayIndicatorLampBehavior),
        new PropertyMetadata(false));

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
        Color.FromRgb(255, 248, 246),
        Color.FromRgb(255, 88, 76),
        Color.FromRgb(211, 5, 23),
        Color.FromRgb(70, 1, 8));

    private static readonly Brush OffStrokeBrush = Freeze(new SolidColorBrush(Color.FromRgb(42, 51, 57)));
    private static readonly Brush GreenStrokeBrush = Freeze(new SolidColorBrush(Color.FromRgb(151, 255, 181)));
    private static readonly Brush OrangeStrokeBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 224, 141)));
    private static readonly Brush RedStrokeBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 177, 169)));

    private static readonly Effect GreenGlow = CreateGlow(Color.FromRgb(31, 255, 99));
    private static readonly Effect OrangeGlow = CreateGlow(Color.FromRgb(255, 157, 20));
    private static readonly Effect RedGlow = CreateGlow(Color.FromRgb(255, 35, 45));

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
        if (ellipse.Fill is RadialGradientBrush)
            return;

        var active = IsActive(ellipse.Fill);
        var state = active ? ResolveActiveState(ellipse.Name, ellipse.Fill) : LampState.Off;

        switch (state)
        {
            case LampState.Green:
                SetLamp(ellipse, GreenLampBrush, GreenStrokeBrush, GreenGlow, active: true);
                break;
            case LampState.Orange:
                SetLamp(ellipse, OrangeLampBrush, OrangeStrokeBrush, OrangeGlow, active: true);
                break;
            case LampState.Red:
                SetLamp(ellipse, RedLampBrush, RedStrokeBrush, RedGlow, active: true);
                break;
            default:
                SetLamp(ellipse, OffLampBrush, OffStrokeBrush, null, active: false);
                break;
        }
    }

    private static void SetLamp(Ellipse ellipse, Brush fill, Brush stroke, Effect? effect, bool active)
    {
        ellipse.Fill = fill;
        ellipse.Stroke = stroke;
        ellipse.StrokeThickness = active ? 1.15 : 1;
        ellipse.Opacity = active ? 1 : 0.88;
        ellipse.Effect = effect;
    }

    private static LampState ResolveActiveState(string name, Brush source)
    {
        return name switch
        {
            "PickupLed" => LampState.Orange,
            "TripLed" or "BlockLed" => LampState.Red,
            "HealthyLed" or "TopHealthLed" => IsHealthyColor(source) ? LampState.Green : LampState.Red,
            "PhaseALed" or "PhaseBLed" or "PhaseCLed" or "EarthLed" =>
                IsTripColor(source) ? LampState.Red : LampState.Orange,
            _ => LampState.Green
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
        brush.GradientStops.Add(new GradientStop(core, 0.3));
        brush.GradientStops.Add(new GradientStop(edge, 0.74));
        brush.GradientStops.Add(new GradientStop(outerRim, 1));
        return Freeze(brush);
    }

    private static Effect CreateGlow(Color color)
        => Freeze(new DropShadowEffect
        {
            BlurRadius = 15,
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

    private enum LampState
    {
        Off,
        Green,
        Orange,
        Red
    }
}
