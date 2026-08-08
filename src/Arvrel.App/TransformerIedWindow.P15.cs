using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.Protection;

namespace Arvrel.App;

/// <summary>
/// P15 public-test presentation layer. The UI never reimplements transformer protection;
/// it invokes TransformerPublicSelfTest from Arvrel.Protection and renders/copies the
/// resulting deterministic report.
/// </summary>
public partial class TransformerIedWindow
{
    private bool _p15Initialized;
    private TransformerPublicSelfTestReport? _p15LastReport;
    private TextBlock? _p15SummaryText;
    private TextBlock? _p15BoundaryText;
    private Button? _p15CopyButton;
    private Button? _p15DetailsButton;

    internal void InitializeP15PublicTestUi()
    {
        if (_p15Initialized)
            return;

        if (ApplyRuntimeButton.Parent is not Panel configurationPanel)
            throw new InvalidOperationException("Transformer practitioner configuration host is unavailable.");

        var applyIndex = configurationPanel.Children.IndexOf(ApplyRuntimeButton);
        if (applyIndex < 0)
            throw new InvalidOperationException("Transformer runtime apply control is unavailable.");

        configurationPanel.Children.Insert(applyIndex, BuildP15SelfTestSection());
        _p15Initialized = true;
    }

    private UIElement BuildP15SelfTestSection()
    {
        var root = new StackPanel { Margin = new Thickness(0, 11, 0, 10) };
        root.Children.Add(new Border
        {
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 0, 0, 10)
        });

        var heading = new Grid { Margin = new Thickness(0, 0, 0, 5) };
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.Children.Add(new TextBlock
        {
            Text = "Public test / deterministic self-test",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var badge = new Border
        {
            Background = NeutralSoftBrush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = "NO SV REQUIRED",
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = NeutralBrush
            }
        };
        Grid.SetColumn(badge, 1);
        heading.Children.Add(badge);
        root.Children.Add(heading);

        _p15BoundaryText = new TextBlock
        {
            Text = "Runs 10 fixed scenarios through the authoritative transformer protection core. This verifies packaged software behavior without MU hardware, Npcap or PCAP. It does not validate packet capture, calibration, IEC conformance or protection-grade timing.",
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 9.4,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        root.Children.Add(_p15BoundaryText);

        var buttons = new WrapPanel { Margin = new Thickness(0, 0, 0, 7) };
        var runButton = new Button
        {
            Content = "RUN 10-SCENARIO SELF-TEST",
            Padding = new Thickness(9, 5, 9, 5),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Run deterministic 87T, harmonic, P13 CT-security and REF verification using the packaged protection core."
        };
        runButton.Click += RunP15SelfTest_Click;
        buttons.Children.Add(runButton);

        _p15DetailsButton = new Button
        {
            Content = "VIEW RESULT",
            Padding = new Thickness(8, 5, 8, 5),
            FontSize = 9.5,
            Margin = new Thickness(0, 0, 6, 0),
            IsEnabled = false
        };
        _p15DetailsButton.Click += ViewP15SelfTest_Click;
        buttons.Children.Add(_p15DetailsButton);

        _p15CopyButton = new Button
        {
            Content = "COPY EVIDENCE",
            Padding = new Thickness(8, 5, 8, 5),
            FontSize = 9.5,
            IsEnabled = false
        };
        _p15CopyButton.Click += CopyP15SelfTest_Click;
        buttons.Children.Add(_p15CopyButton);
        root.Children.Add(buttons);

        _p15SummaryText = new TextBlock
        {
            Text = "SELF-TEST NOT RUN · run this first before Live/Replay evaluation.",
            Foreground = NeutralBrush,
            FontSize = 9.7,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(_p15SummaryText);
        return root;
    }

    private void RunP15SelfTest_Click(object sender, RoutedEventArgs e)
    {
        var report = TransformerPublicSelfTest.RunAll();
        _p15LastReport = report;

        if (_p15SummaryText is not null)
        {
            _p15SummaryText.Text = report.AllPassed
                ? $"PASS · {report.PassedCount}/{report.Cases.Count} · {report.SuiteId}"
                : $"FAIL · {report.PassedCount}/{report.Cases.Count} · {report.FailedCount} scenario(s) require review";
            _p15SummaryText.Foreground = report.AllPassed ? HealthyBrush : TripBrush;
        }

        if (_p15CopyButton is not null)
            _p15CopyButton.IsEnabled = true;
        if (_p15DetailsButton is not null)
            _p15DetailsButton.IsEnabled = true;

        var firstFailure = report.Cases.FirstOrDefault(test => !test.Passed);
        StatusText.Text = report.AllPassed
            ? "Transformer public self-test PASS. The packaged protection core passed all 10 deterministic scenarios; Live/Replay testing may follow."
            : $"Transformer public self-test FAIL · {firstFailure?.Id ?? "unknown"}. Copy the self-test evidence when reporting the defect.";
    }

    private void ViewP15SelfTest_Click(object sender, RoutedEventArgs e)
    {
        if (_p15LastReport is null)
            return;

        var resultWindow = new Window
        {
            Owner = this,
            Title = "Transformer public self-test evidence",
            Width = 780,
            Height = 560,
            MinWidth = 620,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = BuildP15ResultViewer(_p15LastReport)
        };
        resultWindow.ShowDialog();
    }

    private UIElement BuildP15ResultViewer(TransformerPublicSelfTestReport report)
    {
        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new TextBlock
        {
            Text = report.AllPassed
                ? $"PASS · {report.PassedCount}/{report.Cases.Count} deterministic scenarios"
                : $"FAIL · {report.FailedCount} of {report.Cases.Count} deterministic scenarios",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = report.AllPassed ? HealthyBrush : TripBrush,
            Margin = new Thickness(0, 0, 0, 8)
        };
        root.Children.Add(heading);

        var evidence = new TextBox
        {
            Text = report.ToPlainText(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10.5,
            Padding = new Thickness(8)
        };
        Grid.SetRow(evidence, 1);
        root.Children.Add(evidence);

        var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        footer.Children.Add(new TextBlock
        {
            Text = "Attach this evidence to a reproducible GitHub issue. Never attach customer PCAP/SCL unless redistribution is authorized.",
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 9.2,
            VerticalAlignment = VerticalAlignment.Center
        });
        var copy = new Button
        {
            Content = "COPY EVIDENCE",
            Padding = new Thickness(9, 4, 9, 4),
            Margin = new Thickness(8, 0, 0, 0)
        };
        copy.Click += (_, _) => Clipboard.SetText(report.ToPlainText());
        DockPanel.SetDock(copy, Dock.Right);
        footer.Children.Add(copy);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private void CopyP15SelfTest_Click(object sender, RoutedEventArgs e)
    {
        if (_p15LastReport is null)
            return;

        Clipboard.SetText(_p15LastReport.ToPlainText());
        StatusText.Text = "Transformer self-test evidence copied. Include it with the application version and Windows version in any public test report.";
    }
}