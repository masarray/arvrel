using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.App.Controls;

namespace Arvrel.App;

public sealed class AdvancedInjectionWindow : Window
{
    private static readonly SolidColorBrush RunningBrush = CreateBrush("#469A58");
    private static readonly SolidColorBrush StartingBrush = CreateBrush("#C48B2B");
    private static readonly SolidColorBrush StoppedBrush = CreateBrush("#657586");
    private static readonly SolidColorBrush StopCommandBrush = CreateBrush("#C44946");

    private readonly ContentControl _directHost;
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
        Width = 1180;
        Height = 760;
        MinWidth = 960;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        // The launcher now lives inside the Main INJECT workspace and is hidden
        // while this window owns the editor. A taskbar entry keeps the modeless
        // laboratory accessible if it is minimized or covered by another app.
        ShowInTaskbar = true;
        Background = CreateBrush("#F3F6F9");
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
            Foreground = CreateBrush("#172033")
        });
        titleBlock.Children.Add(new TextBlock
        {
            Text = "Virtual secondary-injection workspace",
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 11,
            Foreground = CreateBrush("#64748B")
        });
        header.Children.Add(titleBlock);

        var authorityBadge = new Border
        {
            Padding = new Thickness(10, 6, 10, 6),
            CornerRadius = new CornerRadius(5),
            Background = CreateBrush("#E8F1F8"),
            BorderBrush = CreateBrush("#B9D2E5"),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "EDITOR ACTIVE",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#2E6F9E")
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
            BorderBrush = CreateBrush("#D6E0E8"),
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
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _profileText = new TextBlock
        {
            Text = "No configured profile",
            FontSize = 10.5,
            Foreground = CreateBrush("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        footer.Children.Add(_profileText);

        var outputCommands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(outputCommands, 1);
        footer.Children.Add(outputCommands);

        _startInjectionButton = CreateOutputCommandButton(
            LucideIconKind.Play,
            RunningBrush,
            "Start injection",
            "Start virtual injection output");
        _startInjectionButton.Margin = new Thickness(0, 0, 6, 0);
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
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = StoppedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_statusText, 3);
        footer.Children.Add(_statusText);

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

        _statusText.Text = outputState switch
        {
            "running" => "RUNNING",
            "starting" => "STARTING",
            _ => "STOPPED"
        };
        _statusText.Foreground = outputState switch
        {
            "running" => RunningBrush,
            "starting" => StartingBrush,
            _ => StoppedBrush
        };
        _lastOutputState = outputState;
    }

    private void UpdateCommandAvailability(string outputState)
    {
        var outputActive = outputState is "running" or "starting";
        if (_startInjectionButton.IsEnabled == outputActive)
            _startInjectionButton.IsEnabled = !outputActive;
        if (_stopInjectionButton.IsEnabled != outputActive)
            _stopInjectionButton.IsEnabled = outputActive;

        _startInjectionButton.ToolTip = outputActive
            ? "Virtual injection output is already energized."
            : "Start virtual injection output using the validated values currently shown in the editor.";
        _stopInjectionButton.ToolTip = outputActive
            ? "Stop virtual injection output at 0 V / 0 A while retaining the configured values."
            : "Virtual injection output is already stopped.";
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
            Width = 34,
            Height = 32,
            MinWidth = 34,
            MinHeight = 32,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new LucideIcon
            {
                Kind = iconKind,
                Width = 15,
                Height = 15,
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
            Padding = new Thickness(12, 6, 12, 6)
        };

    private static FrameworkElement Placeholder(string text)
        => new Border
        {
            Margin = new Thickness(18),
            Padding = new Thickness(24),
            Background = CreateBrush("#F8FAFC"),
            BorderBrush = CreateBrush("#D9E3EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontSize = 13,
                Foreground = CreateBrush("#64748B"),
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
