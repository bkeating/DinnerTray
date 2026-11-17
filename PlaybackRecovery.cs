namespace DinnerTray;

internal sealed record PlaybackHealth(string VideoId, bool HasVideo, bool Paused, bool Ended,
    double Time, double Duration, int Ready, bool Live, bool Ad, bool Online, int MediaError, bool RetryError);
internal sealed record ResumePoint(string VideoId, double Time, bool Paused, bool Live);

// Only explicit errors following observed playback qualify. No stall/paused-time heuristics.
internal sealed class PlaybackRecovery
{
    private string identity = "";
    private ResumePoint? lastGood;
    private DateTimeOffset? errorSince;
    private readonly Queue<DateTimeOffset> attempts = new();
    public ResumePoint? Pending { get; private set; }
    private DateTimeOffset pendingUntil;

    public ResumePoint? Observe(PlaybackHealth h, DateTimeOffset now)
    {
        if (h.VideoId != identity)
        {
            identity = h.VideoId; lastGood = null; errorSince = null; Pending = null;
        }
        if (Pending != null && now >= pendingUntil) Pending = null;
        bool failed = h.RetryError || h.MediaError is 2 or 3;
        if (!failed)
        {
            errorSince = null;
            if (Pending == null && h.HasVideo && h.Ready >= 2 && !h.Ad && !h.Ended && h.Time > 0)
                lastGood = new(h.VideoId, h.Time, h.Paused, h.Live);
            return null;
        }
        if (!h.Online || h.Ad || h.Ended || lastGood == null || identity.Length == 0)
        { errorSince = null; return null; }
        if (Pending != null) { errorSince = null; return null; }
        errorSince ??= now;
        while (attempts.TryPeek(out var time) && now - time >= TimeSpan.FromMinutes(10)) attempts.Dequeue();
        if (now - errorSince.Value < TimeSpan.FromSeconds(5) || attempts.Count >= 2) return null;
        attempts.Enqueue(now);
        Pending = lastGood;
        pendingUntil = now.AddSeconds(45);
        errorSince = null;
        return Pending;
    }

    public bool CanRestore(PlaybackHealth h) => Pending != null && Pending.VideoId == h.VideoId &&
        h.HasVideo && h.Ready >= 1 && !h.Ad && !h.RetryError && h.MediaError == 0;
    public void Restored() => Pending = null;
    public void CancelResume() { Pending = null; lastGood = null; errorSince = null; }
}
