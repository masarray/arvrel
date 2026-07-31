using System.Windows;
using System.Windows.Media;

namespace Arvrel.App.Controls;

public enum LucideIconKind
{
    FileInput,
    Code2,
    Play,
    Pause,
    Zap,
    ShieldAlert,
    RotateCcw,
    Download,
    ChevronUp,
    CornerDownLeft,
    ChevronRight,
    ChevronDown,
    X,
    Square,
    Network,
    FolderOpen,
    Save,
    SlidersHorizontal,
    Activity,
    Radio,
    CircleStop
}

public sealed class LucideIcon : FrameworkElement
{
    private static readonly IReadOnlyDictionary<LucideIconKind, IconDefinition> Definitions = BuildDefinitions();

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(LucideIconKind),
        typeof(LucideIcon),
        new FrameworkPropertyMetadata(LucideIconKind.Activity, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground),
        typeof(Brush),
        typeof(LucideIcon),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled),
        typeof(bool),
        typeof(LucideIcon),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public LucideIconKind Kind
    {
        get => (LucideIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public bool Filled
    {
        get => (bool)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsNaN(Width) ? 18 : Width;
        var height = double.IsNaN(Height) ? 18 : Height;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (!Definitions.TryGetValue(Kind, out var definition))
            return;

        var size = Math.Max(1, Math.Min(ActualWidth, ActualHeight));
        var scale = size / 24.0;
        var left = (ActualWidth - size) / 2.0;
        var top = (ActualHeight - size) / 2.0;
        drawingContext.PushTransform(new TranslateTransform(left, top));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));

        var pen = new Pen(Foreground, 2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (pen.CanFreeze)
            pen.Freeze();

        foreach (var part in definition.Parts)
        {
            var fill = Filled && part.CanFill ? Foreground : null;
            var stroke = fill is null ? pen : null;
            drawingContext.DrawGeometry(fill, stroke, part.Geometry);
        }

        drawingContext.Pop();
        drawingContext.Pop();
    }

    private static IReadOnlyDictionary<LucideIconKind, IconDefinition> BuildDefinitions()
    {
        return new Dictionary<LucideIconKind, IconDefinition>
        {
            [LucideIconKind.FileInput] = Def(
                P("M14,2 H6 A2,2 0 0 0 4,4 V20 A2,2 0 0 0 6,22 H18 A2,2 0 0 0 20,20 V8 Z"),
                P("M14,2 V8 H20"),
                P("M12,18 V12 M9,15 L12,12 L15,15")),
            [LucideIconKind.Code2] = Def(
                P("M16,18 L22,12 L16,6"),
                P("M8,6 L2,12 L8,18"),
                P("M14.5,4 L9.5,20")),
            [LucideIconKind.Play] = Def(P("M5,5 A2,2 0 0 1 8.008,3.272 L20.005,10.27 A2,2 0 0 1 20.008,13.728 L8.008,20.728 A2,2 0 0 1 5,19 Z", true)),
            [LucideIconKind.Pause] = Def(P("M6,4 H10 V20 H6 Z", true), P("M14,4 H18 V20 H14 Z", true)),
            [LucideIconKind.Zap] = Def(P("M13,2 L3,14 H12 L11,22 L21,10 H12 Z", true)),
            [LucideIconKind.ShieldAlert] = Def(
                P("M20,13 C20,18 16.5,20.5 12,22 C7.5,20.5 4,18 4,13 V5 L12,2 L20,5 Z"),
                P("M12,8 V12"),
                P("M12,16 H12.01")),
            [LucideIconKind.RotateCcw] = Def(
                P("M3,12 A9,9 0 1 0 6,5.3"),
                P("M3,3 V8 H8")),
            [LucideIconKind.Download] = Def(
                P("M12,3 V15"),
                P("M7,10 L12,15 L17,10"),
                P("M5,21 H19")),
            [LucideIconKind.ChevronUp] = Def(P("M18,15 L12,9 L6,15")),
            [LucideIconKind.CornerDownLeft] = Def(P("M9,10 L4,15 L9,20 M4,15 H16 A4,4 0 0 0 20,11 V4")),
            [LucideIconKind.ChevronRight] = Def(P("M9,18 L15,12 L9,6")),
            [LucideIconKind.ChevronDown] = Def(P("M6,9 L12,15 L18,9")),
            [LucideIconKind.X] = Def(P("M18,6 L6,18 M6,6 L18,18")),
            [LucideIconKind.Square] = Def(P("M5,5 H19 V19 H5 Z", true)),
            [LucideIconKind.Network] = Def(
                P("M9,3 H15 V9 H9 Z"),
                P("M3,15 H9 V21 H3 Z"),
                P("M15,15 H21 V21 H15 Z"),
                P("M12,9 V12 M6,15 V12 H18 V15")),
            [LucideIconKind.FolderOpen] = Def(
                P("M6,14 L8,6 H20 A2,2 0 0 1 22,8 L20,18 A2,2 0 0 1 18,20 H4 A2,2 0 0 1 2,18 V6 A2,2 0 0 1 4,4 H9 L11,7 H18")),
            [LucideIconKind.Save] = Def(
                P("M5,3 H17 L21,7 V21 H3 V5 A2,2 0 0 1 5,3 Z"),
                P("M7,3 V8 H16 V3"),
                P("M7,21 V14 H17 V21")),
            [LucideIconKind.SlidersHorizontal] = Def(
                P("M21,4 H14 M10,4 H3 M21,12 H12 M8,12 H3 M21,20 H16 M12,20 H3"),
                P("M14,2 V6 M12,10 V14 M16,18 V22")),
            [LucideIconKind.Activity] = Def(P("M3,12 H7 L10,3 L14,21 L17,12 H21")),
            [LucideIconKind.Radio] = Def(
                P("M4.9,4.9 A10,10 0 0 0 4.9,19.1"),
                P("M19.1,4.9 A10,10 0 0 1 19.1,19.1"),
                P("M8.5,8.5 A5,5 0 0 0 8.5,15.5"),
                P("M15.5,8.5 A5,5 0 0 1 15.5,15.5"),
                P("M12,12 H12.01")),
            [LucideIconKind.CircleStop] = Def(
                P("M12,2 A10,10 0 1 1 12,22 A10,10 0 1 1 12,2 Z"),
                P("M8,8 H16 V16 H8 Z", true))
        };
    }

    private static IconDefinition Def(params IconPart[] parts) => new(parts);

    private static IconPart P(string data, bool canFill = false)
    {
        var geometry = Geometry.Parse(data);
        if (geometry.CanFreeze)
            geometry.Freeze();
        return new IconPart(geometry, canFill);
    }

    private sealed record IconDefinition(IReadOnlyList<IconPart> Parts);
    private sealed record IconPart(Geometry Geometry, bool CanFill);
}
