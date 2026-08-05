using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private const int MaximumRelayPremiumButtonAttempts = 5;

    private static readonly ControlTemplate RelayPremiumKeyTemplate =
        ParseTemplate(RelayPremiumKeyTemplateXaml);
    private static readonly ControlTemplate RelayPremiumResetTemplate =
        ParseTemplate(RelayPremiumResetTemplateXaml);

    private bool _relayPremiumButtonsApplied;
    private int _relayPremiumButtonAttempts;

    internal void InitializeRelayPremiumButtonTuning()
    {
        if (_relayPremiumButtonsApplied ||
            _relayPremiumButtonAttempts >= MaximumRelayPremiumButtonAttempts)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(ApplyRelayPremiumButtonTuning));
    }

    private void ApplyRelayPremiumButtonTuning()
    {
        if (_relayPremiumButtonsApplied ||
            _relayPremiumButtonAttempts >= MaximumRelayPremiumButtonAttempts)
            return;

        _relayPremiumButtonAttempts++;
        if (!_relayHardwarePresentationInitialized)
        {
            QueueRelayPremiumButtonRetry();
            return;
        }

        var relayBody = VisualAncestors<Border>(HealthyLed)
            .FirstOrDefault(border => border.CornerRadius.TopLeft >= 8);
        if (relayBody is null)
        {
            QueueRelayPremiumButtonRetry();
            return;
        }

        var applied = 0;
        foreach (var button in VisualDescendants<Button>(relayBody))
        {
            if (ContainsHardwareText(button.Content as DependencyObject, "RESET TRIP"))
            {
                button.Template = RelayPremiumResetTemplate;
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

            button.Template = RelayPremiumKeyTemplate;
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Effect = null;
            button.CacheMode = null;
            button.FocusVisualStyle = null;
            button.UseLayoutRounding = true;
            button.SnapsToDevicePixels = true;
            applied++;
        }

        if (applied == 0)
        {
            QueueRelayPremiumButtonRetry();
            return;
        }

        _relayPremiumButtonsApplied = true;
    }

    private void QueueRelayPremiumButtonRetry()
    {
        if (_relayPremiumButtonAttempts >= MaximumRelayPremiumButtonAttempts)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(ApplyRelayPremiumButtonTuning));
    }

    private const string RelayPremiumKeyTemplateXaml = """
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 TargetType="{x:Type Button}">
    <Grid x:Name="Root" RenderTransformOrigin="0.5,0.5">
        <Grid.RenderTransform>
            <TranslateTransform Y="0" />
        </Grid.RenderTransform>

        <Border x:Name="Base"
                Margin="1,3,1,0"
                CornerRadius="4"
                Background="#172128"
                BorderBrush="#0B1115"
                BorderThickness="1" />

        <Border x:Name="Face"
                Margin="0,0,0,3"
                CornerRadius="4"
                BorderThickness="1">
            <Border.BorderBrush>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#93A2AB" Offset="0" />
                    <GradientStop Color="#52616A" Offset="0.38" />
                    <GradientStop Color="#162027" Offset="1" />
                </LinearGradientBrush>
            </Border.BorderBrush>
            <Border.Background>
                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                    <GradientStop Color="#4B5B64" Offset="0" />
                    <GradientStop Color="#39474F" Offset="0.18" />
                    <GradientStop Color="#2D3A41" Offset="0.62" />
                    <GradientStop Color="#222D33" Offset="1" />
                </LinearGradientBrush>
            </Border.Background>
            <Border.Effect>
                <DropShadowEffect Color="#11191E"
                                  BlurRadius="4"
                                  Direction="270"
                                  ShadowDepth="1"
                                  Opacity="0.38"
                                  RenderingBias="Performance" />
            </Border.Effect>
            <Grid ClipToBounds="True">
                <Border x:Name="Gloss"
                        Height="8"
                        Margin="2,1,2,0"
                        CornerRadius="3,3,1,1"
                        VerticalAlignment="Top">
                    <Border.Background>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                            <GradientStop Color="#48FFFFFF" Offset="0" />
                            <GradientStop Color="#16FFFFFF" Offset="0.55" />
                            <GradientStop Color="#00FFFFFF" Offset="1" />
                        </LinearGradientBrush>
                    </Border.Background>
                </Border>
                <Border x:Name="LowerLip"
                        Height="2"
                        Margin="2,0,2,1"
                        CornerRadius="0,0,3,3"
                        VerticalAlignment="Bottom"
                        Background="#6810181D" />
                <Border x:Name="Hover"
                        Background="#00FFFFFF"
                        CornerRadius="3" />
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
            <Setter TargetName="Hover" Property="Background" Value="#0EFFFFFF" />
            <Setter TargetName="Gloss" Property="Opacity" Value="0.92" />
        </Trigger>
        <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Root" Property="RenderTransform">
                <Setter.Value><TranslateTransform Y="1" /></Setter.Value>
            </Setter>
            <Setter TargetName="Face" Property="Margin" Value="0,1,0,2" />
            <Setter TargetName="Base" Property="Opacity" Value="0.78" />
            <Setter TargetName="Gloss" Property="Opacity" Value="0.48" />
            <Setter TargetName="LowerLip" Property="Height" Value="1" />
            <Setter TargetName="Hover" Property="Background" Value="#10000000" />
        </Trigger>
        <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter TargetName="Face" Property="BorderBrush" Value="#6EA9CF" />
        </Trigger>
        <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Root" Property="Opacity" Value="0.46" />
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
""";

    private const string RelayPremiumResetTemplateXaml = """
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 TargetType="{x:Type Button}">
    <Grid x:Name="Root" RenderTransformOrigin="0.5,0.5">
        <Grid.RenderTransform>
            <TranslateTransform Y="0" />
        </Grid.RenderTransform>

        <Border x:Name="Base"
                Margin="1,3,1,0"
                CornerRadius="4"
                Background="#86939B"
                BorderBrush="#61717B"
                BorderThickness="1" />

        <Border x:Name="Face"
                Margin="0,0,0,3"
                CornerRadius="4"
                BorderThickness="1">
            <Border.BorderBrush>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#FFFFFF" Offset="0" />
                    <GradientStop Color="#BCC9D0" Offset="0.52" />
                    <GradientStop Color="#75858F" Offset="1" />
                </LinearGradientBrush>
            </Border.BorderBrush>
            <Border.Background>
                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                    <GradientStop Color="#FFFFFF" Offset="0" />
                    <GradientStop Color="#F5F7F8" Offset="0.24" />
                    <GradientStop Color="#E2E8EB" Offset="0.70" />
                    <GradientStop Color="#CBD5DA" Offset="1" />
                </LinearGradientBrush>
            </Border.Background>
            <Border.Effect>
                <DropShadowEffect Color="#26343D"
                                  BlurRadius="4"
                                  Direction="270"
                                  ShadowDepth="1"
                                  Opacity="0.30"
                                  RenderingBias="Performance" />
            </Border.Effect>
            <Grid ClipToBounds="True">
                <Border x:Name="Gloss"
                        Height="7"
                        Margin="2,1,2,0"
                        CornerRadius="3,3,1,1"
                        VerticalAlignment="Top">
                    <Border.Background>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                            <GradientStop Color="#A6FFFFFF" Offset="0" />
                            <GradientStop Color="#32FFFFFF" Offset="0.58" />
                            <GradientStop Color="#00FFFFFF" Offset="1" />
                        </LinearGradientBrush>
                    </Border.Background>
                </Border>
                <Border x:Name="LowerLip"
                        Height="2"
                        Margin="2,0,2,1"
                        CornerRadius="0,0,3,3"
                        VerticalAlignment="Bottom"
                        Background="#5363727B" />
                <Border x:Name="Hover"
                        Background="#00FFFFFF"
                        CornerRadius="3" />
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
            <Setter TargetName="Hover" Property="Background" Value="#102E6F9E" />
        </Trigger>
        <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Root" Property="RenderTransform">
                <Setter.Value><TranslateTransform Y="1" /></Setter.Value>
            </Setter>
            <Setter TargetName="Face" Property="Margin" Value="0,1,0,2" />
            <Setter TargetName="Base" Property="Opacity" Value="0.80" />
            <Setter TargetName="Gloss" Property="Opacity" Value="0.56" />
            <Setter TargetName="LowerLip" Property="Height" Value="1" />
            <Setter TargetName="Hover" Property="Background" Value="#0D40596A" />
        </Trigger>
        <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter TargetName="Face" Property="BorderBrush" Value="#2F6C91" />
        </Trigger>
        <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Root" Property="Opacity" Value="0.48" />
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
""";
}

internal static class RelayPremiumButtonTuningBootstrap
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
            window.InitializeRelayPremiumButtonTuning();
    }
}
