using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Arvrel.App.Controls;

/// <summary>
/// Applies a hardware-like tactile treatment to the virtual relay faceplate controls
/// without changing their commands, click handlers or navigation behavior.
/// </summary>
internal static class RelayTactileButtonBehavior
{
    private static readonly HashSet<string> KeypadToolTips = new(StringComparer.Ordinal)
    {
        "Up",
        "Down",
        "Enter",
        "Next",
        "Cancel",
        "Reset trip"
    };

    private static readonly Lazy<ResourceDictionary> TactileResources = new(CreateResources);
    private static readonly Brush ResetForeground = CreateFrozenBrush(Color.FromRgb(239, 244, 247));

    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(Button),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnButtonLoaded));
    }

    private static void OnButtonLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag as string == "ARVREL_TACTILE")
            return;

        var toolTip = button.ToolTip?.ToString();
        if (toolTip is not null && KeypadToolTips.Contains(toolTip))
        {
            button.Style = (Style)TactileResources.Value["RelayTactileKey"];
            button.Tag = "ARVREL_TACTILE";
            return;
        }

        if (!ContainsText(button.Content as DependencyObject, "RESET TRIP"))
            return;

        button.Style = (Style)TactileResources.Value["RelayTactileReset"];
        button.Tag = "ARVREL_TACTILE";
        ApplyResetForeground(button.Content as DependencyObject);
    }

    private static bool ContainsText(DependencyObject? node, string expected)
    {
        if (node is null)
            return false;
        if (node is TextBlock textBlock && string.Equals(textBlock.Text, expected, StringComparison.Ordinal))
            return true;

        foreach (var child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is DependencyObject dependencyObject && ContainsText(dependencyObject, expected))
                return true;
        }
        return false;
    }

    private static void ApplyResetForeground(DependencyObject? node)
    {
        if (node is null)
            return;

        switch (node)
        {
            case TextBlock textBlock:
                textBlock.Foreground = ResetForeground;
                textBlock.FontWeight = FontWeights.SemiBold;
                break;
            case LucideIcon icon:
                icon.Foreground = ResetForeground;
                icon.Width = 16;
                icon.Height = 16;
                break;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is DependencyObject dependencyObject)
                ApplyResetForeground(dependencyObject);
        }
    }

    private static ResourceDictionary CreateResources()
    {
        const string xaml = """
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style x:Key="RelayTactileKey" TargetType="Button">
        <Setter Property="Width" Value="48" />
        <Setter Property="Height" Value="43" />
        <Setter Property="Margin" Value="4" />
        <Setter Property="Padding" Value="0" />
        <Setter Property="Foreground" Value="#F0F5F7" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FocusVisualStyle" Value="{x:Null}" />
        <Setter Property="HorizontalContentAlignment" Value="Center" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Grid SnapsToDevicePixels="True">
                        <Border x:Name="Recess"
                                Margin="0,3,0,0"
                                Background="#131B21"
                                BorderBrush="#788993"
                                BorderThickness="1"
                                CornerRadius="7" />
                        <Border x:Name="Depth"
                                Margin="2,6,2,0"
                                Background="#0C1216"
                                CornerRadius="6" />
                        <Border x:Name="Face"
                                Margin="1,0,1,5"
                                BorderBrush="#677883"
                                BorderThickness="1"
                                CornerRadius="6">
                            <Border.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                                    <GradientStop Color="#4E5D67" Offset="0" />
                                    <GradientStop Color="#2D3941" Offset="0.38" />
                                    <GradientStop Color="#1D272E" Offset="1" />
                                </LinearGradientBrush>
                            </Border.Background>
                            <Grid>
                                <Border x:Name="TopHighlight"
                                        Height="1"
                                        Margin="6,2,6,0"
                                        VerticalAlignment="Top"
                                        Background="#9AFFFFFF"
                                        CornerRadius="1" />
                                <Border x:Name="HoverOverlay"
                                        Background="Transparent"
                                        CornerRadius="5" />
                                <ContentPresenter x:Name="KeyContent"
                                                  Margin="0"
                                                  HorizontalAlignment="Center"
                                                  VerticalAlignment="Center"
                                                  RecognizesAccessKey="True" />
                            </Grid>
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Face" Property="BorderBrush" Value="#A9BBC5" />
                            <Setter TargetName="HoverOverlay" Property="Background" Value="#12FFFFFF" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="Face" Property="Margin" Value="1,5,1,0" />
                            <Setter TargetName="Depth" Property="Opacity" Value="0.18" />
                            <Setter TargetName="Recess" Property="Background" Value="#0D1418" />
                            <Setter TargetName="TopHighlight" Property="Opacity" Value="0.18" />
                            <Setter TargetName="HoverOverlay" Property="Background" Value="#22000000" />
                            <Setter TargetName="KeyContent" Property="Margin" Value="0,2,0,0" />
                        </Trigger>
                        <Trigger Property="IsKeyboardFocused" Value="True">
                            <Setter TargetName="Recess" Property="BorderBrush" Value="#77B7DE" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Opacity" Value="0.42" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key="RelayTactileReset" TargetType="Button">
        <Setter Property="Width" Value="132" />
        <Setter Property="Height" Value="47" />
        <Setter Property="Margin" Value="12,0,0,0" />
        <Setter Property="Padding" Value="14,0" />
        <Setter Property="Foreground" Value="#EFF4F7" />
        <Setter Property="FontSize" Value="11.2" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FocusVisualStyle" Value="{x:Null}" />
        <Setter Property="HorizontalContentAlignment" Value="Center" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Grid SnapsToDevicePixels="True">
                        <Border x:Name="Bezel"
                                Margin="0,3,0,0"
                                Background="#75848E"
                                BorderBrush="#52616B"
                                BorderThickness="1"
                                CornerRadius="7" />
                        <Border x:Name="Depth"
                                Margin="3,7,3,0"
                                Background="#11191E"
                                CornerRadius="6" />
                        <Border x:Name="Face"
                                Margin="2,0,2,6"
                                BorderBrush="#748792"
                                BorderThickness="1"
                                CornerRadius="6">
                            <Border.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                                    <GradientStop Color="#4B5B65" Offset="0" />
                                    <GradientStop Color="#2B3740" Offset="0.42" />
                                    <GradientStop Color="#1D272E" Offset="1" />
                                </LinearGradientBrush>
                            </Border.Background>
                            <Grid>
                                <Border x:Name="TopHighlight"
                                        Height="1"
                                        Margin="8,2,8,0"
                                        VerticalAlignment="Top"
                                        Background="#9AFFFFFF"
                                        CornerRadius="1" />
                                <Border x:Name="AccentInset"
                                        Width="3"
                                        Margin="5,8,0,8"
                                        HorizontalAlignment="Left"
                                        Background="#5FA4CE"
                                        CornerRadius="2" />
                                <Border x:Name="HoverOverlay"
                                        Background="Transparent"
                                        CornerRadius="5" />
                                <ContentPresenter x:Name="ResetContent"
                                                  Margin="{TemplateBinding Padding}"
                                                  HorizontalAlignment="Center"
                                                  VerticalAlignment="Center"
                                                  RecognizesAccessKey="True" />
                            </Grid>
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Face" Property="BorderBrush" Value="#B0C2CC" />
                            <Setter TargetName="HoverOverlay" Property="Background" Value="#12FFFFFF" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="Face" Property="Margin" Value="2,6,2,0" />
                            <Setter TargetName="Depth" Property="Opacity" Value="0.16" />
                            <Setter TargetName="Bezel" Property="Background" Value="#64727B" />
                            <Setter TargetName="TopHighlight" Property="Opacity" Value="0.15" />
                            <Setter TargetName="HoverOverlay" Property="Background" Value="#26000000" />
                            <Setter TargetName="ResetContent" Property="Margin" Value="14,2,14,0" />
                        </Trigger>
                        <Trigger Property="IsKeyboardFocused" Value="True">
                            <Setter TargetName="Bezel" Property="BorderBrush" Value="#77B7DE" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Opacity" Value="0.42" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
""";

        return (ResourceDictionary)XamlReader.Parse(xaml);
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
