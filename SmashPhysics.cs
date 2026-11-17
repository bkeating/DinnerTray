using System.Drawing;

namespace DinnerTray;

internal sealed class SmashBlock
{
    public float X, Y, Vx, Vy, Health = 1;
    public const float Size = 38;
    public RectangleF Rect => new(X, Y, Size, Size);
}
internal sealed class SmashWorld
{
    public List<SmashBlock> Blocks { get; } = new();
    public List<(PointF Position, PointF Velocity, float Life)> Chips { get; } = new();
    public int Broken { get; private set; }
    public int Total { get; private set; }
    public Rectangle Area { get; private set; }
    public void Reset(Rectangle area)
    {
        Area = area; Blocks.Clear(); Chips.Clear(); Broken = 0;
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4 - row; col++)
                Blocks.Add(new SmashBlock { X = area.Right - 250 + col * 40 + row * 20, Y = area.Bottom - (row + 1) * 38 });
        Total = Blocks.Count;
    }
    public void Step(float dt)
    {
        foreach (var b in Blocks)
        {
            b.Vy += DesktopPhysics.Gravity * dt; b.X += b.Vx * dt; b.Y += b.Vy * dt;
            if (b.Y + SmashBlock.Size >= Area.Bottom)
            { b.Y = Area.Bottom - SmashBlock.Size; b.Vy = Math.Abs(b.Vy) > 80 ? -Math.Abs(b.Vy) * .15f : 0; b.Vx *= MathF.Exp(-5 * dt); }
            if (b.X < Area.Left) { b.X = Area.Left; b.Vx = Math.Abs(b.Vx) * .3f; }
            if (b.X + SmashBlock.Size > Area.Right) { b.X = Area.Right - SmashBlock.Size; b.Vx = -Math.Abs(b.Vx) * .3f; }
        }
        // Sequential impulses, equal block mass; several passes keep stacks stable.
        for (int pass = 0; pass < 6; pass++)
            for (int i = 0; i < Blocks.Count; i++) for (int j = i + 1; j < Blocks.Count; j++)
            {
                var a = Blocks[i]; var b = Blocks[j];
                var overlap = RectangleF.Intersect(a.Rect, b.Rect);
                if (overlap.Width <= 0 || overlap.Height <= 0) continue;
                bool vertical = overlap.Height <= overlap.Width;
                float nx = vertical ? 0 : a.X < b.X ? -1 : 1;
                float ny = vertical ? (a.Y < b.Y ? -1 : 1) : 0;
                float depth = vertical ? overlap.Height : overlap.Width;
                // A block on the floor is fixed vertically during contact resolution.
                float wa = vertical && a.Y >= Area.Bottom - SmashBlock.Size - .1f ? 0 : 1;
                float wb = vertical && b.Y >= Area.Bottom - SmashBlock.Size - .1f ? 0 : 1;
                float total = wa + wb; if (total == 0) continue;
                a.X += nx * depth * wa / total; a.Y += ny * depth * wa / total;
                b.X -= nx * depth * wb / total; b.Y -= ny * depth * wb / total;
                float closing = (a.Vx - b.Vx) * nx + (a.Vy - b.Vy) * ny;
                if (closing >= 0) continue;
                float impulse = -closing * 1.08f / total;
                a.Vx += nx * impulse * wa; a.Vy += ny * impulse * wa;
                b.Vx -= nx * impulse * wb; b.Vy -= ny * impulse * wb;
                if (closing < -260) { a.Health -= -closing / 1600; b.Health -= -closing / 1600; }
            }
        BreakDead();
        for (int i = Chips.Count - 1; i >= 0; i--)
        {
            var c = Chips[i]; float life = c.Life - dt;
            if (life <= 0) { Chips.RemoveAt(i); continue; }
            Chips[i] = (new(c.Position.X + c.Velocity.X * dt, c.Position.Y + c.Velocity.Y * dt), new(c.Velocity.X, c.Velocity.Y + DesktopPhysics.Gravity * dt), life);
        }
    }
    public void Hit(DesktopPhysics sheep)
    {
        var center = new PointF(sheep.X, sheep.Y - 26);
        foreach (var b in Blocks)
        {
            float dx = center.X - Math.Clamp(center.X, b.X, b.X + SmashBlock.Size);
            float dy = center.Y - Math.Clamp(center.Y, b.Y, b.Y + SmashBlock.Size);
            float distance = MathF.Sqrt(dx*dx + dy*dy);
            if (distance >= 26) continue;
            if (distance < .01f) { dx = sheep.VelocityX >= 0 ? -1 : 1; dy = 0; distance = 1; }
            float nx = dx / distance, ny = dy / distance;
            float closing = (sheep.VelocityX - b.Vx)*nx + (sheep.VelocityY - b.Vy)*ny;
            if (closing >= 0) continue;
            float impulse = -closing * 1.2f / 1.25f; // sheep mass 4, block mass 1
            sheep.VelocityX += nx * impulse / 4; sheep.VelocityY += ny * impulse / 4;
            b.Vx -= nx * impulse; b.Vy -= ny * impulse;
            sheep.X += nx * (26 - distance) * .2f; sheep.Y += ny * (26 - distance) * .2f;
            b.Health -= -closing / 650;
        }
        BreakDead();
    }
    private void BreakDead()
    {
        foreach (var b in Blocks.Where(b => b.Health <= 0).ToArray())
        {
            Blocks.Remove(b); Broken++;
            for (int i = 0; i < 7; i++) Chips.Add((new(b.X+19,b.Y+19), new(b.Vx*.3f+Random.Shared.Next(-180,181), b.Vy*.2f-Random.Shared.Next(80,250)), .65f));
        }
    }
}
