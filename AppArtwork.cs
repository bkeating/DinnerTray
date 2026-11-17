using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DinnerTray;

internal static class AppArtwork
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    public static Icon ToIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    public static Bitmap Draw(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(size / 128f, size / 128f);
        using var shape = new GraphicsPath();
        shape.AddRoundedRectangle(new RectangleF(4, 4, 120, 120), new SizeF(28, 28));
        using var gradient = new LinearGradientBrush(new Rectangle(0, 0, 128, 128), Color.FromArgb(255, 146, 103), Color.FromArgb(213, 61, 68), 65f);
        g.FillPath(gradient, shape);
        using var plate = new SolidBrush(Color.FromArgb(255, 248, 236));
        g.FillEllipse(plate, 21, 21, 86, 86);
        using var rim = new Pen(Color.FromArgb(235, 213, 198), 3);
        g.DrawEllipse(rim, 29, 29, 70, 70);
        using var play = new SolidBrush(Color.FromArgb(213, 68, 66));
        g.FillPolygon(play, [new PointF(55, 44), new PointF(55, 84), new PointF(85, 64)]);
        return bitmap;
    }
}
