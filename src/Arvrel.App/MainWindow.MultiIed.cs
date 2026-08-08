using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.App.Controls.Avr;
using Arvrel.Application.Ied;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _multiIedInstalled;
    private bool _multiIedLoadHooked;
    private ComboBox? _iedTypeCombo;
    private FrameworkElement? _protectionWorkspace;
    private FrameworkElement? _protectionToolbar;
    private Border? _topHealthBadge;
    private AvrWorkspaceControl? _avrWorkspace;
    private Border? _avrToolbar;
    private TextBlock? _avrRunButtonText;
    private TextBlock? _labSubtitleText;
    private string? _relayEngineModeText;
    private string? _relayWindowTitle;
    private string? _relaySubtitleText;

    internal void InitializeMultiIedWorkspace()
    {
        if (_multiIedInstalled)
            return;

        if (!IsLoaded)
        {
            if (!_multiIedLoadHooked)
            {
                _multiIedLoadHooked = true;
                Loaded += MultiIedWindow_Loaded;
            }

            return;
        }

        InstallMultiIedWorkspace();
    }

    private void MultiIedWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MultiIedWindow_Loaded;
        _multiIedLoadHooked = false;
        InstallMultiIedWorkspace();
    }

    private void InstallMultiIedWorkspace()
    {
        if (_multiIedInstalled)
            return;

        if (Content is not Grid root)
        {
            StatusText.Text = "Multi-IED selector could not resolve the ARVREL root workspace.";
            return;
        }

        _protectionToolbar = ResolveRootWorkspaceChild(root, SourceCombo);
        _protectionWorkspace = ResolveRootWorkspaceChild(root, SmvScope);
        if (_protectionToolbar is null || _protectionWorkspace is null)
        {
            StatusText.Text = "Multi-IED selector could not resolve the protection workspace.";
            return;
        }

        _topHealthBadge = MultiIedVisualAncestors<Border>(TopHealthLed).FirstOrDefault();
        _relayEngineModeText = EngineModeText.Text;
        _relayWindowTitle = Title;
        _labSubtitleText = MultiIedVisualDescendants<TextBlock>(root)
            .FirstOrDefault(text => string.Equals(
                text.Text,
                "IEC 61850 Process Bus Protection Laboratory",
                StringComparison.Ordinal));
        _relaySubtitleText = _labSubtitleText?.Text;

        _avrWorkspace = new AvrWorkspaceControl
        {
            Visibility = Visibility.Collapsed
        };
        _avrWorkspace.RunStateChanged += AvrWorkspace_RunStateChanged;
        Grid.SetRow(_avrWorkspace, 2);
        root.Children.Add(_avrWorkspace);

        _avrToolbar = BuildAvrToolbar();
        _avrToolbar.Visibility = Visibility.Collapsed;
        Grid.SetRow(_avrToolbar, 1);
        root.Children.Add(_avrToolbar);

        InstallIedSelector();
        _multiIedInstalled = true;
        SelectIed(VirtualIedKind.ProtectionRelay);
        AddEvent("IED", "Multi-IED laboratory ready · OCR relay + AVR");
    }

    private void InstallIedSelector()
    {
        if (OperatingModeCombo.Parent is not StackPanel headerActions)
            return;

        var insertAt = headerActions.Children.IndexOf(OperatingModeCombo);
        if (insertAt < 0)
            insertAt = 0;

        var label = new TextBlock
        {
            Text = "IED",
            Foreground = BrushFrom("#8FA2AF"),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        _iedTypeCombo = new ComboBox
        {
            Width = 215,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0),
            ItemsSource = VirtualIedCatalog.All,
            DisplayMemberPath = nameof(VirtualIedDescriptor.DisplayName),
            ToolTip = "Select the virtual IED to configure and test"
        };
        TextSearch.SetTextPath(_iedTypeCombo, nameof(VirtualIedDescriptor.DisplayName));
        _iedTypeCombo.SelectionChanged += IedTypeCombo_SelectionChanged;

        headerActions.Children.Insert(insertAt, label);
        headerActions.Children.Insert(insertAt + 1, _iedTypeCombo);
        _iedTypeCombo.SelectedIndex = 0;
    }

    private Border BuildAvrToolbar()
    {
        var toolbar = new Border
        {
            Background = BrushFrom("#F3F6F8"),
            BorderBrush = BrushFrom("#CBD4DA"),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var grid = new Grid { Margin = new Thickness(13, 8, 13, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        heading.Children.Add(new TextBlock
        {
            Text = "AVR LAB",
            Foreground = BrushFrom("#667985"),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 9, 0)
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Injection form → virtual AVR response → configuration / validation",
            Foreground = BrushFrom("#31444F"),
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(heading, 0);
        grid.Children.Add(heading);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(actions, 2);

        var configureButton = CreateToolbarButton("AVR Config", "CompactButton", AvrConfigure_Click);
        var runButton = CreateToolbarButton(string.Empty, "PrimaryButton", AvrRun_Click);
        _avrRunButtonText = new TextBlock { Text = "Start Injection", VerticalAlignment = VerticalAlignment.Center };
        runButton.Content = _avrRunButtonText;
        var resetButton = CreateToolbarButton("Reset", "CompactButton", AvrReset_Click);
        resetButton.Margin = new Thickness(0);

        actions.Children.Add(configureButton);
        actions.Children.Add(runButton);
        actions.Children.Add(resetButton);
        grid.Children.Add(actions);

        toolbar.Child = grid;
        return toolbar;
    }

    private Button CreateToolbarButton(string content, string styleKey, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(9, 4, 9, 4),
            Margin = new Thickness(0, 0, 6, 0),
            MinHeight = 28
        };
        if (TryFindResource(styleKey) is Style style)
            button.Style = style;
        button.Click += handler;
        return button;
    }

    private async void IedTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_iedTypeCombo?.SelectedItem is not VirtualIedDescriptor descriptor || !_multiIedInstalled)
            return;

        if (descriptor.Kind == VirtualIedKind.AutomaticVoltageRegulator)
        {
            _internalRunning = false;
            UpdateRunButton();

            if (_sourceRunning)
            {
                try
                {
                    await _processBus.StopAsync().ConfigureAwait(true);
                    _sourceRunning = false;
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
                {
                    AddEvent("IED WARN", $"Process-bus stop while changing IED: {ex.Message}");
                }
            }
        }
        else if (_avrWorkspace?.IsRunning == true)
        {
            _avrWorkspace.ToggleRun();
        }

        SelectIed(descriptor.Kind);
    }

    private void SelectIed(VirtualIedKind kind)
    {
        if (_protectionToolbar is null || _protectionWorkspace is null || _avrToolbar is null || _avrWorkspace is null)
            return;

        var protectionRelay = kind == VirtualIedKind.ProtectionRelay;
        _protectionToolbar.Visibility = protectionRelay ? Visibility.Visible : Visibility.Collapsed;
        _protectionWorkspace.Visibility = protectionRelay ? Visibility.Visible : Visibility.Collapsed;
        _avrToolbar.Visibility = protectionRelay ? Visibility.Collapsed : Visibility.Visible;
        _avrWorkspace.Visibility = protectionRelay ? Visibility.Collapsed : Visibility.Visible;
        OperatingModeCombo.Visibility = protectionRelay ? Visibility.Visible : Visibility.Collapsed;

        if (_topHealthBadge is not null)
            _topHealthBadge.Visibility = protectionRelay ? Visibility.Visible : Visibility.Collapsed;

        if (protectionRelay)
        {
            Title = _relayWindowTitle ?? "ARVREL — Virtual Protection Relay Lab";
            if (_labSubtitleText is not null)
                _labSubtitleText.Text = _relaySubtitleText ?? "IEC 61850 Process Bus Protection Laboratory";
            EngineModeText.Text = _relayEngineModeText ?? "P6 RELAY";
            StatusText.Text = "Protection Relay · OCR selected. Configure 50/51 and run the existing relay laboratory.";
        }
        else
        {
            if (!EngineModeText.Text.Contains("AVR", StringComparison.Ordinal))
                _relayEngineModeText = EngineModeText.Text;
            Title = "ARVREL — AVR / OLTC Controller Lab";
            if (_labSubtitleText is not null)
                _labSubtitleText.Text = "Automatic Voltage Regulator · OLTC commissioning laboratory";
            EngineModeText.Text = "IED · AVR · U/I BENCH";
            StatusText.Text = "AVR selected. Configure U/I/φ/f targets, energize the test source, observe controller/tap response, then validate settings and blocking.";
        }

        AddEvent("IED", protectionRelay ? "Protection Relay · OCR selected" : "AVR · OLTC Controller selected");
        UpdateAvrRunButton();
    }

    private void AvrConfigure_Click(object sender, RoutedEventArgs e)
        => _avrWorkspace?.FocusConfiguration();

    private void AvrRun_Click(object sender, RoutedEventArgs e)
    {
        _avrWorkspace?.ToggleRun();
        UpdateAvrRunButton();
    }

    private void AvrReset_Click(object sender, RoutedEventArgs e)
    {
        _avrWorkspace?.ResetSimulator();
        UpdateAvrRunButton();
        StatusText.Text = "AVR reset. Test source is OFF at 0 V / 0 A; configured source targets remain ready for the next injection.";
    }

    private void AvrWorkspace_RunStateChanged(object? sender, EventArgs e)
    {
        UpdateAvrRunButton();
        if (_avrWorkspace is not null)
            StatusText.Text = _avrWorkspace.IsRunning
                ? "AVR injection sequence/ramp active. Observe T1/T2, output contact, motor travel, blocking and tap feedback."
                : "AVR sequence/ramp paused. The faceplate shows whether the test source is held energized or OFF.";
    }

    private void UpdateAvrRunButton()
    {
        if (_avrRunButtonText is null || _avrWorkspace is null)
            return;

        _avrRunButtonText.Text = _avrWorkspace.IsRunning ? "Pause Injection" : "Start / Resume";
    }

    private static FrameworkElement? ResolveRootWorkspaceChild(Grid root, DependencyObject descendant)
    {
        DependencyObject current = descendant;
        while (VisualTreeHelper.GetParent(current) is { } parent)
        {
            if (ReferenceEquals(parent, root))
                return current as FrameworkElement;
            current = parent;
        }

        return null;
    }

    private static IEnumerable<T> MultiIedVisualAncestors<T>(DependencyObject child)
        where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(child);
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
                yield return match;
        }
    }

    private static IEnumerable<T> MultiIedVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (var descendant in MultiIedVisualDescendants<T>(child))
                yield return descendant;
        }
    }
}