using DinnerTray;
using System.Drawing.Imaging;

internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        CheckSheep();
        RenderFlock();
        foreach(int width in new[]{1280,1920,3440})
        {
            using var game=new SlingGame(new Rectangle(0,0,width,1000));
            using var pet=new DesktopSheep(new WoollySheep(),new Point(200,200)); pet.Game=game;
            pet.Place(game.SlingAnchor.X,game.SlingAnchor.Y+26);
            Require(game.TryLoad(pet),"Load sheep in sling");
            using var other=new DesktopSheep(new WoollySheep(),new Point((int)game.SlingAnchor.X,(int)game.SlingAnchor.Y+26));
            Require(!game.TryLoad(other),"Only one sheep occupies sling");
            game.Pull(pet,new(game.SlingAnchor.X-1000,game.SlingAnchor.Y+1000));
            Require(Math.Abs(Math.Sqrt(Math.Pow(pet.Body.X-game.SlingAnchor.X,2)+Math.Pow(pet.Body.Y-26-game.SlingAnchor.Y,2))-145)<.1,"Elastic pull limit");
            game.Release(pet);
            Require(game.Loaded==null && pet.GameFlight && pet.Body.VelocityX>0 && pet.Body.VelocityY<0,"Release launches opposite pull");
            // Exercise the actual sling calibration and production collision engine.
            bool reached=false;
            for(int pull=50;pull<=115 && !reached;pull+=5)
            {
                game.Reset();pet.Place(game.SlingAnchor.X,game.SlingAnchor.Y+26);game.TryLoad(pet);
                game.Pull(pet,new(game.SlingAnchor.X-pull,game.SlingAnchor.Y+pull*.65f));game.Release(pet);
                for(int step=0;step<1200 && !pet.Body.Grounded;step++)
                {
                    game.World.Step(1f/240);
                    pet.Body.Step(1f/240,new[]{new Perch(0,width,1000)},game.World.Area,0);
                    if(!pet.Body.Grounded)game.World.Hit(pet.Body);
                }
                reached=game.World.Broken>0;
            }
            Require(reached,$"Pile reachable on {width}px display");
            game.Reset();pet.Place(game.SlingAnchor.X,game.SlingAnchor.Y+26);game.TryLoad(pet);game.Reset();
            Require(game.Loaded==null,"Reset releases loaded sheep");
            Console.WriteLine($"PASS: loading, elastic limit, launch, reachable pile, reset at {width}px");
        }
        using var preview=new SlingGame(new Rectangle(0,0,1280,420));
        using var woolly=new DesktopSheep(new WoollySheep(),new Point(0,0));woolly.Game=preview;
        woolly.Place(preview.SlingAnchor.X,preview.SlingAnchor.Y+26);preview.TryLoad(woolly);
        preview.Pull(woolly,new(preview.SlingAnchor.X-80,preview.SlingAnchor.Y+52));
        preview.BackColor=Color.FromArgb(29,34,43);
        using var bitmap=new Bitmap(1280,420);preview.DrawToBitmap(bitmap,new Rectangle(0,0,1280,420));
        using(var g=Graphics.FromImage(bitmap)) {g.TranslateTransform(woolly.Left,woolly.Top);new WoollySheep().Draw(g,CreaturePose.Held,1,1);}
        Directory.CreateDirectory("test-results");bitmap.Save("test-results/slingshot-preview.png",ImageFormat.Png);
        Console.WriteLine("PASS: rendered game preview");
    }
    static void CheckSheep()
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        int top = area.Top + 120, x = area.Left + area.Width / 2;
        Perch[] ledge = [new(x - 120, x + 120, top)];
        Perch[] floor = [new(area.Left, area.Right, area.Bottom)];
        using var pet = new DesktopSheep(new WoollySheep(), new Point(x, top));
        void Advance(int ticks, Perch[] surfaces)
        {
            for (int i = 0; i < ticks; i++) pet.Animate(1f / 240, surfaces);
        }
        Advance(1, ledge);
        foreach (var (label, pose) in new[] { ("Sit down", CreaturePose.Sit), ("Play faint", CreaturePose.Faint),
                                              ("Take a nap", CreaturePose.Sleep), ("Jump!", CreaturePose.Jump) })
        {
            pet.ContextMenuStrip!.Items.Cast<ToolStripItem>().Single(item => item.Text == label).PerformClick();
            Require(pet.CurrentPose == pose, label);
            Advance(12, ledge);
            if (pose == CreaturePose.Jump) Require(pet.Body.Y < top - 5, "Jump rises above perch");
            else Require(pet.Body.X == x && pet.Body.Y == top, "Resting sheep stays perched");
        }
        Advance(140, ledge);
        Require(pet.Body.Grounded, "Hop lands on ledge");
        pet.Behavior.Act(CreaturePose.Sleep, pet.Body);
        Advance(1, floor);
        Require(pet.CurrentPose == CreaturePose.Fall, "Removing a window wakes sleeping sheep into fall");
        for (int i = 0; i < 2000 && !pet.Body.Grounded; i++) Advance(1, floor);
        Require(pet.CurrentPose == CreaturePose.Land, "Fall ends in landing squash");
        Advance(60, floor);
        Require(pet.CurrentPose == CreaturePose.Faint, "Hard landing causes faint");
        Advance(400, floor);
        Require(pet.CurrentPose == CreaturePose.Walk, "Sheep recovers from faint");

        var body = new DesktopPhysics();
        var brain = new SheepBehavior(new Random(37));
        var seen = new HashSet<CreaturePose>();
        body.Drop(x, area.Bottom);
        for (int i = 0; i < 240 * 250; i++)
        {
            brain.BeforeStep(1f / 240, body);
            body.Step(1f / 240, floor, area, brain.WalkSpeed);
            brain.AfterStep(body);
            seen.Add(brain.Pose(body));
        }
        Require(new[] { CreaturePose.Walk, CreaturePose.Sit, CreaturePose.Sleep, CreaturePose.Jump }.All(seen.Contains),
                "Autonomous walk, sit, doze and jump");
        Console.WriteLine("PASS: sheep commands, autonomous behavior, perch loss, hard landing and recovery");
    }

    static void RenderFlock()
    {
        CreaturePose[] poses = [CreaturePose.Walk, CreaturePose.Sit, CreaturePose.Sleep, CreaturePose.Jump,
                                CreaturePose.Held, CreaturePose.Fall, CreaturePose.Land, CreaturePose.Faint];
        using var sheet = new Bitmap(90 + 120 * poses.Length, 38 + 96 * WoollySheep.Names.Length);
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(39, 44, 57));
        using var font = new Font("Segoe UI", 10, FontStyle.Bold);
        for (int i = 0; i < poses.Length; i++)
            g.DrawString(poses[i].ToString(), font, Brushes.Wheat, 125 + i * 120, 14);
        for (int row = 0; row < WoollySheep.Names.Length; row++)
        {
            var woolly = new WoollySheep(row);
            g.DrawString(woolly.Name, font, Brushes.WhiteSmoke, 14, 76 + row * 96);
            for (int col = 0; col < poses.Length; col++)
            {
                var state = g.Save();
                g.TranslateTransform(90 + 120 * col, 38 + row * 96);
                woolly.Draw(g, poses[col], .2, 1);
                g.Restore(state);
            }
        }
        Directory.CreateDirectory("test-results");
        sheet.Save("test-results/flock.png", ImageFormat.Png);
        Console.WriteLine("PASS: rendered five styles in eight poses");
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
}
