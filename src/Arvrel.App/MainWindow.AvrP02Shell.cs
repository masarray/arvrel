using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _avrToolbarVisibilityHooked;
    private bool _avrShellLoadedRetryHooked;

    internal void ApplyAvrP02ShellPolish()
    {
        var inter = new FontFamily("Inter");
        FontFamily = inter;
        ApplyInterRecursive(this, inter);

        if (_avrToolbar is null)
        {
            if (!_avrShellLoadedRetryHooked)
            {
                _avrShellLoadedRetryHooked = true;
                Loaded += (_, _) => ApplyAvrP02ShellPolish();
            }
            return;
        }

        _avrToolbar.Background = new SolidColorBrush(Color.FromRgb(234, 239, 242));
        _avrToolbar.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 212, 218));

        if (_avrToolbar.Child is Grid grid)
        {
            grid.Margin = new Thickness(10, 2, 10, 2);
            grid.MinHeight = 24;
        }

        foreach (var text in MultiIedVisualDescendants<TextBlock>(_avrToolbar))
        {
            text.FontFamily = inter;
            if (string.Equals(text.Text, "AVR LAB", StringComparison.Ordinal))
            {
                text.Text = "AVR";
                text.FontSize = 8.8;
                text.Margin = new Thickness(0, 0, 7, 0);
            }
            else if (text.Text?.StartsWith("Injection form", StringComparison.Ordinal) == true ||
                     text.Text?.StartsWith("OLTC commissioning", StringComparison.Ordinal) == true)
            {
                text.Text = "OLTC commissioning · manual U/I · SAS";
                text.FontSize = 9.0;
                text.Foreground = new SolidColorBrush(Color.FromRgb(71, 88, 98));
            }
        }

        foreach (var button in MultiIedVisualDescendants<Button>(_avrToolbar))
        {
            button.FontFamily = inter;
            button.MinHeight = 23;
            button.Padding = new Thickness(8, 1, 8, 1);
            button.Margin = new Thickness(0, 0, 5, 0);
        }

        if (_iedTypeCombo is not null)
        {
            _iedTypeCombo.FontFamily = inter;
            _iedTypeCombo.Height = 27;
        }

        if (_avrToolbar.Parent is Grid root && root.RowDefinitions.Count > 1)
        {
            void ApplyToolbarHeight()
                => root.RowDefinitions[1].Height = new GridLength(_avrToolbar.Visibility == Visibility.Visible ? 34 : 54);

            ApplyToolbarHeight();
            if (!_avrToolbarVisibilityHooked)
            {
                _avrToolbarVisibilityHooked = true;
                _avrToolbar.IsVisibleChanged += (_, _) => ApplyToolbarHeight();
            }
        }
    }

    private static void ApplyInterRecursive(DependencyObject root, FontFamily inter)
    {
        if (root is TextBlock text)
            text.FontFamily = inter;
        else if (root is Control control)
            control.FontFamily = inter;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            ApplyInterRecursive(VisualTreeHelper.GetChild(root, i), inter);
    }
}
