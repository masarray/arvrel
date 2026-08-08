using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrP0HmiControl
{
    private bool _interLoadedHooked;

    internal void ApplyCompactVisualPolish()
    {
        var inter = new FontFamily("Inter");
        FontFamily = inter;
        ApplyFontRecursive(this, inter);

        if (!_interLoadedHooked)
        {
            _interLoadedHooked = true;
            Loaded += (_, _) => ApplyFontRecursive(this, new FontFamily("Inter"));
        }

        // Landscape, information-dense AVR display. The trend is context, not
        // the hero, so the body can scale larger while keeping a calm hierarchy.
        MinHeight = 0;
        if (HomePage.RowDefinitions.Count > 4)
            HomePage.RowDefinitions[4].Height = new GridLength(78);
        HomePage.Margin = new Thickness(7, 5, 7, 4);

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
