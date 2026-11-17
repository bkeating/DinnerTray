using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace DinnerTray;

internal enum CreaturePose { Walk, Idle, Fall, Held, Land }
internal interface IDesktopCreature
{
    string Name { get; }
    void Draw(Graphics g, CreaturePose pose, double clock, int direction);
}

// Original pixel artwork, drawn at 3× scale. Future creatures only implement this renderer.
internal sealed class WoollySheep : IDesktopCreature
{
    public string Name => "Woolly";
    public void Draw(Graphics g, CreaturePose pose, double clock, int direction)
    {
        g.SmoothingMode = SmoothingMode.None;
        g.ScaleTransform(3, 3);
        if (direction < 0) { g.TranslateTransform(32, 0); g.ScaleTransform(-1, 1); }
        int stride = pose == CreaturePose.Walk ? (int)(clock * 9) % 4 : 0;
        int bob = pose == CreaturePose.Walk && stride % 2 == 1 ? -1 : 0;
        if (pose == CreaturePose.Land) bob = 2;
        void Box(Color c, int x, int y, int w, int h) { using var b = new SolidBrush(c); g.FillRectangle(b, x, y, w, h); }
        var ink = Color.FromArgb(49, 48, 60);
        var wool = Color.FromArgb(255, 248, 223);
        var shade = Color.FromArgb(212, 207, 193);
        int feet = pose is CreaturePose.Held or CreaturePose.Fall ? 1 : 0;
        // Alternating little hooves, including a dangling pose when carried.
        foreach (int leg in new[] { 9, 13, 20, 23 })
        {
            int swing = pose == CreaturePose.Walk ? (leg % 2 == stride % 2 ? 1 : -1) : 0;
            Box(ink, leg + swing, 17 + bob, 2, 5 + feet - bob);
            Box(ink, leg + swing - 1, 21 + feet, 3, 1);
        }
        // Stepped cloud silhouette, with warm shadow and individual wool tufts.
        Box(ink, 6, 8 + bob, 20, 11); Box(ink, 9, 5 + bob, 14, 16);
        Box(ink, 4, 11 + bob, 24, 5);
        Box(shade, 7, 9 + bob, 18, 10); Box(shade, 10, 6 + bob, 12, 14);
        Box(wool, 7, 9 + bob, 17, 7); Box(wool, 10, 6 + bob, 11, 11);
        Box(wool, 5, 12 + bob, 20, 3); Box(wool, 9, 8 + bob, 14, 9);
        Box(Color.White, 10, 7 + bob, 4, 2); Box(Color.White, 7, 10 + bob, 3, 2);
        Box(shade, 11, 16 + bob, 2, 2); Box(shade, 17, 17 + bob, 2, 2);
        // Face, ears, cream forelock, bright eyes and a pink nose.
        Box(ink, 23, 9 + bob, 7, 9); Box(ink, 21, 8 + bob, 3, 3);
        Box(Color.FromArgb(108, 99, 112), 22, 9 + bob, 2, 1);
        Box(wool, 24, 7 + bob, 4, 3);
        bool blink = pose == CreaturePose.Idle && clock % 4.5 < .18;
        Box(Color.White, 27, 11 + bob, 2, blink ? 1 : 3);
        if (!blink) Box(ink, 28, 12 + bob, 1, 1);
        Box(Color.FromArgb(224, 151, 151), 28, 16 + bob, 2, 1);
        Box(wool, 3, 10 + bob, 3, 3);
    }
}

