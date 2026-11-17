using System.Diagnostics;
using System.Text.Json;

namespace DinnerTray;

internal sealed record VideoState(string Title, bool Paused, double Time, string? Png, string? Error);

internal sealed class BrowserBridge : IDisposable
{
    public static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DinnerTray");
    private readonly string profile = Path.Combine(DataDirectory, "Browser");
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private CdpClient? page;
    private string? targetId;
    private int port;
    private DateTime nextDiscovery;
    private Task<(string Id, string Socket)?>? discovery;
    private DateTime nextHealthCheck;
    private string? playbackError;
    private readonly Dictionary<string, PlaybackRecovery> recovery = new();
    private static readonly JsonSerializerOptions HealthJson = new() { PropertyNameCaseInsensitive = true };
    private static readonly string HealthScript = ReadScript("PlaybackHealth.js");
    private static readonly string FrameScript = ReadScript("VideoFrame.js");
    private static string ReadScript(string name)
    {
        using var stream = typeof(BrowserBridge).Assembly.GetManifestResourceStream("DinnerTray." + name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public string? LastError { get; private set; }
    public bool HasVideo { get; private set; }
    public bool AllowTestPages { get; init; }

    public async Task OpenAsync(string url = "https://www.youtube.com/")
    {
        if (await TryDiscoverPortAsync())
        {
            using var response = await http.PutAsync($"http://127.0.0.1:{port}/json/new?{Uri.EscapeDataString(url)}", null);
            response.EnsureSuccessStatusCode();
            return;
        }
        Directory.CreateDirectory(profile);
        var chrome = new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe")
        }.FirstOrDefault(File.Exists) ?? throw new IOException("Install Google Chrome or Microsoft Edge to use DinnerTray.");
        var info = new ProcessStartInfo(chrome) { UseShellExecute = false };
        foreach (var argument in new[] { $"--user-data-dir={profile}", "--remote-debugging-port=0", "--remote-debugging-address=127.0.0.1", "--no-first-run", "--no-default-browser-check", "--disable-background-timer-throttling", "--disable-backgrounding-occluded-windows", "--disable-renderer-backgrounding", url })
            info.ArgumentList.Add(argument);
        Process.Start(info)?.Dispose();
        for (int i = 0; i < 40; i++)
        {
            if (await TryDiscoverPortAsync()) return;
            await Task.Delay(250);
        }
        throw new IOException("Chrome is taking longer than expected to start. Try Open YouTube again.");
    }

    private async Task<bool> TryDiscoverPortAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(Path.Combine(profile, "DevToolsActivePort"));
            if (lines.Length < 2 || !int.TryParse(lines[0], out int foundPort)) return false;
            var version = await http.GetStringAsync($"http://127.0.0.1:{foundPort}/json/version");
            using var json = JsonDocument.Parse(version);
            // Don't attach to a different process that happened to reuse a stale port.
            if (!json.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!.EndsWith(lines[1], StringComparison.Ordinal)) return false;
            port = foundPort;
            return true;
        }
        catch { return false; }
    }

    private bool IsVideoPage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (AllowTestPages && uri.IsFile) return true;
        return uri.Scheme is "https" or "http";
    }

    private async Task<(string Id, string Socket)?> DiscoverAsync()
    {
        if (!await TryDiscoverPortAsync()) return null;
        using var json = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/list"));
        var candidates = json.RootElement.EnumerateArray().Where(t => t.GetProperty("type").GetString() == "page" && IsVideoPage(t.GetProperty("url").GetString() ?? "")).ToList();
        string? winnerId = null;
        string? winnerSocket = null;
        int best = -1;
        foreach (var target in candidates)
        {
            var id = target.GetProperty("id").GetString()!;
            var ws = target.GetProperty("webSocketDebuggerUrl").GetString()!;
            CdpClient? probe = null;
            try
            {
                // Discovery must never queue work on the active capture connection.
                probe = new CdpClient();
                await probe.ConnectAsync(ws);
                var state = await probe.EvaluateAsync("(() => { const vs = [...document.querySelectorAll('video')].filter(v => v.readyState >= 2 && v.videoWidth > 0); return {video: vs.length > 0, playing: vs.some(v => !v.paused && !v.ended), focused: document.hasFocus()}; })()");
                // Keep the followed tab through buffering, errors, and a reload.
                if (!state.GetProperty("video").GetBoolean() && id != targetId) continue;
                int score = (state.GetProperty("playing").GetBoolean() ? 10 : 0) + (state.GetProperty("focused").GetBoolean() ? 4 : 0) + (id == targetId ? 2 : 0);
                if (score > best) { best = score; winnerId = id; winnerSocket = ws; }
            }
            catch { /* A closing/navigating tab is normal. Try the others. */ }
            finally { probe?.Dispose(); }
        }
        return winnerId == null ? null : (winnerId, winnerSocket!);
    }

    private async Task UpdateTargetAsync()
    {
        if (discovery == null && DateTime.UtcNow >= nextDiscovery) discovery = DiscoverAsync();
        // A slow or suspended unrelated tab must not freeze an already-playing tray video.
        if (discovery == null || (page != null && !discovery.IsCompleted)) return;
        (string Id, string Socket)? winner;
        try { winner = await discovery; }
        catch when (page != null) { return; }
        finally { discovery = null; nextDiscovery = DateTime.UtcNow.AddSeconds(2); }
        if (winner == null || (targetId == winner.Value.Id && page != null)) return;
        Disconnect();
        var connection = new CdpClient();
        try { await connection.ConnectAsync(winner.Value.Socket); }
        catch { connection.Dispose(); throw; }
        page = connection;
        targetId = winner.Value.Id;
        nextHealthCheck = DateTime.MinValue;
    }

    public async Task<VideoState?> CaptureAsync()
    {
        try
        {
            await UpdateTargetAsync();
            if (page == null) { HasVideo = false; return null; }
            if (DateTime.UtcNow >= nextHealthCheck)
            {
                nextHealthCheck = DateTime.UtcNow.AddSeconds(1);
                if (await CheckRecoveryAsync()) return new("Video", true, 0, null, "Recovering YouTube playback…");
            }
            var value = await page.EvaluateAsync(FrameScript);
            if (value.ValueKind == JsonValueKind.Null)
            {
                HasVideo = false;
                return playbackError == null ? null : new("Video", true, 0, null, playbackError);
            }
            HasVideo = true;
            LastError = null;
            return new(value.GetProperty("title").GetString() ?? "Video", value.GetProperty("paused").GetBoolean(), value.GetProperty("time").GetDouble(), value.TryGetProperty("png", out var png) ? png.GetString() : null, playbackError ?? (value.TryGetProperty("error", out var error) ? error.GetString() : null));
        }
        catch (Exception ex) { LastError = ex.Message; Disconnect(forgetTarget: false); return null; }
    }

    public async Task ToggleAsync()
    {
        if (targetId != null && recovery.TryGetValue(targetId, out var state)) state.CancelResume();
        if (page != null) await page.EvaluateAsync("(async () => { const v = [...document.querySelectorAll('video')].filter(v => v.readyState >= 2 && v.videoWidth > 0).sort((a,b) => Number(!b.paused && !b.ended) - Number(!a.paused && !a.ended) || b.videoWidth*b.videoHeight-a.videoWidth*a.videoHeight)[0]; if (v) { if (v.paused) await v.play(); else v.pause(); } return true; })()", userGesture: true);
    }

    private async Task<bool> CheckRecoveryAsync()
    {
        if (page == null || targetId == null) return false;
        // Recovery knows YouTube's player UI and URL identity only. Never refresh other sites.
        var isYouTube = await page.EvaluateAsync("location.hostname === 'youtube.com' || location.hostname.endsWith('.youtube.com')");
        if (!isYouTube.GetBoolean()) { playbackError = null; recovery.Remove(targetId); return false; }
        var health = (await page.EvaluateAsync(HealthScript)).Deserialize<PlaybackHealth>(HealthJson)!;
        playbackError = health.RetryError
            ? (string.IsNullOrEmpty(health.PlayerError) ? "YouTube playback error — click to open browser" : $"YouTube error: {health.PlayerError}")
            : null;
        if (!recovery.TryGetValue(targetId, out var state)) recovery[targetId] = state = new();
        var resume = state.Observe(health, DateTimeOffset.UtcNow);
        if (resume != null)
        {
            LogRecovery("refresh", health.MediaError, health.PlayerError);
            await page.CallAsync("Page.reload", new { ignoreCache = true });
            return true;
        }
        if (state.CanRestore(health))
        {
            var saved = state.Pending!;
            var data = JsonSerializer.Serialize(new { videoId = saved.VideoId, time = saved.Time, paused = saved.Paused, live = saved.Live });
            var restored = await page.EvaluateAsync("""
                (async () => {
                  const saved = SAVED;
                  const url = new URL(location.href);
                  const id = url.searchParams.get('v') || (/^\/shorts\/([^/?]+)/.exec(url.pathname)?.[1]) || '';
                  const v = document.querySelector('video');
                  if (!v || id !== saved.videoId || v.readyState < 3 || document.querySelector('#movie_player')?.classList.contains('ad-showing')) return false;
                  if (!saved.live && Number.isFinite(v.duration) && v.duration > 0)
                    v.currentTime = Math.min(saved.time, Math.max(0, v.duration - 0.25));
                  if (saved.paused) v.pause(); else await v.play();
                  return true;
                })()
                """.Replace("SAVED", data), userGesture: true);
            if (restored.GetBoolean()) { state.Restored(); LogRecovery("restored", 0); }
        }
        return false;
    }

    private static void LogRecovery(string action, int mediaError, string playerError = "")
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var path = Path.Combine(DataDirectory, "recovery.log");
            if (File.Exists(path) && new FileInfo(path).Length > 65536) File.WriteAllText(path, "");
            File.AppendAllText(path, JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, action, mediaError, playerError }) + Environment.NewLine);
        }
        catch { /* Diagnostics must never interrupt playback. */ }
    }

    public async Task ShowAsync()
    {
        if (page == null || targetId == null) { await OpenAsync(); return; }
        var version = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/version"));
        using (version)
        using (var browser = new CdpClient())
        {
            await browser.ConnectAsync(version.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!);
            var window = await browser.CallAsync("Browser.getWindowForTarget", new { targetId });
            await browser.CallAsync("Browser.setWindowBounds", new { windowId = window.GetProperty("windowId").GetInt32(), bounds = new { windowState = "normal" } });
            await browser.CallAsync("Target.activateTarget", new { targetId });
        }
        await page.CallAsync("Page.bringToFront");
    }

    private void Disconnect(bool forgetTarget = true) { page?.Dispose(); page = null; if (forgetTarget) targetId = null; HasVideo = false; playbackError = null; }
    public void Dispose() { Disconnect(); http.Dispose(); }
}
