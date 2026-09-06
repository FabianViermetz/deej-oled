using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DeejFeedback;

internal static class BrandIcon
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create()
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(16, 19, 26));
        using var accent = new SolidBrush(Color.FromArgb(99, 215, 198));
        using var darkPen = new Pen(Color.FromArgb(16, 19, 26), 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var darkBrush = new SolidBrush(Color.FromArgb(16, 19, 26));
        graphics.FillRoundedRectangle(accent, new Rectangle(4, 4, 56, 56), 13);
        var xs = new[] { 17, 27, 37, 47 };
        var ys = new[] { 22, 40, 28, 44 };
        for (var i = 0; i < xs.Length; i++)
        {
            graphics.DrawLine(darkPen, xs[i], 14, xs[i], 50);
            graphics.FillEllipse(darkBrush, xs[i] - 5, ys[i] - 5, 10, 10);
        }
        var handle = bitmap.GetHicon();
        try { using var temporary = Icon.FromHandle(handle); return (Icon)temporary.Clone(); }
        finally { DestroyIcon(handle); }
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
