using System.Drawing;

namespace DinnerTray;

internal sealed class ThrowGesture
{
    private readonly Queue<(PointF Point, double Time)> samples = new();
    public void Clear() => samples.Clear();
    public void Add(PointF point, double time)
    {
        samples.Enqueue((point, time));
        while (samples.Count > 1 && time - samples.Peek().Time > .12) samples.Dequeue();
    }
    public PointF Velocity()
    {
        if (samples.Count < 2) return PointF.Empty;
        var first = samples.Peek(); var last = samples.Last();
        double dt = last.Time - first.Time;
        if (dt < .01) return PointF.Empty;
        float x = (float)((last.Point.X - first.Point.X) / dt), y = (float)((last.Point.Y - first.Point.Y) / dt);
        float speed = MathF.Sqrt(x*x + y*y);
        if (speed < 80) return PointF.Empty;
        if (speed > 2400) { x *= 2400 / speed; y *= 2400 / speed; }
        return new(x, y);
    }
}
