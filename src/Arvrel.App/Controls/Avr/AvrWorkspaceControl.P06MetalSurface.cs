using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private static readonly ImageBrush P06OuterMetalBrush = CreateP06BrushedAluminium(
        baseR: 174,
        baseG: 176,
        baseB: 174,
        specularStrength: 7,
        grainStrength: 5,
        seed: 0x41A7);

    private static readonly ImageBrush P06InnerMetalBrush = CreateP06BrushedAluminium(
        baseR: 198,
        baseG: 201,
        baseB: 200,
        specularStrength: 9,
        grainStrength: 4,
        seed: 0x51B9);

    private int _p06MetalRetries;

    private void ApplyP06MetalSurface()
    {
        if (_faceplateFitHost?.Child is not Border faceplate)
        {
            if (_p06MetalRetries++ < 3)
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ApplyP06MetalSurface));
            return;
        }

        _p06MetalRetries = 0;
        faceplate.Background = P06OuterMetalBrush;
        faceplate.BorderBrush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(92, 98, 101), 0.00),
                new(Color.FromRgb(224, 228, 229), 0.16),
                new(Color.FromRgb(135, 141, 143), 0.50),
                new(Color.FromRgb(232, 235, 235), 0.84),
                new(Color.FromRgb(82, 89, 92), 1.00)
            },
            new Point(0, 0),
            new Point(1, 0));

        if (faceplate.Child is Border innerFrontPanel)
        {
            innerFrontPanel.Background = P06InnerMetalBrush;
            innerFrontPanel.BorderBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromRgb(119, 127, 130), 0.00),
                    new(Color.FromRgb(244, 246, 246), 0.18),
                    new(Color.FromRgb(166, 172, 174), 0.50),
                    new(Color.FromRgb(248, 249, 249), 0.82),
                    new(Color.FromRgb(111, 119, 122), 1.00)
                },
                new Point(0, 0),
                new Point(1, 0));
        }
    }

    private static ImageBrush CreateP06BrushedAluminium(
        int baseR,
        int baseG,
        int baseB,
        int specularStrength,
        int grainStrength,
        int seed)
    {
        // Fine anisotropic aluminium grain. Unlike the previous material, there are no
        // periodic row bands or warm beige stripes; those repeated bands read as wood.
        // Each scanline gets only a small stochastic offset and the horizontal direction
        // carries low-amplitude correlated micro-scratches plus a broad metallic highlight.
        const int width = 256;
        const int height = 96;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        static int Clamp(int value) => Math.Clamp(value, 0, 255);
        static int Noise(int x, int y, int localSeed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + localSeed * 69069);
                n = (n ^ (n >> 13)) * 1274126177u;
                n ^= n >> 16;
                return (int)(n % 17) - 8;
            }
        }

        for (var y = 0; y < height; y++)
        {
            // Very restrained scanline variation. Rare hairline scratches create the
            // brushed-metal cue without turning into visible horizontal bands.
            var row = Noise(5, y, seed) / 3;
            if (Noise(11, y, seed + 17) > 6)
                row += Noise(19, y, seed + 29) > 0 ? 3 : -3;

            var correlated = 0.0;
            for (var x = 0; x < width; x++)
            {
                correlated = correlated * 0.94 + Noise(x, y, seed) * 0.06;

                // Broad specular reflection gives the sheet-metal character. It varies
                // slowly across X instead of repeating as hard stripes across Y.
                var normalized = x / (double)(width - 1);
                var broadHighlight = Math.Sin(normalized * Math.PI);
                var secondary = 0.35 * Math.Sin((normalized * Math.PI * 2.0) + 0.8);
                var specular = (int)Math.Round(specularStrength * (0.72 * broadHighlight + secondary));

                var micro = (int)Math.Round(correlated * grainStrength / 5.0);
                var delta = row + specular + micro;

                // Neutral/cool aluminium with only a tiny warm bias in R. Keeping RGB
                // close together is intentional: strong R/G separation looked beige/wooden.
                var r = Clamp(baseR + delta + 1);
                var g = Clamp(baseG + delta);
                var b = Clamp(baseB + delta);

                var index = y * stride + x * 4;
                pixels[index + 0] = (byte)b;
                pixels[index + 1] = (byte)g;
                pixels[index + 2] = (byte)r;
                pixels[index + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, width, height),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, width, height),
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };
        brush.Freeze();
        return brush;
    }
}
