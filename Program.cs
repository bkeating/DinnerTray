using System.Text.Json;

namespace DinnerTray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--export-icon"))
        {
            using var bitmap = AppArtwork.Draw(256);
            using var icon = AppArtwork.ToIcon(bitmap);
            using var output = File.Create(Path.Combine(AppContext.BaseDirectory, "DinnerTray.ico"));
            icon.Save(output);
            return;
        }
        using var mutex = new Mutex(true, "Local\\DinnerTray.App", out bool first);
        if (!first) return;
        Application.Run(new TrayApp(args));
    }
}

internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly Icon idleIcon;
    private Icon? frameIcon;
    private readonly BrowserBridge browser;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 83 };
    private readonly ToolStripMenuItem play = new("Play / Pause") { Enabled = false };
    private readonly ToolStripMenuItem status = new("Open a video in DinnerTray’s browser") { Enabled = false };
    private AboutForm? about;
    private readonly DesktopPets pets = new();
    private bool busy;
    private bool exiting;
    private readonly SemaphoreSlim actions = new(1);
    private readonly bool diagnostics;
    private long frameCount;
    private DateTime nextDiagnostics;
    private string? previousPng;
    private long changedFrames;

    public TrayApp(string[] args)
    {
        diagnostics = args.Contains("--diagnostics");
        browser = new BrowserBridge { AllowTestPages = args.Contains("--test-pages") };
        using var artwork = AppArtwork.Draw(64);
        idleIcon = AppArtwork.ToIcon(artwork);
        var menu = new ContextMenuStrip { ShowImageMargin = false, Font = new Font("Segoe UI", 10) };
        menu.Items.Add(status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(play);
        menu.Items.Add("Open YouTube", null, async (_, _) => await RunActionAsync(() => browser.OpenAsync()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Spawn a sheep", null, (_, _) => pets.Spawn());
        menu.Items.Add("Spawn a Herd", null, (_, _) => pets.SpawnHerd());
        menu.Items.Add("Send all sheep home", null, (_, _) => pets.Clear());
        menu.Items.Add("Slingshot game / Reset blocks", null, (_, _) => pets.StartGame());
        menu.Items.Add("Close slingshot game", null, (_, _) => pets.StopGame());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("About DinnerTray", null, (_, _) => ShowAbout());
        menu.Items.Add("Quit DinnerTray", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = idleIcon, Text = "DinnerTray — click to open YouTube", Visible = true, ContextMenuStrip = menu };
        tray.MouseClick += async (_, e) => { if (e.Button == MouseButtons.Left) await RunActionAsync(browser.ShowAsync); };
        play.Click += async (_, _) => await RunActionAsync(browser.ToggleAsync);
        timer.Tick += async (_, _) => await TickAsync();
        // Start after the Windows Forms synchronization context exists.
        var startup = new System.Windows.Forms.Timer { Interval = 150 };
        startup.Tick += async (_, _) =>
        {
            startup.Stop(); startup.Dispose();
            if (args.Contains("--about")) ShowAbout();
            int urlIndex = Array.IndexOf(args, "--url");
            string url = urlIndex >= 0 && urlIndex + 1 < args.Length ? args[urlIndex + 1] : "https://www.youtube.com/";
            if (!args.Contains("--no-open")) await RunActionAsync(() => browser.OpenAsync(url));
            timer.Start();
        };
        startup.Start();
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        await actions.WaitAsync();
        try
        {
            // A click arriving during frame capture must not be silently dropped.
            while (busy && !exiting) await Task.Delay(10);
            if (exiting) return;
            busy = true;
            try { await action(); }
            finally { busy = false; }
        }
        catch (Exception ex)
        {
            if (!exiting) tray.ShowBalloonTip(5000, "DinnerTray", ex.Message, ToolTipIcon.Info);
        }
        finally { actions.Release(); }
    }

    private async Task TickAsync()
    {
        if (busy || exiting) return;
        busy = true;
        try
        {
            var video = await browser.CaptureAsync();
            if (exiting) return;
            play.Enabled = browser.HasVideo;
            play.Text = video?.Paused == false ? "Pause" : "Play";
            status.Text = video?.Error ?? (video == null ? "Open a video in DinnerTray’s browser" : Truncate(video.Title, 52));
            string tooltip = video?.Error ?? (video == null ? "DinnerTray — click to open YouTube" : $"{video.Title} — {(video.Paused ? "paused" : "playing")}");
            tray.Text = Truncate(tooltip, 63);
            if (video?.Png is { } png)
            {
                frameCount++;
                if (png != previousPng)
                {
                    changedFrames++;
                    using var stream = new MemoryStream(Convert.FromBase64String(png));
                    using var bitmap = new Bitmap(stream);
                    var icon = AppArtwork.ToIcon(bitmap);
                    tray.Icon = icon;
                    frameIcon?.Dispose();
                    frameIcon = icon;
                    previousPng = png;
                }
            }
            else
            {
                tray.Icon = idleIcon;
                frameIcon?.Dispose(); frameIcon = null; previousPng = null;
            }
            if (diagnostics && DateTime.UtcNow >= nextDiagnostics)
            {
                Directory.CreateDirectory(BrowserBridge.DataDirectory);
                File.WriteAllText(Path.Combine(BrowserBridge.DataDirectory, "diagnostics.json"), JsonSerializer.Serialize(new { frameCount, changedFrames, hasVideo = browser.HasVideo, paused = video?.Paused, videoTime = video?.Time, error = video?.Error ?? browser.LastError, updated = DateTime.UtcNow }));
                if (video?.Png != null) File.WriteAllBytes(Path.Combine(BrowserBridge.DataDirectory, "last-frame.png"), Convert.FromBase64String(video.Png));
                nextDiagnostics = DateTime.UtcNow.AddSeconds(1);
            }
        }
        catch (Exception ex) { if (!exiting) status.Text = Truncate(ex.Message, 52); }
        finally { busy = false; }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    private void ShowAbout()
    {
        if (about == null || about.IsDisposed) about = new AboutForm();
        about.Show(); about.Activate();
    }
    protected override void ExitThreadCore()
    {
        exiting = true;
        pets.Dispose();
        timer.Stop(); timer.Dispose();
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose();
        frameIcon?.Dispose(); idleIcon.Dispose(); browser.Dispose(); about?.Dispose();
        base.ExitThreadCore();
    }
}
