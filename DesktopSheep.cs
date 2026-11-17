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
    internal bool IsLamb { get; }
    internal bool Paired;
    internal bool Mounted;
    internal bool Available => !IsDisposed && !IsLamb && !dragging && !GameFlight && Game?.Loaded != this
        && Body.Grounded && Behavior.Pose(Body) is CreaturePose.Walk or CreaturePose.Idle;
    internal readonly SheepBehavior Behavior = new(Random.Shared);
    internal CreaturePose CurrentPose => dragging || Game?.Loaded == this ? CreaturePose.Held : Behavior.Pose(Body);
    public void Place(float x, float y)
    {
        Paired = false;
        Behavior.Interrupt();
        Body.Drop(x, y);
        Location = new Point((int)x - FootX, (int)y - FootY);
    }
    private double clock;
    public DesktopSheep(IDesktopCreature creature, Point spawn, bool isLamb = false)
    {
        this.creature = creature;
        IsLamb = isLamb;
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
            menu.Items.Add(label, null, (_, _) => { Paired = false; Behavior.Act(pose, Body); });
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
            if (!Paired) Behavior.BeforeStep(dt, Body);
            var activeSurfaces = GameFlight && Game is { IsDisposed: false } ? new[] {new Perch(Game.World.Area.Left,Game.World.Area.Right,Game.World.Area.Bottom)} : surfaces;
            Body.Step(dt, activeSurfaces, GameFlight && Game is { IsDisposed: false } ? Game.World.Area : area, Paired ? 0 : Behavior.WalkSpeed);
            if (Game is { IsDisposed: false } && !Body.Grounded) Game.World.Hit(Body);
            if (Body.Grounded) GameFlight = false;
            Behavior.AfterStep(Body);
            int lift = Paired && Mounted ? 23 + (int)(2 * Math.Sin(clock * 5)) : 0;
            Location = new Point((int)Body.X - FootX, (int)Body.Y - FootY - lift);
        }
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var state = g.Save();
        if (IsLamb || (Paired && Mounted))
        {
            g.TranslateTransform(FootX, FootY);
            if (IsLamb) g.ScaleTransform(.6f, .6f);
            else g.RotateTransform(-16 * Body.Direction);
            g.TranslateTransform(-FootX, -FootY);
        }
        creature.Draw(g, Paired ? CreaturePose.Idle : CurrentPose, clock, Body.Direction);
        g.Restore(state);
        if (Paired)
        {
            for (int i = 0; i < 3; i++)
            {
                double phase = (clock * .65 + i / 3.0 + (Mounted ? .5 : 0)) % 1;
                int x = (int)(18 + i * 37 + 4 * Math.Sin(phase * Math.Tau));
                int y = (int)(20 - phase * 19);
                var brush = i == 1 ? Brushes.HotPink : Brushes.LightCoral;
                g.FillRectangles(brush, [new Rectangle(x, y, 4, 2), new(x + 6, y, 4, 2),
                    new(x, y + 2, 10, 2), new(x + 2, y + 4, 6, 2), new(x + 4, y + 6, 2, 2)]);
            }
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Paired = false;
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
    internal IReadOnlyList<DesktopSheep> Flock => sheep;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double lastTick, nextScan;
    private List<Perch> surfaces = new();
    private SlingGame? game;
    private (DesktopSheep Rear, DesktopSheep Front)? pair;
    private float socialDelay = 5;
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
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        SpawnAt(new Point(Math.Clamp(Cursor.Position.X, screen.Left + 60, screen.Right - 60), screen.Top + 90));
    }
    public void SpawnHerd()
    {
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        int spacing = Math.Min(60, (screen.Width - 120) / 9);
        int start = Math.Clamp(Cursor.Position.X - 9 * spacing / 2, screen.Left + 60, screen.Right - 60 - 9 * spacing);
        for (int i = 0; i < 10; i++)
            SpawnAt(new Point(start + i * spacing, screen.Top + 90));
    }
    private void SpawnAt(Point feet, bool isLamb = false)
    {
        var pet = new DesktopSheep(new WoollySheep(nextStyle), feet, isLamb);
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
        UpdatePair(dt, surfaces, Random.Shared);
    }
    internal void UpdatePair(float dt, IReadOnlyList<Perch> perches, Random random)
    {
        socialDelay -= dt;
        if (pair is { } active)
        {
            var (rear, front) = active;
            bool together = rear.Paired && front.Paired && rear.Available && front.Available;
            if (together && socialDelay > 0) return;
            rear.Paired = front.Paired = rear.Mounted = false;
            pair = null;
            if (together)
                SpawnAt(new Point((int)((rear.Body.X + front.Body.X) / 2), (int)front.Body.Y - 2), true);
            socialDelay = random.Next(20, 41);
            return;
        }
        if (socialDelay > 0) return;
        socialDelay = random.Next(4, 10);
        if (random.Next(3) == 0 || sheep.Count(p => p.IsLamb) >= sheep.Count(p => !p.IsLamb)) return;
        foreach (var rear in sheep.Where(p => p.Available).OrderBy(_ => random.Next()))
        {
            var front = sheep.FirstOrDefault(p => p.Available && Math.Abs(p.Body.X - rear.Body.X) is >= 30 and <= 64
                && p.Body.Y == rear.Body.Y && perches.Any(s => s.Top == p.Body.Y
                    && s.Left <= Math.Min(p.Body.X, rear.Body.X) && s.Right >= Math.Max(p.Body.X, rear.Body.X)));
            if (front == null) continue;
            rear.Body.Direction = front.Body.Direction = Math.Sign(front.Body.X - rear.Body.X);
            rear.Paired = front.Paired = rear.Mounted = true;
            rear.BringToFront();
            pair = (rear, front);
            socialDelay = 3;
            break;
        }
    }
    public void Clear() { foreach (var pet in sheep.ToArray()) pet.Close(); }
    public void Dispose() { timer.Stop(); StopGame(); Clear(); timer.Dispose(); }
}
