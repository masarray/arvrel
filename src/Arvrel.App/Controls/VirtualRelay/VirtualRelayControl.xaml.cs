using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Arvrel.App.Controls.VirtualRelay;

public enum VirtualRelayHardwareKey
{
    F1,
    F2,
    F3,
    F4,
    F5,
    Home,
    Menu,
    Up,
    Down,
    Left,
    Right,
    Ok,
    Back,
    Favorite
}

public enum VirtualRelayLcdTone
{
    Normal,
    Warning,
    Trip
}

public readonly record struct VirtualRelaySoftKey(string Label, bool IsActive = false);

public sealed class VirtualRelayHardwareKeyEventArgs(VirtualRelayHardwareKey key) : EventArgs
{
    public VirtualRelayHardwareKey Key { get; } = key;
}

/// <summary>
/// Native P6 virtual relay hardware. The control owns physical geometry, tactile
/// inputs and LCD chrome. MainWindow owns protection state and HMI navigation.
/// </summary>
public partial class VirtualRelayControl : UserControl
{
    private static readonly HashSet<string> StatusLedLabels = new(StringComparer.Ordinal)
    {
        "HEALTHY",
        "PICKUP",
        "TRIP",
        "PHASE A",
        "PHASE B",
        "PHASE C",
        "EARTH",
        "SMV BLOCK"
    };

    private readonly Border?[] _softKeyBorders = new Border?[5];
    private readonly TextBlock?[] _softKeyTexts = new TextBlock?[5];
    private TextBlock? _lcdTrustText;

    public VirtualRelayControl()
    {
        InitializeComponent();
        EnableResponsiveHardwareScale();
        ImproveStatusLedReadability(this);
        MarkNativeHardwareButtons(this);
        InitializeLcdChrome();
        WireHardwareKeys(this);
    }

    public event RoutedEventHandler? ResetRequested;
    public event EventHandler<VirtualRelayHardwareKeyEventArgs>? HardwareKeyPressed;

    private void ResetButton_Click(object sender, RoutedEventArgs e)
        => ResetRequested?.Invoke(this, e);

    public void SetSoftKeys(IReadOnlyList<VirtualRelaySoftKey> keys)
    {
        for (var index = 0; index < _softKeyTexts.Length; index++)
        {
            if (_softKeyTexts[index] is not { } text || _softKeyBorders[index] is not { } border)
                continue;

            var presentation = index < keys.Count
                ? keys[index]
                : new VirtualRelaySoftKey("—");
            text.Text = presentation.Label.ToUpperInvariant();
            text.Foreground = presentation.IsActive
                ? Brushes.White
                : Brush("#34464F");
            text.FontWeight = presentation.IsActive ? FontWeights.SemiBold : FontWeights.Medium;
            border.Background = presentation.IsActive
                ? Brush("#426F8C")
                : Brush("#CAD4D1");
        }
    }

    public void SetLcdTrustState(string text, VirtualRelayLcdTone tone)
    {
        if (_lcdTrustText is null)
            return;

        _lcdTrustText.Text = text.ToUpperInvariant();
        _lcdTrustText.Foreground = tone switch
        {
            VirtualRelayLcdTone.Trip => Brush("#FFE1DE"),
            VirtualRelayLcdTone.Warning => Brush("#FFE8B0"),
            _ => Brush("#DCE8ED")
        };
    }

    private void EnableResponsiveHardwareScale()
    {
        if (Content is not Viewbox scaler)
            return;

        scaler.Stretch = Stretch.Uniform;
        scaler.StretchDirection = StretchDirection.Both;
        scaler.HorizontalAlignment = HorizontalAlignment.Stretch;
        scaler.VerticalAlignment = VerticalAlignment.Stretch;
    }

