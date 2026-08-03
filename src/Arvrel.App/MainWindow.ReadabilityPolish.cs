using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _readabilityPolishApplied;

    internal void ScheduleReadabilityPolish()
        => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ApplyReadabilityPolish));

    private void ApplyReadabilityPolish()
    {
        if (_readabilityPolishApplied)
            return;

        _readabilityPolishApplied = true;

        var settingTexts = new[]
        {
            Phase50SettingText,
            Phase51SettingText,
            Earth50SettingText,
            Earth51SettingText
        };
        foreach (var text in settingTexts)
        {
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            BindReadabilityTooltip(text);
        }

        EventTraceText.FontSize = 9.2;
        EventTraceText.LineHeight = 13;
        EventTraceText.TextWrapping = TextWrapping.NoWrap;
        EventTraceText.TextTrimming = TextTrimming.CharacterEllipsis;
        BindReadabilityTooltip(EventTraceText);

        if (FindReadabilityAncestor<Border>(EventTraceText) is { Parent: Grid operationGrid } &&
            operationGrid.ColumnDefinitions.Count >= 5)
        {
            for (var index = 0; index < 4; index++)
                operationGrid.ColumnDefinitions[index].Width = new GridLength(0.92, GridUnitType.Star);
            operationGrid.ColumnDefinitions[4].Width = new GridLength(1.65, GridUnitType.Star);
        }

        foreach (var text in new[]
                 {
                     StreamHealthText,
                     WaveformSubtitleText,
                     ProtectionReasonText,
                     RelayFooterText,
                     StatusText,
                     SampleCounterText,
                     FpsText,
                     ActiveSettingsStatusText,
                     SclStatusText
                 })
        {
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            BindReadabilityTooltip(text);
        }
    }

    private static void BindReadabilityTooltip(TextBlock text)
        => BindingOperations.SetBinding(
            text,
            FrameworkElement.ToolTipProperty,
            new Binding(nameof(TextBlock.Text))
            {
                Source = text,
                Mode = BindingMode.OneWay
            });

    private static T? FindReadabilityAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            if (current is T typed)
                return typed;
        }

        return null;
    }
}

internal static class ReadabilityPolishBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoaded));
    }

    private static void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.ScheduleReadabilityPolish();
    }
}
