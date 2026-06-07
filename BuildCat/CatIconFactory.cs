using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace BuildCat;

internal static class CatIconFactory
{
    private static readonly Color Green = Color.FromArgb(20, 176, 95);
    private static readonly Color Yellow = Color.FromArgb(235, 181, 35);
    private static readonly Color Red = Color.FromArgb(220, 65, 60);
    private static readonly Color Gray = Color.FromArgb(142, 148, 158);

    public static Icon Create(BuildState state)
    {
        var color = state switch
        {
            BuildState.Running => Yellow,
            BuildState.Success => Green,
            BuildState.Failed => Red,
            _ => Gray
        };

        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            using var path = new GraphicsPath();
            path.StartFigure();
            path.AddBezier(new PointF(4.6f, 23.6f), new PointF(1.8f, 17.0f), new PointF(4.5f, 9.7f), new PointF(9.0f, 8.2f));
            path.AddLine(new PointF(9.0f, 8.2f), new PointF(7.8f, 2.8f));
            path.AddLine(new PointF(7.8f, 2.8f), new PointF(15.1f, 7.5f));
            path.AddLine(new PointF(15.1f, 7.5f), new PointF(16.9f, 7.5f));
            path.AddLine(new PointF(16.9f, 7.5f), new PointF(24.2f, 2.8f));
            path.AddLine(new PointF(24.2f, 2.8f), new PointF(23.0f, 8.2f));
            path.AddBezier(new PointF(23.0f, 8.2f), new PointF(27.5f, 9.7f), new PointF(30.2f, 17.0f), new PointF(27.4f, 23.6f));
            path.AddBezier(new PointF(27.4f, 23.6f), new PointF(24.1f, 29.0f), new PointF(7.9f, 29.0f), new PointF(4.6f, 23.6f));
            path.CloseFigure();

            DrawGlow(g, path, color);

            using var fill = new SolidBrush(color);
            g.FillPath(fill, path);

            using var highlight = new Pen(Color.FromArgb(120, Color.White), 1.2f)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using var edge = new Pen(Color.FromArgb(150, Color.White), 1.6f)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawPath(edge, path);
            g.DrawArc(highlight, 7.0f, 10.0f, 18.0f, 14.0f, 205, 130);
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static void DrawGlow(Graphics graphics, GraphicsPath path, Color color)
    {
        using var outerGlow = new Pen(Color.FromArgb(45, color), 8.5f)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var innerGlow = new Pen(Color.FromArgb(75, color), 5.5f)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        graphics.DrawPath(outerGlow, path);
        graphics.DrawPath(innerGlow, path);
    }
}
