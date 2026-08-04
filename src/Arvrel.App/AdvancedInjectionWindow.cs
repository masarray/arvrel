using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App;

public sealed class AdvancedInjectionWindow : Window
{
    private readonly ContentControl _directHost;
    private readonly TextBlock _statusText;
    private readonly TextBlock _profileText;
    private readonly TabControl _tabs;

    public AdvancedInjectionWindow()
    {
        Title = "ARVREL — Advanced Injection Laboratory";
        Width = 1180;
        Height = 760;
        MinWidth = 960;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Brush("#F3F6F9");
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var header = new Grid
        {
            Margin = new Thickness(18, 16, 18, 12)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.Children.Add(header);

        var titleBlock = new StackPanel();
        titleBlock.Children.Add(new TextBlock
        {
            Text = "ADVANCED INJECTION LABORATORY",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#172033")
        });
        titleBlock.Children.Add(new TextBlock
        {
            Text = "Modeless virtual secondary-injection workspace · one shared source authority",
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 11,
            Foreground = Brush("#64748B")
        });
        header.Children.Add(titleBlock);

        var authorityBadge = new Border
        {
            Padding = new Thickness(10, 6, 10, 6),
            CornerRadius = new CornerRadius(5),
            Background = Brush("#E8F1F8"),
            BorderBrush = Brush("#B9D2E5"),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "EDITOR AUTHORITY ACTIVE",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#2E6F9E")
            }
        };
        Grid.SetColumn(authorityBadge, 1);
        header.Children.Add(authorityBadge);

        _directHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(12)
        };

        _tabs = new TabControl
        {
            Margin = new Thickness(18, 0, 18, 12),
            Background = Brushes.White,
            BorderBrush = Brush("#D6E0E8"),
            BorderThickness = new Thickness(1)
        };
        _tabs.Items.Add(CreateTab("DIRECT", _directHost, true));
        _tabs.Items.Add(CreateTab("SYMMETRICAL", Placeholder("Sequence-component injection is scheduled for P4.2.2."), false));
        _tabs.Items.Add(CreateTab("IMPEDANCE", Placeholder("R–X plane and loop solving are scheduled for P4.2.3."), false));
        _tabs.Items.Add(CreateTab("RAMP", Placeholder("Step and continuous ramp execution are scheduled for P4.2.4."), false));
        _tabs.Items.Add(CreateTab("SEQUENCER", Placeholder("Prefault/fault/post-fault state sequencing is scheduled for P4.2.5."), false));
        _tabs.Items.Add(CreateTab("WAVEFORM", Placeholder("Advanced transient waveform models are scheduled for P4.2.6."), false));
        _tabs.SelectedIndex = 0;
        Grid.SetRow(_tabs, 1);
        root.Children.Add(_tabs);

        var footer = new Grid
        {
            Margin = new Thickness(18, 0, 18, 16)
        };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _profileText = new TextBlock
        {
            Text = "Configured profile waiting",
            FontSize = 10.5,
            Foreground = Brush("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        footer.Children.Add(_profileText);

        _statusText = new TextBlock
        {
            Text = "STOPPED · OUTPUT ZERO",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#657586"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_statusText, 1);
        footer.Children.Add(_statusText);
    }

    public bool HasEditor => _directHost.Content is FrameworkElement;

    public void AttachEditor(FrameworkElement editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (_directHost.Content is not null)
            throw new InvalidOperationException("The Advanced Injection Window already owns an editor.");

        _directHost.Content = editor;
        editor.Visibility = Visibility.Visible;
        _tabs.SelectedIndex = 0;
    }

    public FrameworkElement? DetachEditor()
    {
        var editor = _directHost.Content as FrameworkElement;
        _directHost.Content = null;
        return editor;
    }

    public void FocusDirectEditor()
    {
        _tabs.SelectedIndex = 0;
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    public void UpdateRuntimeStatus(string profileName, string outputState, string fingerprint)
    {
        _profileText.Text = $"Configured: {profileName} · {fingerprint}";
        _statusText.Text = outputState switch
        {
            "running" => "RUNNING · OUTPUT ACTIVE",
            "starting" => "STARTING · REBUILDING",
            _ => "STOPPED · OUTPUT ZERO"
        };
        _statusText.Foreground = outputState switch
        {
            "running" => Brush("#469A58"),
            "starting" => Brush("#C48B2B"),
            _ => Brush("#657586")
        };
    }

    private static TabItem CreateTab(string header, object content, bool enabled)
        => new()
        {
            Header = header,
            Content = content,
            IsEnabled = enabled,
            Padding = new Thickness(12, 6, 12, 6)
        };

    private static FrameworkElement Placeholder(string text)
        => new Border
        {
            Margin = new Thickness(18),
            Padding = new Thickness(24),
            Background = Brush("#F8FAFC"),
            BorderBrush = Brush("#D9E3EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontSize = 13,
                Foreground = Brush("#64748B"),
                TextWrapping = TextWrapping.Wrap
            }
        };

    private static SolidColorBrush Brush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }
}
