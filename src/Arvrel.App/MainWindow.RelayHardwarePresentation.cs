using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private const int MaximumRelayHardwarePresentationAttempts = 4;
    private const string RecessTag = "ARVREL_TRUE_3D_RECESS";
    private const string BodyBevelTag = "ARVREL_TRUE_3D_BODY";

    private static readonly Brush RelayMountBackground = CreateVerticalGradient(
        ("#EEF2F4", 0.00),
        ("#D5DDE2", 0.42),
        ("#B8C4CB", 1.00));
    private static readonly Brush RelayBodyBackground = CreateVerticalGradient(
        ("#D4DEE4", 0.00),
        ("#BBC8D0", 0.15),
        ("#9FAFBA", 0.58),
        ("#899BA7", 1.00));
    private static readonly Brush RelayBodyBorder = CreateDiagonalGradient(
        ("#F4F8FA", 0.00),
        ("#AEBBC4", 0.34),
        ("#60727E", 1.00));
    private static readonly Brush RelayBodyInnerBevel = CreateDiagonalGradient(
        ("#BFFFFFFF", 0.00),
        ("#50FFFFFF", 0.27),
        ("#22000000", 0.74),
        ("#700E171C", 1.00));
    private static readonly Brush RelayBodyBottomLip = CreateVerticalGradient(
        ("#00101A20", 0.00),
        ("#55313F47", 0.38),
        ("#A44D5E68", 1.00));
    private static readonly Brush RelayBodyTopSheen = CreateVerticalGradient(
        ("#72FFFFFF", 0.00),
        ("#28FFFFFF", 0.42),
        ("#00FFFFFF", 1.00));

    private static readonly Brush RecessWellBackground = CreateDiagonalGradient(
        ("#53636D", 0.00),
        ("#71818B", 0.36),
        ("#AEB9C0", 1.00));
    private static readonly Brush RecessWellBorder = CreateDiagonalGradient(
        ("#44535C", 0.00),
        ("#72828D", 0.52),
        ("#E2E8EB", 1.00));
    private static readonly Brush RecessTopShade = CreateVerticalGradient(
        ("#8B1D282F", 0.00),
        ("#421D282F", 0.58),
        ("#001D282F", 1.00));
    private static readonly Brush RecessLeftShade = CreateHorizontalGradient(
        ("#76192329", 0.00),
        ("#35192329", 0.56),
        ("#00192329", 1.00));
    private static readonly Brush RecessBottomHighlight = CreateVerticalGradient(
        ("#00FFFFFF", 0.00),
        ("#A4FFFFFF", 1.00));
    private static readonly Brush LcdFaceBackground = CreateVerticalGradient(
        ("#F5F8F1", 0.00),
        ("#EDF2EC", 0.45),
        ("#DCE5DD", 1.00));
    private static readonly Brush IndicatorFaceBackground = CreateVerticalGradient(
        ("#F9FBFC", 0.00),
        ("#E8EDF0", 0.52),
        ("#D4DDE2", 1.00));
    private static readonly Brush InnerFaceBorder = CreateDiagonalGradient(
        ("#F7FFFFFF", 0.00),
        ("#AAB8C0", 0.56),
        ("#6C7C86", 1.00));

    private static readonly Effect RelayBodyShadow = CreateHardwareShadow(
        "#26343D", 18, 315, 8, 0.40);

    private static readonly ControlTemplate RelayKey3DTemplate = ParseTemplate(RelayKeyTemplateXaml);
    private static readonly ControlTemplate RelayReset3DTemplate = ParseTemplate(RelayResetTemplateXaml);

    private static readonly HashSet<string> RelayHardwareKeyTips = new(StringComparer.Ordinal)
    {
        "Up",
        "Down",
        "Enter",
        "Next",
        "Cancel",
        "Reset trip"
    };

    private bool _relayHardwarePresentationInitialized;
    private int _relayHardwarePresentationAttempts;

    internal void InitializeRelayHardwarePresentation()
    {
        if (_relayHardwarePresentationInitialized ||
            _relayHardwarePresentationAttempts >= MaximumRelayHardwarePresentationAttempts)
            return;

        // The LCD presenter reparents its content at Loaded priority. Apply the
        // hardware shell after those owners settle so the bevel is attached once.
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(ApplyRelayHardwarePresentation));
    }

    private void ApplyRelayHardwarePresentation()
    {
        if (_relayHardwarePresentationInitialized ||
            _relayHardwarePresentationAttempts >= MaximumRelayHardwarePresentationAttempts)
            return;

        _relayHardwarePresentationAttempts++;
        var borderAncestors = VisualAncestors<Border>(HealthyLed).ToArray();
        var indicatorPanel = borderAncestors.FirstOrDefault();
        var relayBody = borderAncestors.FirstOrDefault(border => border.CornerRadius.TopLeft >= 8);
        var relayPanel = relayBody is null
            ? null
            : borderAncestors.SkipWhile(border => !ReferenceEquals(border, relayBody)).Skip(1).FirstOrDefault();

        if (indicatorPanel is null || relayBody is null || relayPanel is null)
        {
            if (_relayHardwarePresentationAttempts < MaximumRelayHardwarePresentationAttempts)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.ContextIdle,
                    new Action(ApplyRelayHardwarePresentation));
            }
            return;
        }

        _relayHardwarePresentationInitialized = true;

        ApplyMountingWell(relayPanel);
        ApplyRaisedRelayBody(relayBody);
        ApplyRelayLcdRecess(relayBody);
        ApplyRecessedModule(indicatorPanel, IndicatorFaceBackground, 4.0, 3.0);
        ApplyTrue3DButtons(relayBody);
    }

    private static void ApplyMountingWell(Border relayPanel)
    {
        relayPanel.Background = RelayMountBackground;
        relayPanel.BorderBrush = CreateHardwareBrush("#A3B0B8");
        relayPanel.BorderThickness = new Thickness(1);
        relayPanel.CornerRadius = new CornerRadius(6);
        relayPanel.SnapsToDevicePixels = true;
    }

    private static void ApplyRaisedRelayBody(Border relayBody)
    {
        relayBody.Background = RelayBodyBackground;
        relayBody.BorderBrush = RelayBodyBorder;
        relayBody.BorderThickness = new Thickness(1.6);
        relayBody.CornerRadius = new CornerRadius(11);
        relayBody.Effect = RelayBodyShadow;
        relayBody.CacheMode = new BitmapCache(1.0);

        if (relayBody.Child is not Grid bodyGrid ||
            string.Equals(bodyGrid.Tag?.ToString(), BodyBevelTag, StringComparison.Ordinal))
            return;

        bodyGrid.Tag = BodyBevelTag;

        var topSheen = new Border
        {
            Height = 62,
            Margin = new Thickness(2, 2, 2, 0),
            CornerRadius = new CornerRadius(8, 8, 2, 2),
            Background = RelayBodyTopSheen,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false
        };
        Grid.SetRowSpan(topSheen, Math.Max(1, bodyGrid.RowDefinitions.Count));
        Panel.SetZIndex(topSheen, 20);
        bodyGrid.Children.Add(topSheen);

        var innerBevel = new Border
        {
            Margin = new Thickness(1.5),
            CornerRadius = new CornerRadius(9),
            BorderBrush = RelayBodyInnerBevel,
            BorderThickness = new Thickness(1.4),
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Grid.SetRowSpan(innerBevel, Math.Max(1, bodyGrid.RowDefinitions.Count));
        Panel.SetZIndex(innerBevel, 21);
        bodyGrid.Children.Add(innerBevel);

        var lowerLip = new Border
        {
            Height = 7,
            Margin = new Thickness(3, 0, 3, 2),
            CornerRadius = new CornerRadius(0, 0, 7, 7),
            Background = RelayBodyBottomLip,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false
        };
        Grid.SetRowSpan(lowerLip, Math.Max(1, bodyGrid.RowDefinitions.Count));
        Panel.SetZIndex(lowerLip, 22);
        bodyGrid.Children.Add(lowerLip);
    }

    private void ApplyRelayLcdRecess(Border relayBody)
    {
        if (_relayLcdHeader is null)
            return;

        var lcdBezel = VisualDescendants<Border>(relayBody)
            .FirstOrDefault(border => IsVisualAncestor(border, _relayLcdHeader));
        if (lcdBezel is null)
            return;

        ApplyRecessedModule(lcdBezel, LcdFaceBackground, 5.0, 4.0);
    }

    private static void ApplyRecessedModule(
        Border shell,
        Brush faceBackground,
        double outerRadius,
        double innerRadius)
    {
        if (string.Equals(shell.Tag?.ToString(), RecessTag, StringComparison.Ordinal) ||
            shell.Child is not UIElement originalChild)
            return;

        shell.Tag = RecessTag;
        var originalPadding = shell.Padding;
        shell.Child = null;
        shell.Padding = new Thickness(0);
        shell.Background = RecessWellBackground;
        shell.BorderBrush = RecessWellBorder;
        shell.BorderThickness = new Thickness(2);
        shell.CornerRadius = new CornerRadius(outerRadius);
        shell.Effect = null;

        const double wellDepth = 4;
        var face = new Border
        {
            Margin = new Thickness(wellDepth, wellDepth, wellDepth, wellDepth + 1),
            Padding = SubtractThickness(originalPadding, wellDepth),
            Background = faceBackground,
            BorderBrush = InnerFaceBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(innerRadius),
            Child = originalChild,
            SnapsToDevicePixels = true
        };

        var recess = new Grid
        {
            ClipToBounds = true
        };
        recess.Children.Add(face);
        recess.Children.Add(new Border
        {
            Height = 8,
            Margin = new Thickness(2, 2, 2, 0),
            CornerRadius = new CornerRadius(Math.Max(0, outerRadius - 1), Math.Max(0, outerRadius - 1), 0, 0),
            Background = RecessTopShade,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false
        });
        recess.Children.Add(new Border
        {
            Width = 7,
            Margin = new Thickness(2, 2, 0, 2),
            CornerRadius = new CornerRadius(Math.Max(0, outerRadius - 1), 0, 0, Math.Max(0, outerRadius - 1)),
            Background = RecessLeftShade,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false
        });
        recess.Children.Add(new Border
        {
            Height = 2,
            Margin = new Thickness(5, 0, 5, 2),
            Background = RecessBottomHighlight,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false
        });

        shell.Child = recess;
    }

    private static void ApplyTrue3DButtons(Border relayBody)
    {
        foreach (var button in VisualDescendants<Button>(relayBody))
        {
            var toolTip = button.ToolTip?.ToString();
            if (toolTip is not null && RelayHardwareKeyTips.Contains(toolTip))
            {
                button.Template = RelayKey3DTemplate;
                button.Background = Brushes.Transparent;
                button.BorderThickness = new Thickness(0);
                button.Effect = null;
                button.CacheMode = new BitmapCache(1.0);
                button.FocusVisualStyle = null;
                continue;
            }

            if (!ContainsHardwareText(button.Content as DependencyObject, "RESET TRIP"))
                continue;

            button.Template = RelayReset3DTemplate;
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Foreground = CreateHardwareBrush("#2E6F9E");
            button.Padding = new Thickness(10, 4, 10, 4);
            button.MinHeight = 32;
            button.Effect = null;
            button.CacheMode = new BitmapCache(1.0);
            button.FocusVisualStyle = null;
        }
    }

    private static ControlTemplate ParseTemplate(string source)
    {
        var template = XamlReader.Parse(source) as ControlTemplate;
        return template ?? throw new InvalidOperationException("ARVREL relay 3D button template could not be parsed.");
    }

    private static Thickness SubtractThickness(Thickness value, double amount)
        => new(
            Math.Max(0, value.Left - amount),
            Math.Max(0, value.Top - amount),
            Math.Max(0, value.Right - amount),
            Math.Max(0, value.Bottom - amount));

    private static bool ContainsHardwareText(DependencyObject? node, string expected)
    {
        if (node is null)
            return false;
        if (node is TextBlock text && string.Equals(text.Text, expected, StringComparison.Ordinal))
            return true;

        foreach (var child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is DependencyObject dependency && ContainsHardwareText(dependency, expected))
                return true;
        }
        return false;
    }

    private static bool IsVisualAncestor(DependencyObject ancestor, DependencyObject descendant)
    {
        DependencyObject? current = descendant;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static IEnumerable<T> VisualAncestors<T>(DependencyObject source)
        where T : DependencyObject
    {
        DependencyObject? current = source;
        while ((current = VisualTreeHelper.GetParent(current)) is not null)
        {
            if (current is T typed)
                yield return typed;
        }
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;
            foreach (var descendant in VisualDescendants<T>(child))
                yield return descendant;
        }
    }

    private static Brush CreateVerticalGradient(params (string Color, double Offset)[] stops)
        => CreateGradient(new Point(0, 0), new Point(0, 1), stops);

    private static Brush CreateHorizontalGradient(params (string Color, double Offset)[] stops)
        => CreateGradient(new Point(0, 0), new Point(1, 0), stops);

    private static Brush CreateDiagonalGradient(params (string Color, double Offset)[] stops)
        => CreateGradient(new Point(0, 0), new Point(1, 1), stops);

    private static Brush CreateGradient(
        Point start,
        Point end,
        params (string Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = start,
            EndPoint = end
        };
        foreach (var stop in stops)
        {
            brush.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(stop.Color)!,
                stop.Offset));
        }
        brush.Freeze();
        return brush;
    }

    private static Brush CreateHardwareBrush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }

    private static Effect CreateHardwareShadow(
        string value,
        double blurRadius,
        double direction,
        double depth,
        double opacity)
    {
        var effect = new DropShadowEffect
        {
            Color = (Color)ColorConverter.ConvertFromString(value)!,
            BlurRadius = blurRadius,
            Direction = direction,
            ShadowDepth = depth,
            Opacity = opacity,
            RenderingBias = RenderingBias.Performance
        };
        effect.Freeze();
        return effect;
    }

    private const string RelayKeyTemplateXaml = """
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 TargetType="{x:Type Button}">
    <Grid x:Name="Root" RenderTransformOrigin="0.5,0.5">
        <Grid.RenderTransform>
            <TranslateTransform Y="0" />
        </Grid.RenderTransform>

        <Border x:Name="Base"
                Margin="1,5,1,0"
                CornerRadius="5"
                Background="#10171C"
                BorderBrush="#070B0E"
                BorderThickness="1" />

        <Border x:Name="Face"
                Margin="0,0,0,5"
                CornerRadius="5"
                BorderThickness="1.5">
            <Border.BorderBrush>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#A9BAC5" Offset="0" />
                    <GradientStop Color="#53636D" Offset="0.35" />
                    <GradientStop Color="#111A20" Offset="1" />
                </LinearGradientBrush>
            </Border.BorderBrush>
            <Border.Background>
                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                    <GradientStop Color="#566773" Offset="0" />
                    <GradientStop Color="#34434D" Offset="0.18" />
                    <GradientStop Color="#26333C" Offset="0.55" />
                    <GradientStop Color="#172129" Offset="1" />
                </LinearGradientBrush>
            </Border.Background>
            <Border.Effect>
                <DropShadowEffect Color="#11191E"
                                  BlurRadius="7"
                                  Direction="270"
                                  ShadowDepth="3"
                                  Opacity="0.56"
                                  RenderingBias="Performance" />
            </Border.Effect>
            <Grid ClipToBounds="True">
                <Border x:Name="Gloss"
                        Height="13"
                        Margin="3,2,3,0"
                        CornerRadius="3,3,1,1"
                        VerticalAlignment="Top">
                    <Border.Background>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                            <GradientStop Color="#82FFFFFF" Offset="0" />
                            <GradientStop Color="#28FFFFFF" Offset="0.52" />
                            <GradientStop Color="#00FFFFFF" Offset="1" />
                        </LinearGradientBrush>
                    </Border.Background>
                </Border>
                <Border x:Name="LowerLip"
                        Height="4"
                        Margin="2,0,2,1"
                        CornerRadius="0,0,3,3"
                        VerticalAlignment="Bottom"
                        Background="#8D0E151A" />
                <Border x:Name="Hover"
                        Background="#00FFFFFF"
                        CornerRadius="4" />
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
            <Setter TargetName="Hover" Property="Background" Value="#14FFFFFF" />
            <Setter TargetName="Gloss" Property="Opacity" Value="1" />
        </Trigger>
        <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Root" Property="RenderTransform">
                <Setter.Value><TranslateTransform Y="3" /></Setter.Value>
            </Setter>
            <Setter TargetName="Face" Property="Margin" Value="0,2,0,2" />
            <Setter TargetName="Face" Property="Effect" Value="{x:Null}" />
            <Setter TargetName="Base" Property="Opacity" Value="0.25" />
            <Setter TargetName="Gloss" Property="Opacity" Value="0.18" />
            <Setter TargetName="LowerLip" Property="Height" Value="1" />
            <Setter TargetName="Hover" Property="Background" Value="#24000000" />
        </Trigger>
        <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter TargetName="Face" Property="BorderBrush" Value="#76B4DC" />
        </Trigger>
        <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Root" Property="Opacity" Value="0.42" />
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
""";

    private const string RelayResetTemplateXaml = """
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 TargetType="{x:Type Button}">
    <Grid x:Name="Root" RenderTransformOrigin="0.5,0.5">
        <Grid.RenderTransform>
            <TranslateTransform Y="0" />
        </Grid.RenderTransform>

        <Border x:Name="Base"
                Margin="1,5,1,0"
                CornerRadius="5"
                Background="#788690"
                BorderBrush="#596872"
                BorderThickness="1" />

        <Border x:Name="Face"
                Margin="0,0,0,5"
                CornerRadius="5"
                BorderThickness="1.4">
            <Border.BorderBrush>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#FFFFFF" Offset="0" />
                    <GradientStop Color="#B9C7D0" Offset="0.48" />
                    <GradientStop Color="#70818C" Offset="1" />
                </LinearGradientBrush>
            </Border.BorderBrush>
            <Border.Background>
                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                    <GradientStop Color="#FFFFFF" Offset="0" />
                    <GradientStop Color="#F5F8FA" Offset="0.20" />
                    <GradientStop Color="#DCE5EA" Offset="0.68" />
                    <GradientStop Color="#C2CED6" Offset="1" />
                </LinearGradientBrush>
            </Border.Background>
            <Border.Effect>
                <DropShadowEffect Color="#26343D"
                                  BlurRadius="8"
                                  Direction="270"
                                  ShadowDepth="3"
                                  Opacity="0.38"
                                  RenderingBias="Performance" />
            </Border.Effect>
            <Grid ClipToBounds="True">
                <Border x:Name="Gloss"
                        Height="12"
                        Margin="3,2,3,0"
                        CornerRadius="3,3,1,1"
                        VerticalAlignment="Top">
                    <Border.Background>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                            <GradientStop Color="#E6FFFFFF" Offset="0" />
                            <GradientStop Color="#54FFFFFF" Offset="0.55" />
                            <GradientStop Color="#00FFFFFF" Offset="1" />
                        </LinearGradientBrush>
                    </Border.Background>
                </Border>
                <Border x:Name="LowerLip"
                        Height="3"
                        Margin="2,0,2,1"
                        CornerRadius="0,0,3,3"
                        VerticalAlignment="Bottom"
                        Background="#5E637682" />
                <Border x:Name="Hover" Background="#00FFFFFF" CornerRadius="4" />
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
            <Setter TargetName="Hover" Property="Background" Value="#122E6F9E" />
        </Trigger>
        <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Root" Property="RenderTransform">
                <Setter.Value><TranslateTransform Y="3" /></Setter.Value>
            </Setter>
            <Setter TargetName="Face" Property="Margin" Value="0,2,0,2" />
            <Setter TargetName="Face" Property="Effect" Value="{x:Null}" />
            <Setter TargetName="Base" Property="Opacity" Value="0.28" />
            <Setter TargetName="Gloss" Property="Opacity" Value="0.20" />
            <Setter TargetName="LowerLip" Property="Height" Value="1" />
            <Setter TargetName="Hover" Property="Background" Value="#1D40596A" />
        </Trigger>
        <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter TargetName="Face" Property="BorderBrush" Value="#2E6F9E" />
        </Trigger>
        <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Root" Property="Opacity" Value="0.45" />
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
""";
}

internal static class RelayHardwarePresentationBootstrap
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
            window.InitializeRelayHardwarePresentation();
    }
}
