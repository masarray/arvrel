using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _layoutPolishInitialized;

    internal void InitializeLayoutPolish()
    {
        if (_layoutPolishInitialized)
            return;
        if (!_phasorWorkspaceInitialized ||
            _phasorQuantityCombo?.Parent is not StackPanel controlsLine ||
            StreamHealthText.Parent is not StackPanel titleLine ||
            titleLine.Parent is not StackPanel titleBlock ||
            titleBlock.Parent is not Grid headerGrid)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeLayoutPolish));
            return;
        }

        _layoutPolishInitialized = true;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        if (_analysisHost is not null)
        {
            _analysisHost.UseLayoutRounding = true;
            _analysisHost.SnapsToDevicePixels = true;
        }

        if (headerGrid.ColumnDefinitions.Count >= 3)
        {
            headerGrid.ColumnDefinitions[0].Width = new GridLength(210);
            headerGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            headerGrid.ColumnDefinitions[1].MinWidth = 318;
            headerGrid.ColumnDefinitions[2].Width = GridLength.Auto;
        }

        titleBlock.Width = 210;
        var subtitle = titleBlock.Children.OfType<TextBlock>().FirstOrDefault();
        if (subtitle is not null)
            subtitle.Width = 210;
        StreamHealthText.MaxWidth = 84;

        controlsLine.Margin = new Thickness(4, 0, 4, 0);
        controlsLine.HorizontalAlignment = HorizontalAlignment.Center;
        controlsLine.VerticalAlignment = VerticalAlignment.Center;

        if (_analysisModeButtons.TryGetValue(AnalysisWorkspaceMode.Injection, out var injection))
        {
            injection.Content = "INJECT";
            injection.MinWidth = 62;
        }
        if (_analysisModeButtons.TryGetValue(AnalysisWorkspaceMode.Waveform, out var wave))
            wave.MinWidth = 44;
        if (_analysisModeButtons.TryGetValue(AnalysisWorkspaceMode.Dual, out var dual))
            dual.MinWidth = 44;
        if (_analysisModeButtons.TryGetValue(AnalysisWorkspaceMode.Phasor, out var phasor))
            phasor.MinWidth = 58;

        _phasorQuantityCombo.Width = 96;
        _phasorQuantityCombo.Margin = new Thickness(6, 0, 0, 0);

        var measurementSummary = headerGrid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(child => !ReferenceEquals(child, titleBlock) && !ReferenceEquals(child, controlsLine));
        if (measurementSummary is not null)
        {
            measurementSummary.Margin = new Thickness(8, 0, 0, 0);
            var groups = measurementSummary.Children.OfType<StackPanel>().ToArray();
            for (var index = 0; index < groups.Length; index++)
            {
                groups[index].MinWidth = index == groups.Length - 1 ? 58 : 44;
                groups[index].Margin = new Thickness(0, 0, index == groups.Length - 1 ? 0 : 8, 0);
            }
        }

        // Preset, clear/reset, and Advanced actions may temporarily invoke the
        // legacy generic renderer. Restore the concise injection labels in the
        // same dispatcher cycle instead of waiting for the 250 ms observer.
        if (_virtualInjectionView is not null)
            _virtualInjectionView.AddHandler(
                Button.ClickEvent,
                new RoutedEventHandler(InjectionEditorAction_Click),
                handledEventsToo: true);
        if (_virtualInjectionPresetCombo is not null)
            _virtualInjectionPresetCombo.SelectionChanged += (_, _) => QueueConciseInjectionRefresh();
    }

    private void InjectionEditorAction_Click(object sender, RoutedEventArgs e)
        => QueueConciseInjectionRefresh();

    private void QueueConciseInjectionRefresh()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(RefreshVirtualInjectionRunStopPresentation));
    }
}

internal static class LayoutPolishBootstrap
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
        if (sender is not MainWindow window)
            return;

        window.Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(window.InitializeLayoutPolish));
    }
}