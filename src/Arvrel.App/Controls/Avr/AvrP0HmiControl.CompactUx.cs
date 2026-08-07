using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrP0HmiControl
{
    internal void ApplyCompactVisualPolish()
    {
        var inter = new FontFamily("Inter, Segoe UI");
        FontFamily = inter;
        ApplyFontRecursive(this, inter);

        // A real AVR display is landscape and information-dense. Keep the trend
        // as supporting context and reserve vertical space for the hero values.
        MinHeight = 0;
        if (HomePage.RowDefinitions.Count > 4)
            HomePage.RowDefinitions[4].Height = new GridLength(84);
        HomePage.Margin = new Thickness(7, 5, 7, 5);

        VoltageHero.FontSize = 34;
        VoltageHero.Margin = new Thickness(0, -2, 0, 0);
        TapHero.FontSize = 52;
        TapHero.MinWidth = 88;
        TrendScale.FontSize = 7;
    }

    internal void GoHome()
    {
        MenuOverlay.Visibility = Visibility.Collapsed;
        SetPage(Page.Home);
    }

    private static void ApplyFontRecursive(DependencyObject root, FontFamily font)
    {
        if (root is TextBlock text)
            text.FontFamily = font;
        else if (root is Control control)
            control.FontFamily = font;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            ApplyFontRecursive(VisualTreeHelper.GetChild(root, i), font);
    }
}
