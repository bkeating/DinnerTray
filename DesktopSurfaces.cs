using System.Runtime.InteropServices;
using System.Text;

namespace DinnerTray;

internal static class DesktopSurfaces
{
    private delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    public static List<Perch> Read(HashSet<IntPtr> pets)
    {
        var result = new List<Perch>();
        var covers = new List<Rectangle>();
        EnumWindows((hwnd, _) =>
        {
            if (pets.Contains(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            var name = new StringBuilder(128); GetClassName(hwnd, name, 128);
            if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            if (DwmGetWindowAttribute(hwnd, 9, out Rect r, Marshal.SizeOf<Rect>()) != 0 && !GetWindowRect(hwnd, out r)) return true;
            if (r.Right - r.Left < 120 || r.Bottom - r.Top < 60) return true;
            // EnumWindows is front-to-back. Only exposed pieces of a lower window are platforms.
            var segments = new List<(int Left, int Right)> { (r.Left + 10, r.Right - 10) };
            foreach (var cover in covers.Where(c => c.Top <= r.Top && c.Bottom > r.Top))
            {
                segments = segments.SelectMany(s =>
                {
                    if (cover.Right <= s.Left || cover.Left >= s.Right) return new[] { s };
                    return new[] { (s.Left, Math.Min(s.Right, cover.Left)), (Math.Max(s.Left, cover.Right), s.Right) }.Where(p => p.Item2 > p.Item1);
                }).ToList();
            }
            result.AddRange(segments.Select(s => new Perch(s.Left, s.Right, r.Top)));
            covers.Add(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
            return true;
        }, IntPtr.Zero);
        foreach (var screen in Screen.AllScreens)
            result.Add(new Perch(screen.WorkingArea.Left, screen.WorkingArea.Right, screen.WorkingArea.Bottom));
        return result;
    }
}
