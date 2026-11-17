using System.Drawing;

namespace DinnerTray;

internal readonly record struct Perch(float Left, float Right, float Top);

internal sealed class DesktopPhysics
{
    // Coordinates are the creature's feet; the renderer is independent of collision rules.
    public const float Gravity = 1200;
    public float X, Y, VelocityX, VelocityY;
    public int Direction = 1;
    public bool Grounded { get; private set; }
    public bool Landed { get; private set; }

    public void Drop(float x, float y) { X = x; Y = y; VelocityX = VelocityY = 0; Grounded = false; }
    public void Launch(float vx, float vy) { VelocityX = vx; VelocityY = vy; Grounded = false; if (vx != 0) Direction = vx > 0 ? 1 : -1; }
    public void Step(float seconds, IReadOnlyList<Perch> surfaces, Rectangle bounds, float speed)
    {
        float dt = Math.Clamp(seconds, 0, .05f);
        Landed = false;
        bool Supported(float x) => surfaces.Any(s => x >= s.Left && x <= s.Right && Math.Abs(Y - s.Top) < 2);
        if (Grounded && !Supported(X)) Grounded = false;
        if (Grounded)
        {
            X += Direction * speed * dt;
            if (X <= bounds.Left + 24) { X = bounds.Left + 24; Direction = 1; }
            if (X >= bounds.Right - 24) { X = bounds.Right - 24; Direction = -1; }
            if (!Supported(X)) Grounded = false;
        }
        if (!Grounded)
        {
            float before = Y, beforeX = X;
            X += VelocityX * dt;
            Y += VelocityY * dt + .5f * Gravity * dt * dt;
            VelocityY += Gravity * dt;
            if (X < bounds.Left + 24) { X = bounds.Left + 24; VelocityX = Math.Abs(VelocityX) * .35f; }
            if (X > bounds.Right - 24) { X = bounds.Right - 24; VelocityX = -Math.Abs(VelocityX) * .35f; }
            var hit = surfaces.Where(s => {
                float crossingX = beforeX + (X - beforeX) * (Y == before ? 0 : (s.Top - before) / (Y - before));
                return VelocityY >= 0 && crossingX >= s.Left && crossingX <= s.Right && s.Top >= before - .01f && s.Top <= Y;
            })
                .OrderBy(s => s.Top).Cast<Perch?>().FirstOrDefault();
            if (hit is { } p) { Y = p.Top; VelocityX = VelocityY = 0; Grounded = true; Landed = true; }
        }
    }
}
