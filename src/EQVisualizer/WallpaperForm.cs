using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
namespace EQVisualizer;

internal sealed class WallpaperForm : Form
{
    private readonly Settings settings;
    private readonly AudioLevel audio;
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly Screen screen;
    private readonly VisualScene scene = new();
    private readonly float[] spectrum = new float[64];
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double lastTime;
    private float phase;
    private bool systemPaused, covered, isPlaying, hasMusicSession;
    private string clockText = "";
    private Image? artwork;
    private Bitmap? brightnessSample;
    private Bitmap? caption;
    private Bitmap? backdrop;
    private float appliedBlur = -1;
    private string title = "", artist = "";
    private D3D11Renderer? renderer;
    private Vector2 pointer;
    private Vector4 primary = new(.2f, .85f, 1, 1), secondary = new(.7f, .25f, 1, 1);
    private ShadeRegion[] shades = [];
    public bool Attached => IsHandleCreated && DesktopHost.IsAttached(Handle);
    public Rectangle MonitorBounds => screen.Bounds;
    public bool NeedsAudio => timer.Enabled;
    public Image? CopyArtwork() => artwork is null ? null : new Bitmap(artwork);
    public string RendererName => renderer is null ? "Compatibilidad GDI+" : renderer.UsesSoftware ? "Direct3D 11 (software)" : "Direct3D 11";
    public WallpaperForm(Screen screen, Settings settings, AudioLevel audio)
    {
        this.screen = screen; this.settings = settings; this.audio = audio;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds; SetStyle(ControlStyles.Opaque, true);
        timer.Tick += (_, _) => TickFrame();
        if (settings.NoSessionMode != IdleMode.Off) SetNowPlaying(null, false, hasSession: false);
    }
    protected override bool ShowWithoutActivation => true;
    public void SetAttached() { }
    public void RefreshState(bool repaint = false)
    {
        covered = DesktopHost.IsCovered(screen.Bounds);
        bool idleClock = !hasMusicSession && settings.NoSessionMode.HasClock();
        bool idleImage = !hasMusicSession && settings.NoSessionMode.HasImage() && artwork is not null;
        bool show = settings.Enabled && !systemPaused && (hasMusicSession ? artwork is not null : idleClock || idleImage) && Attached;
        bool changed = Visible != show;
        if (changed) Visible = show;
        timer.Interval = idleClock ? 15000 : Math.Max(8, 1000 / settings.Fps);
        bool animate = show && !covered && (idleClock || hasMusicSession && isPlaying);
        if (timer.Enabled != animate) { timer.Enabled = animate; lastTime = clock.Elapsed.TotalSeconds; }
        if (show && !covered && (repaint || changed)) Invalidate();
    }
    public void SetSystemPaused(bool paused) { systemPaused = paused; RefreshState(); }
    public void SetNowPlaying(Image? image, bool playing, string title = "", string artist = "", bool hasSession = true)
    {
        hasMusicSession = hasSession;
        this.title = title; this.artist = artist;
        clockText = "";
        caption?.Dispose(); caption = null;
        backdrop?.Dispose(); backdrop = null;
        try { renderer?.SetCaption(null); renderer?.SetBackground(null); } catch { DropRenderer(); }
        artwork?.Dispose(); artwork = hasSession ? image is null ? null : new Bitmap(image) : LoadIdleImage();
        brightnessSample?.Dispose(); brightnessSample = artwork is null ? null : new Bitmap(artwork, 16, 16);
        if (artwork is not null)
        {
            using var tiny = new Bitmap(artwork, 32, 32); var samples = new Vector3[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { var c = tiny.GetPixel(x, y); samples[y * 32 + x] = new(c.R / 255f, c.G / 255f, c.B / 255f); }
            (primary, secondary) = ArtworkPalette.Create(samples);
        }
        try { renderer?.SetArtwork(artwork); } catch { DropRenderer(); }
        isPlaying = playing;
        if (!hasMusicSession) UpdateClock();
        RefreshState(true);
    }
    public void RefreshIdleOptions()
    {
        if (!hasMusicSession) SetNowPlaying(null, false, hasSession: false);
        else RefreshState(true);
    }
    private Image? LoadIdleImage()
    {
        return settings.NoSessionMode.HasImage() ? LocalImage.Load(settings.NoSessionImagePath) : null;
    }
    private void UpdateClock()
    {
        if (hasMusicSession || !settings.NoSessionMode.HasClock()) return;
        string next = DateTime.Now.ToString("HH:mm | dddd d MMMM", System.Globalization.CultureInfo.CurrentCulture);
        if (next == clockText) return;
        clockText = next;
        caption?.Dispose();
        caption = TrackCaption.Create(DateTime.Now.ToString("HH:mm"), DateTime.Now.ToString("dddd, d MMMM", System.Globalization.CultureInfo.CurrentCulture));
        try { renderer?.SetCaption(caption); } catch { DropRenderer(); }
    }
    public void SetShades(Rectangle[] rectangles)
    {
        var next = rectangles.Where(r => r.IntersectsWith(screen.Bounds)).Select(r =>
        {
            float luminance = .5f;
            if (brightnessSample is not null)
            {
                float square = Math.Max(Width, Height) * 1.12f;
                int x = Math.Clamp((int)(((r.X - screen.Bounds.X - Width / 2f) / square + .5f) * 16), 0, 15);
                int y = Math.Clamp((int)(((r.Y - screen.Bounds.Y - Height / 2f) / square + .5f) * 16), 0, 15);
                var c = brightnessSample.GetPixel(x, y); luminance = (.2126f * c.R + .7152f * c.G + .0722f * c.B) / 255;
            }
            return new ShadeRegion(r.X - screen.Bounds.X, r.Y - screen.Bounds.Y, r.Width, r.Height, .48f + luminance * .32f);
        }).ToArray();
        if (!shades.SequenceEqual(next)) { shades = next; if (Visible && !covered) Invalidate(); }
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { renderer = new D3D11Renderer(Handle, ClientSize.Width, ClientSize.Height); renderer.SetArtwork(artwork); renderer.SetCaption(caption); renderer.SetBackground(backdrop); } catch { DropRenderer(); }
    }
    protected override void OnHandleDestroyed(EventArgs e) { DropRenderer(); base.OnHandleDestroyed(e); }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    private void TickFrame()
    {
        if (!hasMusicSession) { UpdateClock(); Invalidate(); return; }
        double now = clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Clamp(now - lastTime, 0, .1); lastTime = now;
        if (settings.EnableTunnel) phase += dt * .16f * settings.Strength;
        var cursor = PointToClient(Cursor.Position);
        var wanted = settings.EnableParallax ? new Vector2((cursor.X - Width / 2f) / Math.Max(1, Width), (cursor.Y - Height / 2f) / Math.Max(1, Height)) * settings.ParallaxStrength * .06f : Vector2.Zero;
        pointer = Vector2.Lerp(pointer, wanted, 1 - MathF.Exp(-dt * 7));
        var input = settings.CaptureAudio ? audio.Bands : new float[64];
        float smoothing = 1 - MathF.Exp(-dt / Math.Max(.015f, settings.Smoothing));
        for (int i = 0; i < 64; i++) spectrum[i] += (input[i] - spectrum[i]) * smoothing;
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (!settings.Enabled || systemPaused || covered || hasMusicSession && artwork is null) return;
        if (artwork is not null && (backdrop is null || appliedBlur != settings.BackgroundBlur))
        {
            backdrop?.Dispose(); backdrop = BackdropImage.Create(artwork, settings.BackgroundBlur); appliedBlur = settings.BackgroundBlur;
            try { renderer?.SetBackground(backdrop); } catch { DropRenderer(); }
        }
        if (hasMusicSession && settings.ShowTrackInfo && caption is null && !(string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist)))
        {
            caption = TrackCaption.Create(title, artist);
            try { renderer?.SetCaption(caption); } catch { DropRenderer(); }
        }
        var region = settings.ContrastMode == ContrastMode.WholeImage ? new[] { new ShadeRegion(0, 0, Width, Height) } : settings.ContrastMode == ContrastMode.Adaptive ? shades : [];
        if (!hasMusicSession)
            scene.BuildIdle(Width, Height, settings.CoverSize, new(settings.PositionX, settings.PositionY),
                settings.NoSessionMode.HasImage() && artwork is not null, settings.NoSessionMode.HasClock() && caption is not null, region);
        else
        {
        float bottomY = Math.Clamp((screen.WorkingArea.Bottom - screen.Bounds.Top) / (float)Math.Max(1, Height) - settings.BottomMargin, .2f, .98f);
        scene.Build(new(Width, Height, phase, new(settings.PositionX + pointer.X, settings.PositionY + pointer.Y),
            settings.CoverSize, settings.Strength, settings.Sensitivity, settings.EnableTunnel,
            settings.VisualizerMode.HasCircle(),
            settings.VisualizerMode.HasBottom(), bottomY, settings.QualityLayers, primary, secondary, settings.BarThickness, settings.ShowTrackInfo && caption is not null), spectrum, region);
        }
        if (renderer is not null)
        {
            try
            {
                renderer.Resize((int)(Width * settings.RenderScale), (int)(Height * settings.RenderScale));
                renderer.Render(scene.Vertices); return;
            }
            catch { DropRenderer(); }
        }
        RenderCompatibility(e.Graphics);
    }
    private void RenderCompatibility(Graphics g)
    {
        g.Clear(Color.FromArgb(8, 8, 12)); g.InterpolationMode = InterpolationMode.Bilinear;
        var vertices = scene.Vertices;
        PointF Pixel(Vector2 p) => new((p.X + 1) * Width / 2, (1 - p.Y) * Height / 2);
        for (int i = 0; i + 5 < vertices.Length; i += 6)
        {
            var a = vertices[i]; var b = vertices[i + 1]; var c = vertices[i + 2]; var d = vertices[i + 5];
            if (a.Textured > .5f)
            {
                var image = a.Textured > 2.5f ? backdrop : a.Textured > 1.5f ? caption : artwork;
                if (image is null) continue;
                using var attr = new ImageAttributes(); attr.SetColorMatrix(new ColorMatrix
                { Matrix00 = a.Color.X, Matrix11 = a.Color.Y, Matrix22 = a.Color.Z, Matrix33 = a.Color.W, Matrix44 = 1 });
                g.DrawImage(image, [Pixel(a.Position), Pixel(b.Position), Pixel(d.Position)], new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, attr);
            }
            else
            {
                var v = Vector4.Clamp(a.Color, Vector4.Zero, Vector4.One);
                Color ColorOf(Vector4 color) => Color.FromArgb((int)(color.W * 255), (int)(color.X * 255), (int)(color.Y * 255), (int)(color.Z * 255));
                var p1 = Pixel((a.Position + b.Position) / 2); var p2 = Pixel((c.Position + d.Position) / 2);
                using Brush brush = a.Color == c.Color ? new SolidBrush(ColorOf(v)) : new LinearGradientBrush(p1, p2, ColorOf(v), ColorOf(Vector4.Clamp(c.Color, Vector4.Zero, Vector4.One)));
                g.FillPolygon(brush, [Pixel(a.Position), Pixel(b.Position), Pixel(c.Position), Pixel(d.Position)]);
            }
        }
    }
    private void DropRenderer() { renderer?.Dispose(); renderer = null; DoubleBuffered = true; }
    private static Vector4 ToVector(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, 1);
    protected override void Dispose(bool disposing)
    { if (disposing) { timer.Dispose(); artwork?.Dispose(); brightnessSample?.Dispose(); caption?.Dispose(); backdrop?.Dispose(); DropRenderer(); } base.Dispose(disposing); }
}
