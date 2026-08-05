using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.App.Controls;

namespace Arvrel.App;

public sealed class AdvancedInjectionWindow : Window
{
    private static readonly SolidColorBrush RunningBrush = CreateBrush("#469A58");
    private static readonly SolidColorBrush RunningBackgroundBrush = CreateBrush("#EAF5EC");
    private static readonly SolidColorBrush RunningBorderBrush = CreateBrush("#B9D8BF");
    private static readonly SolidColorBrush StartingBrush = CreateBrush("#C48B2B");
    private static readonly SolidColorBrush StartingBackgroundBrush = CreateBrush("#FBF2E3");
    private static readonly SolidColorBrush StartingBorderBrush = CreateBrush("#E2C58F");
    private static readonly SolidColorBrush StoppedBrush = CreateBrush("#657586");
    private static readonly SolidColorBrush StoppedBackgroundBrush = CreateBrush("#F2F5F7");
    private static readonly SolidColorBrush StoppedBorderBrush = CreateBrush("#CBD3DA");
    private static readonly SolidColorBrush StopCommandBrush = CreateBrush("#C44946");
    private static readonly SolidColorBrush SurfaceBrush = CreateBrush("#FFFFFF");
    private static readonly SolidColorBrush LineBrush = CreateBrush("#D6E0E8");
    private static readonly SolidColorBrush TextBrush = CreateBrush("#172033");
    private static readonly SolidColorBrush MutedBrush = CreateBrush("#64748B");

    private readonly ContentControl _directHost;
    private readonly Border _statusBadge;
    private readonly TextBlock _statusText;
    private readonly TextBlock _profileText;
    private readonly Button _startInjectionButton;
    private readonly Button _stopInjectionButton;
    private readonly TabControl _tabs;
    private string? _lastProfileName;
    private string? _lastFingerprint;
    private string? _lastOutputState;

    public AdvancedInjectionWindow()
    {
        Title = "ARVREL — Advanced Injection Laboratory";
        Width = 1040;
        Height = 680;
        MinWidth = 880;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        Background = CreateBrush("#F3F6F9");
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        var root = new Grid
        {
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Content = root;

        var headerShell = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 8, 12, 8),
            SnapsToDevicePixels = true
        };
        root.Children.Add(headerShell);

        var header = new Grid
        {
            MinHeight = 42,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerShell.Child = header;

        var titleBlock = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        titleBlock.Children.Add(new TextBlock
        {
            Text = "ADVANCED INJECTION LABORATORY",
            FontSize = 15.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush
        });
        titleBlock.Children.Add(new TextBlock
        {
            Text = "Virtual secondary-injection workspace",
            Margin = new Thickness(0, 1, 0, 0),
            FontSize = 10,
            Foreground = MutedBrush
        });
        header.Children.Add(titleBlock);

        var profileBlock = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 132,
            MaxWidth = 210
        };
        profileBlock.Children.Add(new TextBlock
        {
            Text = "PROFILE",
            FontSize = 8.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = MutedBrush
        });
        _profileText = new TextBlock
        {
            Text = "No configured profile",
            Margin = new Thickness(0, 1, 0, 0),
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        profileBlock.Children.Add(_profileText);
        Grid.SetColumn(profileBlock, 1);
        header.Children.Add(profileBlock);

        var outputCommands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(outputCommands, 3);
        header.Children.Add(outputCommands);

        _startInjectionButton = CreateOutputCommandButton(
            LucideIconKind.Play,
            RunningBrush,
            "Start injection",
            "Start virtual injection output");
        _startInjectionButton.Margin = new Thickness(0, 0, 5, 0);
        _startInjectionButton.Click += (_, _) => StartInjectionRequested?.Invoke(this, EventArgs.Empty);
        outputCommands.Children.Add(_startInjectionButton);

        _stopInjectionButton = CreateOutputCommandButton(
            LucideIconKind.CircleStop,
            StopCommandBrush,
            "Stop injection",
            "Stop virtual injection output");
        _stopInjectionButton.Click += (_, _) => StopInjectionRequested?.Invoke(this, EventArgs.Empty);
        outputCommands.Children.Add(_stopInjectionButton);

        _statusText = new TextBlock
        {
            Text = "STOPPED",
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = StoppedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusBadge = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 72,
            CornerRadius = new CornerRadius(4),
            Background = StoppedBackgroundBrush,
            BorderBrush = StoppedBorderBrush,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _statusText
        };
        Grid.SetColumn(_statusBadge, 5);
        header.Children.Add(_statusBadge);

        var authorityBadge = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(4),
            Background = CreateBrush("#E8F1F8"),
            BorderBrush = CreateBrush("#B9D2E5"),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "This modeless window currently owns the shared Direct injection editor.",
            Child = new TextBlock
            {
                Text = "EDITOR",
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#2E6F9E")
            }
        };
        Grid.SetColumn(authorityBadge, 7);
        header.Children.Add(authorityBadge);

