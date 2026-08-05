using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private const int MaximumRelayFinalRefinementAttempts = 7;
    private const string RelayFinalGlossTag = "ARVREL_RELAY_FINAL_FULL_FACE_GLOSS";

    private static readonly Brush RelayFinalFasciaGloss = CreateRelayFinalFasciaGloss();
    private static readonly ControlTemplate RelayFinalKeyTemplate =
        ParseTemplate(RelayFinalKeyTemplateXaml);
    private static readonly ControlTemplate RelayFinalResetTemplate =
        ParseTemplate(RelayFinalResetTemplateXaml);

    private bool _relayFinalRefinementApplied;
    private int _relayFinalRefinementAttempts;

    internal void InitializeRelayFaceplateFinalRefinement()
    {
        if (_relayFinalRefinementApplied ||
            _relayFinalRefinementAttempts >= MaximumRelayFinalRefinementAttempts)
            return;

        // Use two SystemIdle hops. Every earlier Loaded handler gets to enqueue
        // its hardware/gloss/button work before this final authority is appended.
        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(DeferRelayFaceplateFinalRefinement));
    }

    private void DeferRelayFaceplateFinalRefinement()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(ApplyRelayFaceplateFinalRefinement));
    }

    private void ApplyRelayFaceplateFinalRefinement()
    {
        if (_relayFinalRefinementApplied ||
            _relayFinalRefinementAttempts >= MaximumRelayFinalRefinementAttempts)
            return;

        _relayFinalRefinementAttempts++;
        if (!_relayHardwarePresentationInitialized)
        {
            QueueRelayFinalRefinementRetry();
            return;
        }

        var relayBody = VisualAncestors<Border>(HealthyLed)
            .FirstOrDefault(border => border.CornerRadius.TopLeft >= 8);
        if (relayBody?.Child is not Grid bodyGrid ||
            !string.Equals(bodyGrid.Tag?.ToString(), BodyBevelTag, StringComparison.Ordinal))
        {
            QueueRelayFinalRefinementRetry();
            return;
        }

        ApplyFinalFullFasciaGloss(bodyGrid);
        var buttonCount = ApplyFinalRelayButtons(relayBody);
        ApplyFinalRelayLedDimensions();

        if (buttonCount == 0)
        {
            QueueRelayFinalRefinementRetry();
            return;
        }

        _relayFinalRefinementApplied = true;
    }

    private static void ApplyFinalFullFasciaGloss(Grid bodyGrid)
    {
        var gloss = bodyGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border =>
                string.Equals(border.Tag?.ToString(), RelayFinalGlossTag, StringComparison.Ordinal));

        if (gloss is null)
        {
            gloss = new Border { Tag = RelayFinalGlossTag };

            // Same-Z children paint in collection order. Insert first so the
            // clear coat is above the molded body but below every control.
            bodyGrid.Children.Insert(0, gloss);
        }

        Grid.SetRow(gloss, 0);
        Grid.SetColumn(gloss, 0);
        Grid.SetRowSpan(gloss, Math.Max(1, bodyGrid.RowDefinitions.Count));
        Grid.SetColumnSpan(gloss, Math.Max(1, bodyGrid.ColumnDefinitions.Count));

        gloss.Width = double.NaN;
        gloss.Height = double.NaN;
        gloss.Margin = new Thickness(1.1);
        gloss.CornerRadius = new CornerRadius(9.7);
        gloss.HorizontalAlignment = HorizontalAlignment.Stretch;
        gloss.VerticalAlignment = VerticalAlignment.Stretch;
        gloss.Background = RelayFinalFasciaGloss;
        gloss.Opacity = 1.0;
        gloss.IsHitTestVisible = false;
        gloss.CacheMode = new BitmapCache(1.0);
        Panel.SetZIndex(gloss, 0);
    }

    private static int ApplyFinalRelayButtons(Border relayBody)
    {
        var applied = 0;
        foreach (var button in VisualDescendants<Button>(relayBody))
        {
            if (ContainsHardwareText(button.Content as DependencyObject, "RESET TRIP"))
            {
                button.Template = RelayFinalResetTemplate;
                button.Background = Brushes.Transparent;
                button.BorderThickness = new Thickness(0);
                button.Foreground = CreateHardwareBrush("#2F6C91");
                button.Padding = new Thickness(10, 3, 10, 3);
                button.MinHeight = 29;
                button.Effect = null;
                button.CacheMode = null;
                button.FocusVisualStyle = null;
                button.UseLayoutRounding = true;
                button.SnapsToDevicePixels = true;
                applied++;
                continue;
            }

            var toolTip = button.ToolTip?.ToString();
            if (toolTip is null || !RelayHardwareKeyTips.Contains(toolTip))
                continue;

            button.Template = RelayFinalKeyTemplate;
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Effect = null;
            button.CacheMode = null;
            button.FocusVisualStyle = null;
            button.UseLayoutRounding = true;
            button.SnapsToDevicePixels = true;
            applied++;
        }

        return applied;
    }

    private void ApplyFinalRelayLedDimensions()
    {
        var relayLeds = new[]
        {
            HealthyLed,
            PickupLed,
            TripLed,
            PhaseALed,
            PhaseBLed,
            PhaseCLed,
            EarthLed,
            BlockLed
        };

        foreach (var led in relayLeds)
        {
            led.Width = 14;
            led.Height = 14;
            led.MinWidth = 14;
            led.MinHeight = 14;
            led.StrokeThickness = 2;
            led.UseLayoutRounding = true;
        }
    }

    private void QueueRelayFinalRefinementRetry()
    {
        if (_relayFinalRefinementAttempts >= MaximumRelayFinalRefinementAttempts)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(ApplyRelayFaceplateFinalRefinement));
    }

    private static Brush CreateRelayFinalFasciaGloss()
    {
        var clearCoat = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        clearCoat.GradientStops.Add(new GradientStop(Color.FromArgb(31, 255, 255, 255), 0.00));
        clearCoat.GradientStops.Add(new GradientStop(Color.FromArgb(13, 255, 255, 255), 0.30));
        clearCoat.GradientStops.Add(new GradientStop(Color.FromArgb(3, 255, 255, 255), 0.68));
        clearCoat.GradientStops.Add(new GradientStop(Color.FromArgb(10, 18, 30, 38), 1.00));
        clearCoat.Freeze();

        var broadReflection = new LinearGradientBrush
        {
            StartPoint = new Point(0.05, 0),
            EndPoint = new Point(0.86, 1)
        };
        broadReflection.GradientStops.Add(new GradientStop(Color.FromArgb(52, 255, 255, 255), 0.00));
        broadReflection.GradientStops.Add(new GradientStop(Color.FromArgb(31, 255, 255, 255), 0.38));
        broadReflection.GradientStops.Add(new GradientStop(Color.FromArgb(15, 255, 255, 255), 0.72));
        broadReflection.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.00));
        broadReflection.Freeze();

        var edgeReflection = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        edgeReflection.GradientStops.Add(new GradientStop(Color.FromArgb(37, 255, 255, 255), 0.00));
        edgeReflection.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.00));
        edgeReflection.Freeze();

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(
            clearCoat,
            null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));
        drawing.Children.Add(new GeometryDrawing(
            broadReflection,
            null,
            Geometry.Parse("M 0,0 L 0.72,0 L 0.24,1 L 0,1 Z")));
        drawing.Children.Add(new GeometryDrawing(
            edgeReflection,
            null,
            Geometry.Parse("M 0,0 L 0.13,0 L 0,0.34 Z")));
        drawing.Freeze();

        var brush = new DrawingBrush(drawing)
        {
            Viewbox = new Rect(0, 0, 1, 1),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 1, 1),
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Stretch = Stretch.Fill,
            TileMode = TileMode.None
        };
        brush.Freeze();
        return brush;
    }

    private const string RelayFinalKeyTemplateXaml = """
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 TargetType="{x:Type Button}">
    <Grid x:Name="Root" RenderTransformOrigin="0.5,0.5">
        <Grid.RenderTransform><TranslateTransform Y="0" /></Grid.RenderTransform>
        <Border x:Name="Base" Margin="1,2.5,1,0" CornerRadius="4"
                Background="#172128" BorderBrush="#0A0F13" BorderThickness="1" />
        <Border x:Name="Face" Margin="0,0,0,2.5" CornerRadius="4" BorderThickness="1">
            <Border.BorderBrush>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#A0ADB5" Offset="0" />
                    <GradientStop Color="#53626B" Offset="0.40" />
                    <GradientStop Color="#151E24" Offset="1" />
                </LinearGradientBrush>
            </Border.BorderBrush>
            <Border.Background>
                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                    <GradientStop Color="#4D5D66" Offset="0" />
                    <GradientStop Color="#3A484F" Offset="0.20" />
                    <GradientStop Color="#2D3940" Offset="0.64" />
                    <GradientStop Color="#202A30" Offset="1" />
                </LinearGradientBrush>
            </Border.Background>
            <Border.Effect>
                <DropShadowEffect Color="#11191E" BlurRadius="4" Direction="270"
                                  ShadowDepth="0.8" Opacity="0.34"
                                  RenderingBias="Performance" />
            </Border.Effect>
            <Grid ClipToBounds="True">
                <Border x:Name="Gloss" Height="8" Margin="2,1,2,0"
                        CornerRadius="3,3,1,1" VerticalAlignment="Top">
                    <Border.Background>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                            <GradientStop Color="#50FFFFFF" Offset="0" />
                            <GradientStop Color="#18FFFFFF" Offset="0.55" />
                            <GradientStop Color="#00FFFFFF" Offset="1" />
                        </LinearGradientBrush>
                    </Border.Background>
                </Border>
                <Border x:Name="LowerLip" Height="2" Margin="2,0,2,0.5"
                        CornerRadius="0,0,3,3" VerticalAlignment="Bottom"
                        Background="#6210181D" />
                <Border x:Name="Hover" Background="#00FFFFFF" CornerRadius="3" />
                <ContentPresenter Margin="{TemplateBinding Padding}"
                                  HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                  VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                  RecognizesAccessKey="True"
                                  TextElement.Foreground="{TemplateBinding Foreground}" />
            </Grid>
        </Border>
    </Grid>
    <ControlTemplate.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Hover" Property="Background" Value="#0CFFFFFF" />
            <Setter TargetName="Gloss" Property="Opacity" Value="0.96" />
        </Trigger>
        <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Root" Property="RenderTransform">
                <Setter.Value><TranslateTransform Y="0.5" /></Setter.Value>
            </Setter>
            <Setter TargetName="Face" Property="Margin" Value="0,0.5,0,2" />
            <Setter TargetName="Base" Property="Opacity" Value="0.90" />
            <Setter TargetName="Gloss" Property="Opacity" Value="0.62" />
            <Setter TargetName="LowerLip" Property="Height" Value="1.5" />
            <Setter TargetName="Hover" Property="Background" Value="#08000000" />
        </Trigger>
        <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Root" Property="Opacity" Value="0.46" />
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
""";

    private const string RelayFinalResetTemplateXaml = """
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 TargetType="{x:Type Button}">
    <Grid x:Name="Root" RenderTransformOrigin="0.5,0.5">
        <Grid.RenderTransform><TranslateTransform Y="0" /></Grid.RenderTransform>
        <Border x:Name="Base" Margin="1,2.5,1,0" CornerRadius="4"
                Background="#89969D" BorderBrush="#65747D" BorderThickness="1" />
        <Border x:Name="Face" Margin="0,0,0,2.5" CornerRadius="4" BorderThickness="1">
            <Border.BorderBrush>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#FFFFFF" Offset="0" />
                    <GradientStop Color="#C1CDD3" Offset="0.52" />
                    <GradientStop Color="#788891" Offset="1" />
                </LinearGradientBrush>
            </Border.BorderBrush>
            <Border.Background>
                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                    <GradientStop Color="#FFFFFF" Offset="0" />
                    <GradientStop Color="#F7F9FA" Offset="0.25" />
                    <GradientStop Color="#E3E9EC" Offset="0.70" />
                    <GradientStop Color="#CDD6DB" Offset="1" />
                </LinearGradientBrush>
            </Border.Background>
            <Border.Effect>
                <DropShadowEffect Color="#26343D" BlurRadius="4" Direction="270"
                                  ShadowDepth="0.8" Opacity="0.28"
                                  RenderingBias="Performance" />
            </Border.Effect>
            <Grid ClipToBounds="True">
                <Border x:Name="Gloss" Height="7" Margin="2,1,2,0"
                        CornerRadius="3,3,1,1" VerticalAlignment="Top">
                    <Border.Background>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                            <GradientStop Color="#B8FFFFFF" Offset="0" />
                            <GradientStop Color="#38FFFFFF" Offset="0.58" />
                            <GradientStop Color="#00FFFFFF" Offset="1" />
                        </LinearGradientBrush>
                    </Border.Background>
                </Border>
                <Border x:Name="LowerLip" Height="2" Margin="2,0,2,0.5"
                        CornerRadius="0,0,3,3" VerticalAlignment="Bottom"
                        Background="#5063727B" />
                <Border x:Name="Hover" Background="#00FFFFFF" CornerRadius="3" />
                <ContentPresenter Margin="{TemplateBinding Padding}"
                                  HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                  VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                  RecognizesAccessKey="True"
                                  TextElement.Foreground="{TemplateBinding Foreground}" />
            </Grid>
        </Border>
    </Grid>
    <ControlTemplate.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Hover" Property="Background" Value="#0CFFFFFF" />
        </Trigger>
        <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Root" Property="RenderTransform">
                <Setter.Value><TranslateTransform Y="0.5" /></Setter.Value>
            </Setter>
            <Setter TargetName="Face" Property="Margin" Value="0,0.5,0,2" />
            <Setter TargetName="Base" Property="Opacity" Value="0.90" />
            <Setter TargetName="Gloss" Property="Opacity" Value="0.66" />
            <Setter TargetName="LowerLip" Property="Height" Value="1.5" />
            <Setter TargetName="Hover" Property="Background" Value="#07000000" />
        </Trigger>
        <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Root" Property="Opacity" Value="0.48" />
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
""";
}

internal static class RelayFaceplateFinalRefinementBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.InitializeRelayFaceplateFinalRefinement();
    }
}
