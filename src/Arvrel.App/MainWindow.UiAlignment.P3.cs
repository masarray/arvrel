using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _p3UiAlignmentApplied;

    internal void ScheduleP3UiAlignment()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(ApplyP3UiAlignment));
    }

    private void ApplyP3UiAlignment()
    {
        if (_p3UiAlignmentApplied)
            return;

        _p3UiAlignmentApplied = true;
        AlignApplicationHeader();
        AlignSourceToolbar();
        AlignWaveformFooterControls();
    }

    private void AlignApplicationHeader()
    {
        NormalizeComboBox(OperatingModeCombo, minimumHeight: 32);
        OperatingModeCombo.Width = 142;
        OperatingModeCombo.Height = 32;
        OperatingModeCombo.MinHeight = 32;
        OperatingModeCombo.MaxHeight = 32;
        OperatingModeCombo.Margin = new Thickness(0, 0, 9, 0);

        if (OperatingModeCombo.Parent is StackPanel headerActions)
        {
            headerActions.VerticalAlignment = VerticalAlignment.Center;
            foreach (var child in headerActions.Children.OfType<FrameworkElement>())
                child.VerticalAlignment = VerticalAlignment.Center;
        }

        TopHealthText.VerticalAlignment = VerticalAlignment.Center;
        TopHealthText.LineHeight = 13;
        TopHealthLed.VerticalAlignment = VerticalAlignment.Center;

        if (Ancestor<Border>(TopHealthText) is { } healthBadge)
        {
            healthBadge.Height = 32;
            healthBadge.Padding = new Thickness(9, 0, 9, 0);
            healthBadge.VerticalAlignment = VerticalAlignment.Center;
            healthBadge.Margin = new Thickness(0, 0, 9, 0);

            if (healthBadge.Child is StackPanel healthLine)
            {
                healthLine.VerticalAlignment = VerticalAlignment.Center;
                foreach (var child in healthLine.Children.OfType<FrameworkElement>())
                    child.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        EngineModeText.VerticalAlignment = VerticalAlignment.Center;
        EngineModeText.TextTrimming = TextTrimming.CharacterEllipsis;
    }

    private void AlignSourceToolbar()
    {
        if (SourceCombo.Parent is not Grid toolbarGrid)
            return;

        toolbarGrid.Margin = new Thickness(13, 10, 13, 10);
        toolbarGrid.VerticalAlignment = VerticalAlignment.Stretch;

        var selectors = new[] { SourceCombo, AdapterCombo, StreamCombo, ViewCombo };
        foreach (var selector in selectors)
        {
            NormalizeComboBox(selector, minimumHeight: 34);
            selector.Height = 34;
            selector.MinHeight = 34;
            selector.MaxHeight = 34;
            selector.VerticalAlignment = VerticalAlignment.Center;
            selector.Padding = new Thickness(10, 0, 8, 0);
        }

        foreach (var label in toolbarGrid.Children.OfType<TextBlock>())
        {
            var column = Grid.GetColumn(label);
            if (column is not (0 or 3 or 6 or 9))
                continue;

            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 8, 0);
            label.LineHeight = 14;
            label.TextAlignment = TextAlignment.Left;
        }

        if (RunButton.Parent is not StackPanel actionPanel)
            return;

        actionPanel.VerticalAlignment = VerticalAlignment.Center;
        actionPanel.HorizontalAlignment = HorizontalAlignment.Right;

        var iconOnlyStyle = TryFindResource("IconOnlyButton") as Style;
        foreach (var button in actionPanel.Children.OfType<Button>())
        {
            NormalizeButton(button);
            button.Height = 34;
            button.MinHeight = 34;
            button.MaxHeight = 34;
            button.VerticalAlignment = VerticalAlignment.Center;

            if (ReferenceEquals(button.Style, iconOnlyStyle))
            {
                button.Width = 36;
                button.MinWidth = 36;
                button.MaxWidth = 36;
                button.Padding = new Thickness(0);
            }
        }

        RunButton.MinWidth = 90;
        RunButton.Padding = new Thickness(12, 0, 12, 0);
        RunButton.Margin = new Thickness(1, 0, 0, 0);
    }

    private void AlignWaveformFooterControls()
    {
        var buttons = new[] { InjectFaultButton, DegradeSmvButton };
        foreach (var button in buttons)
        {
            NormalizeButton(button);
            button.MinHeight = 30;
            button.VerticalAlignment = VerticalAlignment.Center;
            button.Padding = new Thickness(9, 0, 9, 0);
        }

        if (InjectFaultButton.Parent is StackPanel footerActions)
        {
            footerActions.VerticalAlignment = VerticalAlignment.Center;
            foreach (var child in footerActions.Children.OfType<FrameworkElement>())
                child.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    internal static void NormalizeComboBox(ComboBox combo, double minimumHeight = 30)
    {
        combo.MinHeight = Math.Max(combo.MinHeight, minimumHeight);
        combo.VerticalAlignment = VerticalAlignment.Center;
        combo.VerticalContentAlignment = VerticalAlignment.Center;
        combo.HorizontalContentAlignment = HorizontalAlignment.Left;
        combo.SnapsToDevicePixels = true;
        combo.UseLayoutRounding = true;

        var horizontalLeft = Math.Max(8, combo.Padding.Left);
        var horizontalRight = Math.Max(7, combo.Padding.Right);
        combo.Padding = new Thickness(horizontalLeft, 0, horizontalRight, 0);
    }

    internal static void NormalizeTextBox(TextBox textBox)
    {
        if (textBox.AcceptsReturn)
            return;

        textBox.MinHeight = Math.Max(30, textBox.MinHeight);
        textBox.VerticalAlignment = VerticalAlignment.Center;
        textBox.VerticalContentAlignment = VerticalAlignment.Center;
        textBox.SnapsToDevicePixels = true;
        textBox.UseLayoutRounding = true;

        var horizontalLeft = Math.Max(8, textBox.Padding.Left);
        var horizontalRight = Math.Max(8, textBox.Padding.Right);
        textBox.Padding = new Thickness(horizontalLeft, 0, horizontalRight, 0);
    }

    internal static void NormalizeButton(Button button)
    {
        button.VerticalAlignment = VerticalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.SnapsToDevicePixels = true;
        button.UseLayoutRounding = true;

        AlignButtonContent(button.Content);
    }

    private static void AlignButtonContent(object? content)
    {
        switch (content)
        {
            case StackPanel panel:
                panel.VerticalAlignment = VerticalAlignment.Center;
                foreach (var child in panel.Children.OfType<FrameworkElement>())
                    child.VerticalAlignment = VerticalAlignment.Center;
                break;

            case FrameworkElement element:
                element.VerticalAlignment = VerticalAlignment.Center;
                break;
        }
    }

    private static T? Ancestor<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is T typed)
                return typed;
        }

        return null;
    }
}

internal static class P3UiAlignmentBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoaded));

        EventManager.RegisterClassHandler(
            typeof(ComboBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnComboBoxLoaded));

        EventManager.RegisterClassHandler(
            typeof(TextBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTextBoxLoaded));

        EventManager.RegisterClassHandler(
            typeof(Button),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnButtonLoaded));
    }

    private static void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.ScheduleP3UiAlignment();
    }

    private static void OnComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox combo && IsArvrelWindow(combo))
            MainWindow.NormalizeComboBox(combo);
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && IsArvrelWindow(textBox))
            MainWindow.NormalizeTextBox(textBox);
    }

    private static void OnButtonLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && IsArvrelWindow(button))
            MainWindow.NormalizeButton(button);
    }

    private static bool IsArvrelWindow(DependencyObject control)
    {
        var window = Window.GetWindow(control);
        return window?.GetType().Assembly == typeof(MainWindow).Assembly;
    }
}
