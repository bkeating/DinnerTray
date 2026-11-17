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
        using var stream = typeof(AppArtwork).Assembly.GetManifestResourceStream("DinnerTray.Logo.png")
            ?? throw new InvalidOperationException("The DinnerTray logo resource is missing.");
        using var logo = Image.FromStream(stream);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var scale = Math.Min((float)size / logo.Width, (float)size / logo.Height);
        var width = logo.Width * scale;
        var height = logo.Height * scale;
        g.DrawImage(logo, (size - width) / 2, (size - height) / 2, width, height);
        return bitmap;
    }
}
