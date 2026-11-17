using System.Drawing.Drawing2D;

namespace DinnerTray;

internal sealed class SlingGame : Form
{
    public SmashWorld World { get; } = new();
    public DesktopSheep? Loaded { get; private set; }
    public bool Aiming { get; private set; }
    public PointF SlingAnchor => new(World.Area.Left + Math.Min(190, World.Area.Width * .2f), World.Area.Bottom - 125);
    private float Power => MathF.Sqrt(World.Area.Width * DesktopPhysics.Gravity) / 125 * 1.25f;
    private Rectangle resetButton, closeButton;
    public SlingGame(Rectangle area)
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta; TransparencyKey = Color.Magenta; DoubleBuffered = true;
        Text = "Woolly Slingshot — DinnerTray"; Bounds = area; World.Reset(area);
        ContextMenuStrip = new ContextMenuStrip();
        ContextMenuStrip.Items.Add("Reset blocks", null, (_, _) => Reset());
        ContextMenuStrip.Items.Add("Close slingshot", null, (_, _) => Close());
        FormClosed += (_, _) => { Unload(); ContextMenuStrip.Dispose(); };
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
    protected override void WndProc(ref Message m) { if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } base.WndProc(ref m); }
    public void Reset() { Unload(); World.Reset(Bounds); Invalidate(); }
    public bool TryLoad(DesktopSheep pet)
    {
        if (Loaded != null || IsDisposed) return false;
        float dx = pet.Body.X - SlingAnchor.X, dy = pet.Body.Y - 26 - SlingAnchor.Y;
        if (dx*dx + dy*dy > 85*85) return false;
        Loaded = pet; Aiming = false; pet.GameFlight = false;
        pet.Place(SlingAnchor.X, SlingAnchor.Y + 26); Invalidate(); return true;
    }
    public void Pull(DesktopSheep pet, PointF center)
    {
        if (Loaded != pet) return;
        Aiming = true;
        float dx = center.X - SlingAnchor.X, dy = center.Y - SlingAnchor.Y;
        float length = MathF.Sqrt(dx*dx + dy*dy);
        if (length > 145) { dx *= 145/length; dy *= 145/length; }
        pet.Place(SlingAnchor.X + dx, SlingAnchor.Y + dy + 26); Invalidate();
    }
    private PointF LaunchVelocity() => Loaded == null ? PointF.Empty : new((SlingAnchor.X - Loaded.Body.X)*Power, (SlingAnchor.Y - (Loaded.Body.Y-26))*Power);
    public void Release(DesktopSheep pet)
    {
        if (Loaded != pet) return;
        var velocity = LaunchVelocity(); Loaded = null; Aiming = false;
        pet.Body.Launch(velocity.X, velocity.Y); pet.GameFlight = true; Invalidate();
    }
    public void Unload()
    {
        if (Loaded is { IsDisposed: false } pet) pet.Body.Drop(pet.Body.X, pet.Body.Y);
        Loaded = null; Aiming = false;
    }
    public void RenderTick() { if (Loaded?.IsDisposed == true) Unload(); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e); if (e.Button != MouseButtons.Left) return;
        if (resetButton.Contains(e.Location)) Reset();
        else if (closeButton.Contains(e.Location)) Close();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float ax = SlingAnchor.X - Left, ay = SlingAnchor.Y - Top;
        using var wood = new Pen(Color.FromArgb(118, 72, 43), 14) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(wood, ax, Height-5, ax, ay+42);
        g.DrawLine(wood, ax, ay+42, ax-24, ay-5); g.DrawLine(wood, ax, ay+42, ax+24, ay-5);
        using var band = new Pen(Color.FromArgb(216, 143, 98), 5);
        var pouch = Loaded == null ? new PointF(ax,ay) : new PointF(Loaded.Body.X-Left,Loaded.Body.Y-26-Top);
        g.DrawLine(band, new PointF(ax-24,ay-5), pouch); g.DrawLine(band,pouch,new PointF(ax+24,ay-5));
        if (Loaded == null)
        {
            using var ring = new Pen(Color.FromArgb(233, 202, 135), 2) { DashStyle = DashStyle.Dot };
            g.DrawEllipse(ring, ax-45, ay-45,90,90);
        }
        if (Aiming && Loaded != null)
        {
            var v = LaunchVelocity();
            using var dots = new SolidBrush(Color.FromArgb(255,233,180));
            for (float t=.08f; t<2.8f; t+=.08f)
            {
                float x=pouch.X+v.X*t, y=pouch.Y+v.Y*t+.5f*DesktopPhysics.Gravity*t*t;
                if(y>Height-26 || x<0 || x>Width) break;
                g.FillEllipse(dots,x-2,y-2,4,4);
            }
        }
        using var fill = new SolidBrush(Color.FromArgb(197,142,76));
        using var edge = new Pen(Color.FromArgb(89,57,36), 2);
        using var grain = new Pen(Color.FromArgb(235,187,114), 2);
        foreach(var b in World.Blocks)
        {
            var r=new RectangleF(b.X-Left,b.Y-Top,SmashBlock.Size,SmashBlock.Size);
            g.FillRectangle(fill,r); g.DrawRectangle(edge,r.X,r.Y,r.Width,r.Height);
            g.DrawLine(grain,r.Left+6,r.Top+7,r.Right-6,r.Top+7); g.DrawLine(grain,r.Left+6,r.Bottom-8,r.Right-6,r.Bottom-8);
            if(b.Health<.7f) { g.DrawLine(edge,r.Left+12,r.Top,r.Left+22,r.Top+20); g.DrawLine(edge,r.Left+22,r.Top+20,r.Left+15,r.Bottom); }
        }
        foreach(var c in World.Chips) g.FillRectangle(fill,c.Position.X-Left,c.Position.Y-Top,5,5);
        // Small opaque control island; the rest of the desktop remains click-through.
        int panelWidth=Math.Min(560,Width-20), panelX=(Width-panelWidth)/2;
        using var panel=new SolidBrush(Color.FromArgb(40,44,52));
        g.FillRectangle(panel,panelX,Height-36,panelWidth,34);
        using var font=new Font("Segoe UI",9);
        string message=World.Broken==World.Total ? "All smashed!" : $"{World.Broken}/{World.Total} · Drag into sling · pull & release";
        g.DrawString(message,font,Brushes.White,panelX+10,Height-27);
        resetButton=new Rectangle(panelX+panelWidth-110,Height-33,65,28);
        closeButton=new Rectangle(panelX+panelWidth-40,Height-33,30,28);
        g.FillRectangle(Brushes.SlateGray,resetButton); g.FillRectangle(Brushes.SlateGray,closeButton);
        g.DrawString("Reset",font,Brushes.White,resetButton.X+9,resetButton.Y+5);
        g.DrawString("×",font,Brushes.White,closeButton.X+8,closeButton.Y+4);
    }
}
