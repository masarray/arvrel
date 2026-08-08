using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrP0HmiControl
{
    private bool _p03LayoutApplied;

    internal void ApplyP03Layout()
    {
        if (_p03LayoutApplied)
            return;
        _p03LayoutApplied = true;

        FontFamily = new FontFamily("Inter");
        HomePage.Margin = new Thickness(6, 4, 6, 3);

        if (HomePage.RowDefinitions.Count >= 7)
        {
            HomePage.RowDefinitions[1].Height = new GridLength(4);
            HomePage.RowDefinitions[3].Height = new GridLength(4);
            HomePage.RowDefinitions[4].Height = new GridLength(62);
            HomePage.RowDefinitions[5].Height = new GridLength(4);
            HomePage.RowDefinitions[6].Height = new GridLength(43);
        }

        // Recover vertical pixels for the operational strip. The old layout used
        // them for navigation/footer chrome, causing T1/T2 and motor bars to clip.
        if (BottomStatus.Parent is Grid footer && footer.Parent is Grid root && root.RowDefinitions.Count >= 3)
        {
            root.RowDefinitions[0].Height = new GridLength(32);
            root.RowDefinitions[2].Height = new GridLength(22);
        }

        TimerBar.Height = 8;
        TimerBar.MinHeight = 8;
        TimerBar.VerticalAlignment = VerticalAlignment.Bottom;
        TimerBar.Foreground = new SolidColorBrush(Color.FromRgb(43, 125, 178));

        MotorBar.Height = 8;
        MotorBar.MinHeight = 8;
        MotorBar.VerticalAlignment = VerticalAlignment.Bottom;
        MotorBar.Foreground = new SolidColorBrush(Color.FromRgb(60, 143, 91));

        BottomStatus.FontSize = 7.5;
        FooterReason.FontSize = 7.4;
    }

    internal void ApplyP03State(AvrSnapshot snapshot, bool severeBlock)
    {
        ApplyP03Layout();

        var normal = new SolidColorBrush(Color.FromRgb(54, 91, 112));
        var amber = new SolidColorBrush(Color.FromRgb(190, 119, 20));
        var red = new SolidColorBrush(Color.FromRgb(190, 62, 51));
        var alarm = severeBlock ? red : amber;

        HomeState.Foreground = snapshot.Blocked ? alarm : normal;
        TrendState.Foreground = snapshot.Blocked ? alarm : normal;
        HomeOutput.Foreground = snapshot.Blocked ? alarm : new SolidColorBrush(Color.FromRgb(67, 89, 99));
        BottomStatus.Foreground = snapshot.Blocked ? alarm : new SolidColorBrush(Color.FromRgb(63, 82, 91));

        HomeState.FontWeight = snapshot.Blocked ? FontWeights.Bold : FontWeights.SemiBold;
        TrendState.FontWeight = snapshot.Blocked ? FontWeights.Bold : FontWeights.SemiBold;
        HomeOutput.FontWeight = snapshot.Blocked ? FontWeights.Bold : FontWeights.Normal;

        if (snapshot.Blocked)
        {
            TimerBar.Foreground = alarm;
            MotorBar.Foreground = new SolidColorBrush(Color.FromRgb(118, 128, 133));
        }
        else
        {
            TimerBar.Foreground = new SolidColorBrush(Color.FromRgb(43, 125, 178));
            MotorBar.Foreground = new SolidColorBrush(Color.FromRgb(60, 143, 91));
        }
    }
}