        _directHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(6)
        };

        _tabs = new TabControl
        {
            Margin = new Thickness(10, 8, 10, 10),
            Background = SurfaceBrush,
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
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

        UpdateCommandAvailability("stopped");
    }

    public event EventHandler? StartInjectionRequested;
    public event EventHandler? StopInjectionRequested;

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
        if (!string.Equals(_lastProfileName, profileName, StringComparison.Ordinal))
        {
            _profileText.Text = profileName;
            _lastProfileName = profileName;
        }

        if (!string.Equals(_lastFingerprint, fingerprint, StringComparison.Ordinal))
        {
            _profileText.ToolTip = $"Injection fingerprint {fingerprint}";
            _lastFingerprint = fingerprint;
        }

        UpdateCommandAvailability(outputState);
        if (string.Equals(_lastOutputState, outputState, StringComparison.Ordinal))
            return;

        switch (outputState)
        {
            case "running":
                SetStatusPresentation("RUNNING", RunningBrush, RunningBackgroundBrush, RunningBorderBrush);
                break;
            case "starting":
                SetStatusPresentation("STARTING", StartingBrush, StartingBackgroundBrush, StartingBorderBrush);
                break;
            default:
                SetStatusPresentation("STOPPED", StoppedBrush, StoppedBackgroundBrush, StoppedBorderBrush);
                break;
        }
        _lastOutputState = outputState;
    }

    private void SetStatusPresentation(string text, Brush foreground, Brush background, Brush border)
    {
        if (!string.Equals(_statusText.Text, text, StringComparison.Ordinal))
            _statusText.Text = text;
        if (!ReferenceEquals(_statusText.Foreground, foreground))
            _statusText.Foreground = foreground;
        if (!ReferenceEquals(_statusBadge.Background, background))
            _statusBadge.Background = background;
        if (!ReferenceEquals(_statusBadge.BorderBrush, border))
            _statusBadge.BorderBrush = border;
    }

    private void UpdateCommandAvailability(string outputState)
    {
        var outputActive = outputState is "running" or "starting";
        if (_startInjectionButton.IsEnabled == outputActive)
            _startInjectionButton.IsEnabled = !outputActive;
        if (_stopInjectionButton.IsEnabled != outputActive)
            _stopInjectionButton.IsEnabled = outputActive;

        SetToolTipIfChanged(
            _startInjectionButton,
            outputActive
                ? "Virtual injection output is already energized."
                : "Start virtual injection output using the validated values currently shown in the editor.");
        SetToolTipIfChanged(
            _stopInjectionButton,
            outputActive
                ? "Stop virtual injection output at 0 V / 0 A while retaining the configured values."
                : "Virtual injection output is already stopped.");
    }

    private static void SetToolTipIfChanged(FrameworkElement element, string value)
    {
        if (!Equals(element.ToolTip, value))
            element.ToolTip = value;
    }

    private static Button CreateOutputCommandButton(
        LucideIconKind iconKind,
        Brush iconBrush,
        string automationName,
        string helpText)
    {
        var button = new Button
        {
            Style = Application.Current?.TryFindResource("IconOnlyButton") as Style,
            Width = 32,
            Height = 30,
            MinWidth = 32,
            MinHeight = 30,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new LucideIcon
            {
                Kind = iconKind,
                Width = 14,
                Height = 14,
                Foreground = iconBrush,
                Filled = iconKind == LucideIconKind.CircleStop
            }
        };
        AutomationProperties.SetName(button, automationName);
        AutomationProperties.SetHelpText(button, helpText);
        ToolTipService.SetShowOnDisabled(button, true);
        return button;
    }

    private static TabItem CreateTab(string header, object content, bool enabled)
        => new()
        {
            Header = header,
            Content = content,
            IsEnabled = enabled,
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 10.5,
            MinHeight = 28
        };

    private static FrameworkElement Placeholder(string text)
        => new Border
        {
            Margin = new Thickness(10),
            Padding = new Thickness(16),
            Background = CreateBrush("#F8FAFC"),
            BorderBrush = CreateBrush("#D9E3EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = new TextBlock
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontSize = 12,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap
            }
        };

    private static SolidColorBrush CreateBrush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }
}
