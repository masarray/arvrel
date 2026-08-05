using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
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
    private const double RelayLampBezelDiameter = 16.0;
    private const double RelayLampCavityDiameter = 12.4;
    private const double RelayLampHaloDiameter = 10.8;
    private const double RelayLampLensDiameter = 9.6;
    private const double ActiveLampGlowBlurRadius = 12.0;
    private const double ActiveLampGlowOpacity = 0.74;

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

    private static readonly ConditionalWeakTable<Ellipse, LampVisualParts> LampParts = new();
    private static readonly Color OffReference = Color.FromRgb(81, 96, 106);

    private static readonly Brush RelayLampBezelBrush = CreateBezelBrush();
    private static readonly Brush RelayLampCavityBrush = CreateCavityBrush();
    private static readonly Brush RelayLampHighlightBrush = Freeze(
        new SolidColorBrush(Color.FromArgb(205, 255, 255, 255)));

    private static readonly Brush OffLampBrush = CreateLampBrush(
        Color.FromRgb(210, 219, 224),
        Color.FromRgb(112, 124, 132),
        Color.FromRgb(61, 72, 79),
        Color.FromRgb(31, 39, 44),
        Color.FromRgb(7, 11, 13));

    private static readonly Brush GreenLampBrush = CreateLampBrush(
        Color.FromRgb(250, 255, 251),
        Color.FromRgb(118, 255, 151),
        Color.FromRgb(12, 204, 78),
        Color.FromRgb(3, 112, 42),
        Color.FromRgb(1, 28, 11));

    private static readonly Brush OrangeLampBrush = CreateLampBrush(
        Color.FromRgb(255, 255, 244),
        Color.FromRgb(255, 224, 121),
        Color.FromRgb(248, 161, 24),
        Color.FromRgb(163, 69, 1),
        Color.FromRgb(35, 12, 0));

    private static readonly Brush RedLampBrush = CreateLampBrush(
        Color.FromRgb(255, 241, 241),
        Color.FromRgb(255, 112, 116),
        Color.FromRgb(231, 25, 40),
        Color.FromRgb(133, 4, 17),
        Color.FromRgb(30, 0, 4));

    private static readonly Brush GreenHaloBrush = Freeze(
        new SolidColorBrush(Color.FromRgb(38, 225, 91)));
    private static readonly Brush OrangeHaloBrush = Freeze(
        new SolidColorBrush(Color.FromRgb(242, 151, 23)));
    private static readonly Brush RedHaloBrush = Freeze(
        new SolidColorBrush(Color.FromRgb(237, 38, 48)));
    private static readonly Brush TransparentHaloBrush = Freeze(Brushes.Transparent.Clone());

    private static readonly Effect BezelShadow = CreateDirectionalShadow(
        Color.FromRgb(28, 38, 44), 4.0, 315, 1.2, 0.62);
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
        if (sender is not Ellipse ellipse ||
            !IsRelayIndicator(ellipse) ||
            (bool)ellipse.GetValue(IsAttachedProperty))
            return;

        ellipse.SetValue(IsAttachedProperty, true);
        ConfigurePhysicalLamp(ellipse);
        FillDescriptor.AddValueChanged(ellipse, OnFillChanged);
        ApplyVisualState(ellipse);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Ellipse ellipse || !(bool)ellipse.GetValue(IsAttachedProperty))
            return;

        FillDescriptor.RemoveValueChanged(ellipse, OnFillChanged);
        ellipse.SetValue(AppliedPinnedStateProperty, RelayIndicatorState.Auto);
        ellipse.BeginAnimation(Shape.FillProperty, null);
        ellipse.SetValue(IsAttachedProperty, false);
    }

    private static void OnFillChanged(object? sender, EventArgs e)
    {
        if (sender is Ellipse ellipse)
            ApplyVisualState(ellipse);
    }

    private static void ConfigurePhysicalLamp(Ellipse lens)
    {
        if (string.Equals(lens.Name, "TopHealthLed", StringComparison.Ordinal) ||
            lens.Parent is not Grid host)
        {
            ConfigureCompactLamp(lens);
            return;
        }

        if (LampParts.TryGetValue(lens, out _))
            return;

        var bezel = PhysicalEllipse(RelayLampBezelDiameter, RelayLampBezelBrush, 20);
        bezel.Effect = BezelShadow;

        var cavity = PhysicalEllipse(RelayLampCavityDiameter, RelayLampCavityBrush, 21);
        var halo = PhysicalEllipse(RelayLampHaloDiameter, TransparentHaloBrush, 22);
        halo.Opacity = 0;

        var highlight = PhysicalEllipse(3.2, RelayLampHighlightBrush, 24);
        highlight.Height = 2.15;
        highlight.Opacity = 0.38;
        highlight.RenderTransform = new TranslateTransform(-2.15, -2.35);

        CopyGridCell(lens, bezel);
        CopyGridCell(lens, cavity);
        CopyGridCell(lens, halo);
        CopyGridCell(lens, highlight);

        host.Children.Add(bezel);
        host.Children.Add(cavity);
        host.Children.Add(halo);
        host.Children.Add(highlight);

        lens.Width = RelayLampLensDiameter;
        lens.Height = RelayLampLensDiameter;
        lens.MinWidth = RelayLampLensDiameter;
        lens.MinHeight = RelayLampLensDiameter;
        lens.HorizontalAlignment = HorizontalAlignment.Center;
        lens.VerticalAlignment = VerticalAlignment.Center;
        lens.Stretch = Stretch.Fill;
        lens.Stroke = null;
        lens.StrokeThickness = 0;
        lens.Effect = null;
        lens.OpacityMask = null;
        lens.SnapsToDevicePixels = false;
        lens.UseLayoutRounding = true;
        lens.IsHitTestVisible = false;
        Panel.SetZIndex(lens, 23);

        LampParts.Add(lens, new LampVisualParts(bezel, cavity, halo, highlight));
    }

    private static void ConfigureCompactLamp(Ellipse lens)
    {
        lens.Width = 8;
        lens.Height = 8;
        lens.MinWidth = 8;
        lens.MinHeight = 8;
        lens.StrokeThickness = 1;
        lens.SnapsToDevicePixels = false;
        lens.UseLayoutRounding = true;
    }

    private static Ellipse PhysicalEllipse(double diameter, Brush fill, int zIndex)
    {
        var ellipse = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            MinWidth = diameter,
            MinHeight = diameter,
            Fill = fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            SnapsToDevicePixels = false,
            UseLayoutRounding = true
        };
        Panel.SetZIndex(ellipse, zIndex);
        return ellipse;
    }

    private static void CopyGridCell(UIElement source, UIElement target)
    {
        Grid.SetRow(target, Grid.GetRow(source));
        Grid.SetColumn(target, Grid.GetColumn(source));
        Grid.SetRowSpan(target, Grid.GetRowSpan(source));
        Grid.SetColumnSpan(target, Grid.GetColumnSpan(source));
    }

    private static void ApplyVisualState(Ellipse lens)
    {
        var pinned = GetPinnedState(lens);
        if (pinned != RelayIndicatorState.Auto)
        {
            ApplyResolvedState(lens, pinned, enforcePinnedFill: true);
            return;
        }

        ClearPinnedFillOverride(lens);
        if (lens.Fill is RadialGradientBrush)
            return;

        var active = IsActive(lens.Fill);
        var state = active ? ResolveActiveState(lens.Name, lens.Fill) : RelayIndicatorState.Off;
        ApplyResolvedState(lens, state, enforcePinnedFill: false);
    }

    private static void ApplyResolvedState(
        Ellipse lens,
        RelayIndicatorState state,
        bool enforcePinnedFill)
    {
        var visual = state switch
        {
            RelayIndicatorState.Green =>
                new LampStateVisual(GreenLampBrush, GreenHaloBrush, GreenGlow, 0.80, 0.94),
            RelayIndicatorState.Orange =>
                new LampStateVisual(OrangeLampBrush, OrangeHaloBrush, OrangeGlow, 0.80, 0.94),
            RelayIndicatorState.Red =>
                new LampStateVisual(RedLampBrush, RedHaloBrush, RedGlow, 0.80, 0.94),
            _ =>
                new LampStateVisual(OffLampBrush, TransparentHaloBrush, null, 0.0, 0.38)
        };

        if (enforcePinnedFill)
            ApplyPinnedFillOverride(lens, visual.Lens, state);
        else if (!ReferenceEquals(lens.Fill, visual.Lens))
            lens.Fill = visual.Lens;

        if (LampParts.TryGetValue(lens, out var parts))
        {
            parts.Halo.Fill = visual.Halo;
            parts.Halo.Effect = visual.Glow;
            parts.Halo.Opacity = visual.HaloOpacity;
            parts.Highlight.Opacity = visual.HighlightOpacity;
            lens.Stroke = null;
            lens.StrokeThickness = 0;
            lens.Effect = null;
            lens.Opacity = 1;
            return;
        }

        ApplyCompactLampVisual(lens, state, visual);
    }

    private static void ApplyCompactLampVisual(
        Ellipse lens,
        RelayIndicatorState state,
        LampStateVisual visual)
    {
        lens.Stroke = state == RelayIndicatorState.Off
            ? Freeze(new SolidColorBrush(Color.FromRgb(69, 82, 90)))
            : Freeze(new SolidColorBrush(Color.FromRgb(215, 225, 230)));
        lens.StrokeThickness = 1;
        lens.Effect = visual.Glow;
        lens.Opacity = state == RelayIndicatorState.Off ? 0.90 : 1.0;
    }

    private static void ApplyPinnedFillOverride(
        Ellipse lens,
        Brush fill,
        RelayIndicatorState state)
    {
        var applied = (RelayIndicatorState)lens.GetValue(AppliedPinnedStateProperty);
        if (applied == state)
            return;

        var animation = new ObjectAnimationUsingKeyFrames
        {
            Duration = new Duration(TimeSpan.Zero),
            FillBehavior = FillBehavior.HoldEnd
        };
        animation.KeyFrames.Add(new DiscreteObjectKeyFrame(fill, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.Freeze();

        lens.SetValue(AppliedPinnedStateProperty, state);
        try
        {
            lens.BeginAnimation(
                Shape.FillProperty,
                animation,
                HandoffBehavior.SnapshotAndReplace);
        }
        catch
        {
            lens.SetValue(AppliedPinnedStateProperty, RelayIndicatorState.Auto);
            throw;
        }
    }

    private static void ClearPinnedFillOverride(Ellipse lens)
    {
        if ((RelayIndicatorState)lens.GetValue(AppliedPinnedStateProperty) == RelayIndicatorState.Auto)
            return;

        lens.SetValue(AppliedPinnedStateProperty, RelayIndicatorState.Auto);
        lens.BeginAnimation(Shape.FillProperty, null);
    }

    private static RelayIndicatorState ResolveActiveState(string name, Brush source)
    {
        return name switch
        {
            "PickupLed" => RelayIndicatorState.Orange,
            "TripLed" => RelayIndicatorState.Red,
            "BlockLed" => RelayIndicatorState.Orange,
            "HealthyLed" or "TopHealthLed" =>
                IsHealthyColor(source) ? RelayIndicatorState.Green : RelayIndicatorState.Red,
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
        => brush is SolidColorBrush solid &&
           solid.Color.G > solid.Color.R &&
           solid.Color.G > solid.Color.B;

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
            GradientOrigin = new Point(0.28, 0.22),
            RadiusX = 0.73,
            RadiusY = 0.73,
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
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(244, 248, 250), 0.00));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(166, 178, 185), 0.25));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(75, 88, 96), 0.63));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(18, 25, 29), 1.00));
        return Freeze(brush);
    }

    private static Brush CreateCavityBrush()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.48, 0.46),
            GradientOrigin = new Point(0.37, 0.30),
            RadiusX = 0.72,
            RadiusY = 0.72
        };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(82, 95, 103), 0.00));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(32, 41, 46), 0.58));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(5, 8, 10), 1.00));
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

    private static Effect CreateDirectionalShadow(
        Color color,
        double blurRadius,
        double direction,
        double depth,
        double opacity)
        => Freeze(new DropShadowEffect
        {
            BlurRadius = blurRadius,
            Color = color,
            Direction = direction,
            ShadowDepth = depth,
            Opacity = opacity,
            RenderingBias = RenderingBias.Quality
        });

    private static T Freeze<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }

    private sealed record LampVisualParts(
        Ellipse Bezel,
        Ellipse Cavity,
        Ellipse Halo,
        Ellipse Highlight);

    private readonly record struct LampStateVisual(
        Brush Lens,
        Brush Halo,
        Effect? Glow,
        double HaloOpacity,
        double HighlightOpacity);
}
