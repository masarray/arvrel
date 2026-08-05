using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private const double ActiveLampGlowBlurRadius = 13.5;
    private const double ActiveLampGlowOpacity = 0.84;

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

    private static readonly DependencyProperty AppliedPinnedStateProperty = DependencyProperty.RegisterAttached(
        "AppliedPinnedState",
        typeof(RelayIndicatorState),
        typeof(RelayIndicatorLampBehavior),
        new PropertyMetadata(RelayIndicatorState.Auto));

    private static readonly DependencyPropertyDescriptor FillDescriptor =
        DependencyPropertyDescriptor.FromProperty(Shape.FillProperty, typeof(Ellipse));

    private static readonly Color OffReference = Color.FromRgb(81, 96, 106);

    private static readonly Brush RelayLampBezelBrush = CreateBezelBrush();

    private static readonly Brush OffLampBrush = CreateLampBrush(
        Color.FromRgb(218, 225, 229),
        Color.FromRgb(106, 119, 127),
        Color.FromRgb(54, 65, 72),
        Color.FromRgb(22, 29, 34),
        Color.FromRgb(5, 8, 10));

    private static readonly Brush GreenLampBrush = CreateLampBrush(
        Color.FromRgb(251, 255, 252),
        Color.FromRgb(116, 255, 151),
        Color.FromRgb(9, 199, 75),
        Color.FromRgb(3, 101, 39),
        Color.FromRgb(2, 20, 9));

    private static readonly Brush OrangeLampBrush = CreateLampBrush(
        Color.FromRgb(255, 255, 246),
        Color.FromRgb(255, 219, 111),
        Color.FromRgb(247, 157, 20),
        Color.FromRgb(159, 65, 0),
        Color.FromRgb(31, 10, 0));

    private static readonly Brush RedLampBrush = CreateLampBrush(
        Color.FromRgb(255, 239, 239),
        Color.FromRgb(255, 105, 109),
        Color.FromRgb(230, 23, 38),
        Color.FromRgb(130, 4, 16),
        Color.FromRgb(28, 0, 4));

    private static readonly Effect OffLampShadow = CreateGlow(
        Color.FromRgb(24, 32, 38), 4.5, 0.48);
    private static readonly Effect GreenGlow = CreateGlow(
        Color.FromRgb(31, 255, 99), ActiveLampGlowBlurRadius, ActiveLampGlowOpacity);
    private static readonly Effect OrangeGlow = CreateGlow(
        Color.FromRgb(255, 166, 25), ActiveLampGlowBlurRadius, ActiveLampGlowOpacity);
    private static readonly Effect RedGlow = CreateGlow(
        Color.FromRgb(255, 37, 48), ActiveLampGlowBlurRadius, ActiveLampGlowOpacity);

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

        // BeginAnimation can synchronously raise the Fill value-changed callback.
        // Move the guard first so teardown cannot re-enter the pinned-state path.
        ellipse.SetValue(AppliedPinnedStateProperty, RelayIndicatorState.Auto);
        ellipse.BeginAnimation(Shape.FillProperty, null);
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
            ApplyResolvedState(ellipse, pinned, enforcePinnedFill: true);
            return;
        }

        ClearPinnedFillOverride(ellipse);
        if (ellipse.Fill is RadialGradientBrush)
            return;

        var active = IsActive(ellipse.Fill);
        var state = active ? ResolveActiveState(ellipse.Name, ellipse.Fill) : RelayIndicatorState.Off;
        ApplyResolvedState(ellipse, state, enforcePinnedFill: false);
    }

    private static void ApplyResolvedState(
        Ellipse ellipse,
        RelayIndicatorState state,
        bool enforcePinnedFill)
    {
        switch (state)
        {
            case RelayIndicatorState.Green:
                SetLamp(ellipse, GreenLampBrush, GreenGlow, true, state, enforcePinnedFill);
                break;
            case RelayIndicatorState.Orange:
                SetLamp(ellipse, OrangeLampBrush, OrangeGlow, true, state, enforcePinnedFill);
                break;
            case RelayIndicatorState.Red:
                SetLamp(ellipse, RedLampBrush, RedGlow, true, state, enforcePinnedFill);
                break;
            default:
                SetLamp(ellipse, OffLampBrush, OffLampShadow, false, RelayIndicatorState.Off, enforcePinnedFill);
                break;
        }
    }

    private static void SetLamp(
        Ellipse ellipse,
        Brush fill,
        Effect effect,
        bool active,
        RelayIndicatorState state,
        bool enforcePinnedFill)
    {
        if (enforcePinnedFill)
            ApplyPinnedFillOverride(ellipse, fill, state);
        else if (!ReferenceEquals(ellipse.Fill, fill))
            ellipse.Fill = fill;

        // Every state uses one metallic bezel and one physical lens geometry.
        // Only the emitted colour changes, matching a real relay indicator bank.
        ellipse.Stroke = RelayLampBezelBrush;
        ellipse.StrokeThickness = ellipse.Width <= 9 ? 1.15 : 2.0;
        ellipse.Opacity = active ? 1.0 : 0.96;
        ellipse.Effect = effect;
    }

    private static void ApplyPinnedFillOverride(
        Ellipse ellipse,
        Brush fill,
        RelayIndicatorState state)
    {
        var applied = (RelayIndicatorState)ellipse.GetValue(AppliedPinnedStateProperty);
        if (applied == state)
            return;

        var animation = new ObjectAnimationUsingKeyFrames
        {
            Duration = new Duration(TimeSpan.Zero),
            FillBehavior = FillBehavior.HoldEnd
        };
        animation.KeyFrames.Add(new DiscreteObjectKeyFrame(fill, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.Freeze();

        // Mark the transition before BeginAnimation. WPF can synchronously notify
        // FillDescriptor while applying the animation; the marker makes that callback
        // idempotent and prevents recursive animation creation / UI stack overflow.
        ellipse.SetValue(AppliedPinnedStateProperty, state);
        try
        {
            ellipse.BeginAnimation(
                Shape.FillProperty,
                animation,
                HandoffBehavior.SnapshotAndReplace);
        }
        catch
        {
            ellipse.SetValue(AppliedPinnedStateProperty, RelayIndicatorState.Auto);
            throw;
        }
    }

    private static void ClearPinnedFillOverride(Ellipse ellipse)
    {
        if ((RelayIndicatorState)ellipse.GetValue(AppliedPinnedStateProperty) == RelayIndicatorState.Auto)
            return;

        // Clear the guard before removing the animation for the same synchronous
        // callback reason as ApplyPinnedFillOverride.
        ellipse.SetValue(AppliedPinnedStateProperty, RelayIndicatorState.Auto);
        ellipse.BeginAnimation(Shape.FillProperty, null);
    }

    private static RelayIndicatorState ResolveActiveState(string name, Brush source)
    {
        return name switch
        {
            "PickupLed" => RelayIndicatorState.Orange,
            "TripLed" => RelayIndicatorState.Red,
            "BlockLed" => RelayIndicatorState.Orange,
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

    private static Brush CreateLampBrush(
        Color highlight,
        Color core,
        Color middle,
        Color edge,
        Color outerRim)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.52, 0.54),
            GradientOrigin = new Point(0.29, 0.23),
            RadiusX = 0.72,
            RadiusY = 0.72,
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };
        brush.GradientStops.Add(new GradientStop(highlight, 0.00));
        brush.GradientStops.Add(new GradientStop(core, 0.18));
        brush.GradientStops.Add(new GradientStop(middle, 0.48));
        brush.GradientStops.Add(new GradientStop(edge, 0.78));
        brush.GradientStops.Add(new GradientStop(outerRim, 1.00));
        return Freeze(brush);
    }

    private static Brush CreateBezelBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(232, 238, 241), 0.00));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(127, 141, 149), 0.28));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(48, 58, 65), 0.68));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(8, 12, 15), 1.00));
        return Freeze(brush);
    }

    private static Effect CreateGlow(Color color, double blurRadius, double opacity)
        => Freeze(new DropShadowEffect
        {
            BlurRadius = blurRadius,
            Color = color,
            Direction = 0,
            Opacity = opacity,
            ShadowDepth = 0,
            RenderingBias = RenderingBias.Quality
        });

    private static T Freeze<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }
}
