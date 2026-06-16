using Svg;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BuildCat;

internal static class CatIconFactory
{
    private static readonly Dictionary<BuildState, SvgDocument> Svgs = new()
    {
        [BuildState.Success] = LoadSvg("BuildCat.Assets.BuildCat.Green.svg"),
        [BuildState.Running] = LoadSvg("BuildCat.Assets.BuildCat.Yellow.svg"),
        [BuildState.Failed]  = LoadSvg("BuildCat.Assets.BuildCat.Red.svg"),
        [BuildState.Unknown] = LoadSvg("BuildCat.Assets.BuildCat.Gray.svg"),
    };

    public static Icon Create(BuildState state)
    {
        var svgDoc = Svgs[state];

        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            using var rendered = svgDoc.Draw(32, 32);
            g.DrawImage(rendered, 0, 0, 32, 32);
        }

        var handle = bitmap.GetHicon();
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

    private static SvgDocument LoadSvg(string resourceName)
    {
        var assembly = typeof(CatIconFactory).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"{resourceName} embedded resource not found");
        using var reader = new StreamReader(stream);
        return SvgDocument.FromSvg<SvgDocument>(reader.ReadToEnd());
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
