using System.Drawing.Drawing2D;

namespace DinnerTray;

// Columns in the atlas follow this order, four animation frames per pose.
internal enum CreaturePose { Walk, Idle, Fall, Held, Land, Sit, Sleep, Jump, Faint }
internal interface IDesktopCreature
{
    string Name { get; }
    void Draw(Graphics g, CreaturePose pose, double seconds, int direction);
}

internal sealed class WoollySheep(int style = 0) : IDesktopCreature
{
    internal static readonly string[] Names = ["Woolly", "Clover", "Luna", "Fern", "Bramble"];
    public string Name => Names[style];
    private static readonly Bitmap Atlas = LoadAtlas();
    private static Bitmap LoadAtlas()
    {
        using var stream = typeof(WoollySheep).Assembly.GetManifestResourceStream("DinnerTray.Sheep.png")
            ?? throw new InvalidOperationException("Missing sheep sprite sheet.");
        using var source = new Bitmap(stream);
        return new Bitmap(source);
    }

    public void Draw(Graphics g, CreaturePose pose, double seconds, int direction)
    {
        double fps = pose switch { CreaturePose.Sleep => 1.6, CreaturePose.Sit => 2, CreaturePose.Fall => 11, _ => 7 };
        int frame = pose == CreaturePose.Idle ? (seconds % 3.1 > 2.94 ? 3 : 0) : (int)(seconds * fps) % 4;
        var state = g.Save();
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        if (direction < 0) { g.TranslateTransform(120, 0); g.ScaleTransform(-1, 1); }
        g.DrawImage(Atlas, new Rectangle(0, 0, 120, 96), ((int)pose * 4 + frame) * 40, style * 32, 40, 32, GraphicsUnit.Pixel);
        g.Restore(state);
    }
}

// Decisions only affect a supported sheep. Physics owns every airborne position.
internal sealed class SheepBehavior(Random random)
{
    private CreaturePose activity = CreaturePose.Walk;
    private double remaining = 1 + random.NextDouble() * 3;
    private float jumpStart, impactSpeed;
    private bool jumping, dozeAfterSit, faintAfterLanding;
    public CreaturePose Pose(DesktopPhysics body) => body.Grounded ? activity :
        jumping && body.Y <= jumpStart ? CreaturePose.Jump : CreaturePose.Fall;
    public float WalkSpeed => activity == CreaturePose.Walk ? 34 : 0;

    public void Interrupt()
    {
        jumping = dozeAfterSit = faintAfterLanding = false;
        impactSpeed = 0;
        activity = CreaturePose.Walk;
    }

    public void Act(CreaturePose pose, DesktopPhysics body)
    {
        if (!body.Grounded) return;
        Interrupt();
        activity = pose;
        remaining = pose switch
        {
            CreaturePose.Sleep => 4 + random.NextDouble() * 4,
            CreaturePose.Faint => 1.5,
            CreaturePose.Land => .18,
            CreaturePose.Walk => 3 + random.NextDouble() * 5,
            _ => 1.2 + random.NextDouble() * 1.8
        };
        if (pose == CreaturePose.Jump)
        {
            jumping = true;
            jumpStart = body.Y;
            body.Launch(body.Direction * 72, -235);
        }
    }

    public void BeforeStep(float dt, DesktopPhysics body)
    {
        if (!body.Grounded) { impactSpeed = Math.Max(impactSpeed, body.VelocityY); return; }
        remaining -= dt;
        if (remaining > 0) return;
        if (activity == CreaturePose.Walk)
        {
            CreaturePose choice = random.Next(4) switch
            {
                0 => CreaturePose.Idle, 1 => CreaturePose.Jump, _ => CreaturePose.Sit
            };
            Act(choice, body);
            dozeAfterSit = choice == CreaturePose.Sit && random.Next(2) == 0;
        }
        else if (activity == CreaturePose.Sit && dozeAfterSit) Act(CreaturePose.Sleep, body);
        else if (activity == CreaturePose.Land && faintAfterLanding) Act(CreaturePose.Faint, body);
        else
        {
            if (activity != CreaturePose.Land && random.Next(3) == 0) body.Direction *= -1;
            Act(CreaturePose.Walk, body);
        }
    }

    public void AfterStep(DesktopPhysics body)
    {
        if (!body.Landed) return;
        bool hardLanding = impactSpeed > 560;
        Act(CreaturePose.Land, body);
        faintAfterLanding = hardLanding;
    }
}
