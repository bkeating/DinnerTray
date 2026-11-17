using System.Diagnostics;

namespace DinnerTray;

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
    private const int FootX = 60, FootY = 84;
    internal readonly SheepBehavior Behavior = new(Random.Shared);
    internal CreaturePose CurrentPose => dragging || Game?.Loaded == this ? CreaturePose.Held : Behavior.Pose(Body);
    public void Place(float x, float y)
    {
        Behavior.Interrupt();
        Body.Drop(x, y);
        Location = new Point((int)x - FootX, (int)y - FootY);
    }
    private double clock;
    public DesktopSheep(IDesktopCreature creature, Point spawn)
    {
        this.creature = creature;
        Text = creature.Name + " — DinnerTray";
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(120, 96);
        BackColor = Color.Magenta; TransparencyKey = Color.Magenta;
        DoubleBuffered = true; Cursor = Cursors.Hand;
        StartPosition = FormStartPosition.Manual;
        Place(spawn.X, spawn.Y);
        var menu = new ContextMenuStrip();
        foreach (var (label, pose) in new[] { ("Sit down", CreaturePose.Sit), ("Take a nap", CreaturePose.Sleep),
                                              ("Jump!", CreaturePose.Jump), ("Play faint", CreaturePose.Faint) })
            menu.Items.Add(label, null, (_, _) => Behavior.Act(pose, Body));
        menu.Opening += (_, _) =>
        {
            for (int i = 0; i < 4; i++) menu.Items[i].Enabled = Body.Grounded && !dragging && Game?.Loaded != this;
        };
        menu.Items.Add(new ToolStripSeparator());
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
            Behavior.BeforeStep(dt, Body);
            var activeSurfaces = GameFlight && Game is { IsDisposed: false } ? new[] {new Perch(Game.World.Area.Left,Game.World.Area.Right,Game.World.Area.Bottom)} : surfaces;
            Body.Step(dt, activeSurfaces, GameFlight && Game is { IsDisposed: false } ? Game.World.Area : area, Behavior.WalkSpeed);
            if (Game is { IsDisposed: false } && !Body.Grounded) Game.World.Hit(Body);
            if (Body.Grounded) GameFlight = false;
            Behavior.AfterStep(Body);
            Location = new Point((int)Body.X - FootX, (int)Body.Y - FootY);
        }
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        creature.Draw(e.Graphics, CurrentPose, clock, Body.Direction);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Behavior.Interrupt();
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
        if (Game?.Loaded == this) { Game.Pull(this,new PointF(cursor.X-grab.X+FootX,cursor.Y-grab.Y+FootY-26)); return; }
        Location = new Point(cursor.X - grab.X, cursor.Y - grab.Y);
        Body.Drop(Left + FootX, Top + FootY);
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
    private int nextStyle;
    public void Spawn()
    {
        if (sheep.Count >= 12) return;
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        var pet = new DesktopSheep(new WoollySheep(nextStyle), new Point(Math.Clamp(Cursor.Position.X, screen.Left + 60, screen.Right - 60), screen.Top + 90));
        nextStyle = (nextStyle + 1) % WoollySheep.Names.Length;
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
