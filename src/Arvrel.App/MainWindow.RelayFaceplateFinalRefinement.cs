using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private const int MaximumRelayFinalButtonAttempts = 7;

    private static readonly ControlTemplate RelayFinalKeyTemplate =
        ParseTemplate(RelayFinalKeyTemplateXaml);
    private static readonly ControlTemplate RelayFinalResetTemplate =
        ParseTemplate(RelayFinalResetTemplateXaml);

    private bool _relayFinalButtonsApplied;
    private int _relayFinalButtonAttempts;

    internal void InitializeRelayFaceplateFinalRefinement()
    {
        if (_relayFinalButtonsApplied ||
            _relayFinalButtonAttempts >= MaximumRelayFinalButtonAttempts)
            return;

        // The relay has several historic presentation bootstraps. Two idle hops
        // place this bounded button authority after the hardware and premium
        // templates without creating another fascia or LED presentation owner.
        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(DeferRelayFinalButtonTuning));
    }

    private void DeferRelayFinalButtonTuning()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(ApplyRelayFinalButtonTuning));
    }

    private void ApplyRelayFinalButtonTuning()
    {
        if (_relayFinalButtonsApplied ||
            _relayFinalButtonAttempts >= MaximumRelayFinalButtonAttempts)
            return;

        _relayFinalButtonAttempts++;
        if (!_relayHardwarePresentationInitialized)
        {
            QueueRelayFinalButtonRetry();
            return;
        }

        var relayBody = VisualAncestors<Border>(HealthyLed)
            .FirstOrDefault(border => border.CornerRadius.TopLeft >= 8);
        if (relayBody is null)
        {
            QueueRelayFinalButtonRetry();
            return;
        }

        var buttonCount = ApplyFinalRelayButtons(relayBody);
        if (buttonCount == 0)
        {
            QueueRelayFinalButtonRetry();
            return;
        }

        _relayFinalButtonsApplied = true;
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

    private void QueueRelayFinalButtonRetry()
    {
        if (_relayFinalButtonAttempts >= MaximumRelayFinalButtonAttempts)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(ApplyRelayFinalButtonTuning));
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