    private void InitializeLcdChrome()
    {
        _lcdTrustText = LogicalDescendants<TextBlock>(this)
            .FirstOrDefault(text => string.Equals(text.Text, "SV READY", StringComparison.Ordinal));

        var softKeyStrip = LogicalDescendants<UniformGrid>(this)
            .FirstOrDefault(grid => grid.Children
                .OfType<Border>()
                .Select(border => border.Child as TextBlock)
                .Any(text => string.Equals(text?.Text, "MEASURE", StringComparison.Ordinal)));
        if (softKeyStrip is null)
            return;

        softKeyStrip.Rows = 1;
        softKeyStrip.Columns = 5;
        softKeyStrip.Children.Clear();

        for (var index = 0; index < 5; index++)
        {
            var text = new TextBlock
            {
                Text = "—",
                Foreground = Brush("#34464F"),
                FontFamily = new FontFamily("Segoe UI Semibold, Segoe UI"),
                FontSize = 7.7,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var border = new Border
            {
                Background = Brush("#CAD4D1"),
                BorderBrush = Brush("#899995"),
                BorderThickness = new Thickness(0, 1, index == 4 ? 0 : 1, 0),
                Padding = new Thickness(2, 0, 2, 0),
                Child = text
            };
            _softKeyBorders[index] = border;
            _softKeyTexts[index] = text;
            softKeyStrip.Children.Add(border);
        }

        SetSoftKeys(new[]
        {
            new VirtualRelaySoftKey("I RMS"),
            new VirtualRelaySoftKey("U RMS"),
            new VirtualRelaySoftKey("SEQ"),
            new VirtualRelaySoftKey("EVENTS"),
            new VirtualRelaySoftKey("SET FAV")
        });
    }

    private void WireHardwareKeys(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is Button button && TryResolveHardwareKey(button, out var key))
            {
                button.Click += (_, _) => HardwareKeyPressed?.Invoke(
                    this,
                    new VirtualRelayHardwareKeyEventArgs(key));
            }

            if (child is DependencyObject dependencyObject)
                WireHardwareKeys(dependencyObject);
        }
    }

    private static bool TryResolveHardwareKey(Button button, out VirtualRelayHardwareKey key)
    {
        if (button.Content is string content)
        {
            switch (content)
            {
                case "F1": key = VirtualRelayHardwareKey.F1; return true;
                case "F2": key = VirtualRelayHardwareKey.F2; return true;
                case "F3": key = VirtualRelayHardwareKey.F3; return true;
                case "F4": key = VirtualRelayHardwareKey.F4; return true;
                case "F5": key = VirtualRelayHardwareKey.F5; return true;
                case "RESET": key = default; return false;
            }
        }

        key = button.ToolTip?.ToString() switch
        {
            "Home page" => VirtualRelayHardwareKey.Home,
            "Main menu" => VirtualRelayHardwareKey.Menu,
            "Up" => VirtualRelayHardwareKey.Up,
            "Down" => VirtualRelayHardwareKey.Down,
            "Cancel" => VirtualRelayHardwareKey.Left,
            "Next" => VirtualRelayHardwareKey.Right,
            "Enter" => VirtualRelayHardwareKey.Ok,
            "Back" => VirtualRelayHardwareKey.Back,
            "Favorite page" => VirtualRelayHardwareKey.Favorite,
            _ => default
        };

        return button.ToolTip?.ToString() is
            "Home page" or "Main menu" or "Up" or "Down" or "Cancel" or
            "Next" or "Enter" or "Back" or "Favorite page";
    }

    private static void ImproveStatusLedReadability(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is TextBlock textBlock)
            {
                if (StatusLedLabels.Contains(textBlock.Text))
                {
                    textBlock.FontSize = Math.Max(textBlock.FontSize, 11.6);
                    textBlock.FontWeight = FontWeights.SemiBold;
                    TextOptions.SetTextFormattingMode(textBlock, TextFormattingMode.Display);
                    TextOptions.SetTextHintingMode(textBlock, TextHintingMode.Fixed);
                }
                else if (string.Equals(textBlock.Text, "STATUS", StringComparison.Ordinal))
                {
                    textBlock.FontSize = Math.Max(textBlock.FontSize, 10.8);
                    textBlock.FontWeight = FontWeights.SemiBold;
                }
            }

            if (child is DependencyObject dependencyObject)
                ImproveStatusLedReadability(dependencyObject);
        }
    }

    private static void MarkNativeHardwareButtons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is Button button)
                button.Tag = "ARVREL_TACTILE";

            if (child is DependencyObject dependencyObject)
                MarkNativeHardwareButtons(dependencyObject);
        }
    }

    private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T typed)
                yield return typed;
            if (child is DependencyObject dependencyObject)
            {
                foreach (var nested in LogicalDescendants<T>(dependencyObject))
                    yield return nested;
            }
        }
    }

    private static Brush Brush(string hex)
        => (Brush)new BrushConverter().ConvertFromString(hex)!;
}
