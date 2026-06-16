using Svg;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BuildCat;

internal static class CatIconFactory
{
    private static readonly Color Green = Color.FromArgb(20, 176, 95);
    private static readonly Color Yellow = Color.FromArgb(235, 181, 35);
    private static readonly Color Red = Color.FromArgb(220, 65, 60);
    private static readonly Color Gray = Color.FromArgb(142, 148, 158);

    private static readonly string SvgTemplate = LoadSvgTemplate();

    public static Icon Create(BuildState state)
    {
        var color = state switch
        {
            BuildState.Running => Yellow,
            BuildState.Success => Green,
            BuildState.Failed => Red,
            _ => Gray
        };

        var hex = $"#{color.R:x2}{color.G:x2}{color.B:x2}";
        var svgContent = SvgTemplate.Replace("fill:#020202", $"fill:{hex}");
        var svgDoc = SvgDocument.FromSvg<SvgDocument>(svgContent);

        using var result = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Layered glow: render progressively larger, centered, with increasing opacity
            using var outerGlow = svgDoc.Draw(42, 42);
            DrawWithOpacity(g, outerGlow, -5, -5, 0.25f);

            using var innerGlow = svgDoc.Draw(36, 36);
            DrawWithOpacity(g, innerGlow, -2, -2, 0.45f);

            using var main = svgDoc.Draw(32, 32);
            g.DrawImage(main, 0, 0, 32, 32);
        }

        var handle = result.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void DrawWithOpacity(Graphics g, Bitmap bmp, int x, int y, float opacity)
    {
        var colorMatrix = new ColorMatrix { Matrix33 = opacity };
        using var attrs = new ImageAttributes();
        attrs.SetColorMatrix(colorMatrix);
        g.DrawImage(bmp, new Rectangle(x, y, bmp.Width, bmp.Height),
            0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attrs);
    }

    private static string LoadSvgTemplate()
    {
        var assembly = typeof(CatIconFactory).Assembly;
        using var stream = assembly.GetManifestResourceStream("BuildCat.Assets.BuildCat.svg")
            ?? throw new InvalidOperationException("BuildCat.Assets.BuildCat.svg embedded resource not found");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