internal sealed class DesktopSheep : Form
{
    private readonly IDesktopCreature creature;
    public DesktopPhysics Body { get; } = new();
    private bool dragging;
    private Point grab;
    private readonly ThrowGesture gesture = new();
    public SlingGame? Game;
    public bool GameFlight;
    public bool IsDragging => dragging;
    private static double InputTime => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    public void Place(float x, float y) { Body.Drop(x,y); Location = new Point((int)x-48,(int)y-66); }
    private double clock, idleUntil, nextDecision = 3, landedUntil;
    public DesktopSheep(IDesktopCreature creature, Point spawn)
    {
        this.creature = creature;
        Text = creature.Name + " — DinnerTray";
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(96, 72);
        BackColor = Color.Magenta; TransparencyKey = Color.Magenta;
        DoubleBuffered = true; Cursor = Cursors.Hand;
        StartPosition = FormStartPosition.Manual;
        Body.Drop(spawn.X, spawn.Y);
        Location = new Point(spawn.X - 48, spawn.Y - 66);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Send this sheep home", null, (_, _) => Close());
        ContextMenuStrip = menu;
        FormClosed += (_, _) => menu.Dispose();
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } // MA_NOACTIVATE
        base.WndProc(ref m);
    }
    public void Animate(float dt, IReadOnlyList<Perch> surfaces)
    {
        clock += dt;
        if (!dragging && Game?.Loaded != this)
        {
            var screen = Screen.FromPoint(new Point((int)Body.X, (int)Body.Y));
            var area = screen.WorkingArea;
            // Recover after monitor removal or taskbar/work-area changes.
            if (Body.Y > area.Bottom || Body.X < area.Left || Body.X > area.Right)
                Body.Drop(Math.Clamp(Body.X, area.Left + 48, area.Right - 48), Math.Min(Body.Y, area.Bottom));
            if (Body.Grounded && clock >= nextDecision)
            {
                idleUntil = clock + Random.Shared.NextDouble() * 2 + .7;
                nextDecision = idleUntil + Random.Shared.Next(4, 10);
                if (Random.Shared.Next(3) == 0) Body.Direction *= -1;
            }
            var activeSurfaces = GameFlight && Game is { IsDisposed: false } ? new[] {new Perch(Game.World.Area.Left,Game.World.Area.Right,Game.World.Area.Bottom)} : surfaces;
            Body.Step(dt, activeSurfaces, GameFlight && Game is { IsDisposed: false } ? Game.World.Area : area, clock < idleUntil ? 0 : 34);
            if (Game is { IsDisposed: false } && !Body.Grounded) Game.World.Hit(Body);
            if (Body.Grounded) GameFlight = false;
            if (Body.Landed) landedUntil = clock + .16;
            Location = new Point((int)Body.X - 48, (int)Body.Y - 66);
        }
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var pose = dragging ? CreaturePose.Held : !Body.Grounded ? CreaturePose.Fall : clock < landedUntil ? CreaturePose.Land : clock < idleUntil ? CreaturePose.Idle : CreaturePose.Walk;
        creature.Draw(e.Graphics, pose, clock, Body.Direction);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        dragging = true; grab = e.Location; Capture = true;
        GameFlight = false; gesture.Clear(); gesture.Add(Cursor.Position, InputTime);
        Body.Launch(0,0);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging) return;
        var cursor = Cursor.Position;
        gesture.Add(cursor,InputTime);
        if (Game?.Loaded == this) { Game.Pull(this,new PointF(cursor.X-grab.X+48,cursor.Y-grab.Y+40)); return; }
        Location = new Point(cursor.X - grab.X, cursor.Y - grab.Y);
        Body.Drop(Left + 48, Top + 66);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && dragging)
        {
            gesture.Add(Cursor.Position, InputTime);
            if (Game?.Loaded == this) Game.Release(this);
            else if (Game?.TryLoad(this) != true) { var velocity=gesture.Velocity(); Body.Launch(velocity.X,velocity.Y); }
            dragging = false; Capture = false;
        }
    }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
}

internal sealed class DesktopPets : IDisposable
{
    private readonly List<DesktopSheep> sheep = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double lastTick, nextScan;
    private List<Perch> surfaces = new();
    private SlingGame? game;
    public void StartGame()
    {
        if (game is { IsDisposed:false }) { game.Reset(); return; }
        game = new SlingGame(Screen.FromPoint(Cursor.Position).WorkingArea);
        foreach(var pet in sheep) pet.Game = game;
        game.FormClosed += (_,_) => { foreach(var pet in sheep) { pet.Game=null; pet.GameFlight=false; } game=null; if(sheep.Count==0)timer.Stop(); };
        game.Show();
        if(sheep.Count==0) Spawn();
        nextScan=0; lastTick=clock.Elapsed.TotalSeconds; timer.Start();
    }
    public void StopGame() => game?.Close();
    public DesktopPets() { timer.Tick += (_, _) => Tick(); }
    public void Spawn()
    {
        if (sheep.Count >= 12) return;
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        var pet = new DesktopSheep(new WoollySheep(), new Point(Math.Clamp(Cursor.Position.X, screen.Left + 60, screen.Right - 60), screen.Top + 90));
        pet.Game=game;
        sheep.Add(pet); pet.FormClosed += (_, _) => { sheep.Remove(pet); if (sheep.Count == 0 && game==null) timer.Stop(); };
        pet.Show(); nextScan = 0; lastTick = clock.Elapsed.TotalSeconds; timer.Start();
    }
    private void Tick()
    {
        double now = clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(.05, now - lastTick); lastTick = now;
        if (now >= nextScan)
        {
            var exclude=sheep.Select(s=>s.Handle).ToHashSet(); if(game!=null)exclude.Add(game.Handle);
            surfaces = DesktopSurfaces.Read(exclude); nextScan = now + .2;
        }
        if(game!=null && !Screen.AllScreens.Any(s=>s.WorkingArea==game.World.Area)) StopGame();
        // Small fixed-size substeps prevent fast projectiles tunneling through thin blocks.
        int steps=Math.Max(1,(int)Math.Ceiling(dt*240)); float step=dt/steps;
        for(int i=0;i<steps;i++) { game?.World.Step(step); foreach(var pet in sheep.ToArray())pet.Animate(step,surfaces); }
        game?.RenderTick();
    }
    public void Clear() { foreach (var pet in sheep.ToArray()) pet.Close(); }
    public void Dispose() { timer.Stop(); StopGame(); Clear(); timer.Dispose(); }
}
