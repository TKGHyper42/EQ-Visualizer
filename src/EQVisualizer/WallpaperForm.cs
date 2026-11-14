using System.Diagnostics;
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
    private Bitmap? softwareFrame;
    private float appliedBlur = -1;
    private string title = "", artist = "";
    private D3D11Renderer? renderer;
    private Vector2 pointer;
    private Vector4 primary = new(.2f, .85f, 1, 1), secondary = new(.7f, .25f, 1, 1);
    private ShadeRegion[] shades = [];
    public long PresentedFrames { get; private set; }
    public string DebugState => !settings.Enabled ? "Desactivado" :
        systemPaused ? "Suspendido" : !Attached ? "Sin enlace con Explorer" :
        !Visible ? "Oculto" : covered ? "Cubierto" :
        timer.Enabled && hasMusicSession ? "Animado" :
        timer.Enabled ? "Reloj" : "Estático";

    public bool Attached => IsHandleCreated && DesktopHost.IsAttached(Handle);
    public Rectangle MonitorBounds => screen.Bounds;
    public bool NeedsAudio => timer.Enabled && hasMusicSession && isPlaying;
    public bool HasMusicSession => hasMusicSession;
    public Image? CopyArtwork() => artwork is null ? null : new Bitmap(artwork);
    public string RendererName => renderer is null ? "Compatibilidad GDI+" : renderer.UsesSoftware ? "Direct3D 11 (software)" : "Direct3D 11";
    public WallpaperForm(Screen screen, Settings settings, AudioLevel audio)
    {
        this.screen = screen; this.settings = settings; this.audio = audio;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopLevel = false; StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds; SetStyle(ControlStyles.Opaque, true);
        timer.Tick += (_, _) => TickFrame();
        if (settings.NoSessionMode != IdleMode.Off) SetNowPlaying(null, false, hasSession: false);
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020; // NOACTIVATE | TOOLWINDOW | TRANSPARENT
            return parameters;
        }
    }
    public void RefreshState(bool repaint = false)
    {
        bool wasCovered = covered;
        covered = DesktopHost.IsCovered(screen.Bounds);
        bool idleClock = !hasMusicSession && settings.NoSessionMode.HasClock();
        bool idleImage = !hasMusicSession && settings.NoSessionMode.HasImage() && artwork is not null;
        bool show = settings.Enabled && !systemPaused && (hasMusicSession ? artwork is not null : idleClock || idleImage) && Attached;
        bool changed = Visible != show;
        if (changed) Visible = show;
        if (show) DesktopHost.KeepBelowIcons(Handle);
        timer.Interval = idleClock ? 15000 : Math.Max(8, 1000 / settings.Fps);
        bool animate = show && !covered && (idleClock || hasMusicSession && isPlaying);
        if (timer.Enabled != animate) { timer.Enabled = animate; lastTime = clock.Elapsed.TotalSeconds; }
        if (show && !covered && (repaint || changed || wasCovered))
        {
            if (idleClock) UpdateClock();
            PresentFrame();
        }
    }
    public void SetSystemPaused(bool paused) { systemPaused = paused; RefreshState(); }
    public void SetPlaybackState(bool playing)
    {
        if (isPlaying == playing) return;
        isPlaying = playing;
        if (!playing) Array.Clear(spectrum);
        RefreshState(true);
    }
    public void SetNowPlaying(Image? image, bool playing, string title = "", string artist = "", bool hasSession = true)
    {
        hasMusicSession = hasSession;
        this.title = title; this.artist = artist;
        clockText = "";
        caption?.Dispose(); caption = null;
        backdrop?.Dispose(); backdrop = null;
        try { renderer?.SetCaption(null); renderer?.SetBackground(null); } catch { DropRenderer(); }
        artwork?.Dispose(); artwork = hasSession ? image is null ? MissingCover() : new Bitmap(image) : LoadIdleImage();
        brightnessSample?.Dispose(); brightnessSample = artwork is null ? null : new Bitmap(artwork, 16, 16);
        if (artwork is not null)
        {
            using var tiny = new Bitmap(artwork, 32, 32); var samples = new Vector3[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { var c = tiny.GetPixel(x, y); samples[y * 32 + x] = new(c.R / 255f, c.G / 255f, c.B / 255f); }
            (primary, secondary) = ArtworkPalette.Create(samples);
        }
        try { renderer?.SetArtwork(artwork); } catch { DropRenderer(); }
        isPlaying = playing;
        if (!playing) Array.Clear(spectrum);
        if (!hasMusicSession) UpdateClock();
        RefreshState(true);
    }
    public void RefreshIdleOptions()
    {
        if (!hasMusicSession) SetNowPlaying(null, false, hasSession: false);
        else
        {
            caption?.Dispose();
            caption = null;
            try { renderer?.SetCaption(null); } catch { DropRenderer(); }
            RefreshState(true);
        }
    }
    private Image? LoadIdleImage()
    {
        return settings.NoSessionMode.HasImage() ? LocalImage.Load(settings.NoSessionImagePath) : null;
    }
    private static Bitmap MissingCover()
    {
        var image = new Bitmap(256, 256);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.FromArgb(26, 27, 32));
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(176, 178, 187));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("Sin portada", font, brush, new RectangleF(0, 0, 256, 256), format);
        return image;
    }
    private bool UpdateClock()
    {
        if (hasMusicSession || !settings.NoSessionMode.HasClock()) return false;
        string next = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + settings.ClockStyle;
        if (next == clockText) return false;
        clockText = next;
        caption?.Dispose();
        var accent = artwork is null ? Ui.Accent : Color.FromArgb(
            (int)(Math.Clamp(primary.X, 0, 1) * 255), (int)(Math.Clamp(primary.Y, 0, 1) * 255), (int)(Math.Clamp(primary.Z, 0, 1) * 255));
        caption = ClockFace.Create(DateTime.Now, settings.ClockStyle, accent);
        try { renderer?.SetCaption(caption); } catch { DropRenderer(); }
        return true;
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
        ConfigureRenderer();
    }
    public void ConfigureRenderer()
    {
        DropRenderer();
        if (settings.UseGdiRenderer || !IsHandleCreated) return;
        try { renderer = new D3D11Renderer(Handle, ClientSize.Width, ClientSize.Height); renderer.SetArtwork(artwork); renderer.SetCaption(caption); renderer.SetBackground(backdrop); } catch { DropRenderer(); }
        if (renderer is not null) DoubleBuffered = false;
    }
    protected override void OnHandleDestroyed(EventArgs e) { DropRenderer(); base.OnHandleDestroyed(e); }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; } // HTTRANSPARENT
        if (message.Msg == 0x0021) { message.Result = new IntPtr(4); return; } // MA_NOACTIVATEANDEAT
        base.WndProc(ref message);
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    private void TickFrame()
    {
        if (!hasMusicSession)
        {
            if (UpdateClock()) PresentFrame();
            return;
        }
        double now = clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Clamp(now - lastTime, 0, .1); lastTime = now;
        if (settings.EnableTunnel) phase = (phase + dt * .4f) % 1;
        var cursor = PointToClient(Cursor.Position);
        var wanted = settings.EnableParallax ? new Vector2((cursor.X - Width / 2f) / Math.Max(1, Width), (cursor.Y - Height / 2f) / Math.Max(1, Height)) * settings.ParallaxStrength * .06f : Vector2.Zero;
        pointer = Vector2.Lerp(pointer, wanted, 1 - MathF.Exp(-dt * 7));
        var input = audio.Bands;
        float smoothing = 1 - MathF.Exp(-dt / Math.Max(.015f, settings.Smoothing));
        for (int i = 0; i < spectrum.Length; i++) spectrum[i] += ((settings.CaptureAudio ? input[i] : 0) - spectrum[i]) * smoothing;
        PresentFrame();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (!settings.Enabled || systemPaused || covered || hasMusicSession && artwork is null) return;
        BuildFrame();
        if (renderer is not null)
        {
            try
            {
                renderer.Resize((int)(Width * settings.RenderScale), (int)(Height * settings.RenderScale));
                renderer.Render(scene.Vertices);
                PresentedFrames++;
                return;
            }
            catch { DropRenderer(); }
        }
        RenderCompatibility(e.Graphics);
        PresentedFrames++;
    }
    private void PresentFrame()
    {
        if (!Visible || systemPaused || covered) return;
        if (renderer is null)
        {
            BuildFrame();
            if (softwareFrame is null || softwareFrame.Width != Width || softwareFrame.Height != Height)
            {
                softwareFrame?.Dispose(); softwareFrame = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            }
            using (var buffer = Graphics.FromImage(softwareFrame)) RenderCompatibility(buffer);
            using (var target = Graphics.FromHwnd(Handle)) target.DrawImageUnscaled(softwareFrame, 0, 0);
            PresentedFrames++;
            return;
        }
        try
        {
            BuildFrame();
            renderer.Resize((int)(Width * settings.RenderScale), (int)(Height * settings.RenderScale));
            renderer.Render(scene.Vertices);
            PresentedFrames++;
        }
        catch { DropRenderer(); Invalidate(); }
    }
    private void BuildFrame(bool still = false)
    {
        if (artwork is not null && (backdrop is null || appliedBlur != settings.BackgroundBlur))
        {
            backdrop?.Dispose(); backdrop = BackdropImage.Create(artwork, settings.BackgroundBlur); appliedBlur = settings.BackgroundBlur;
            try { renderer?.SetBackground(backdrop); } catch { DropRenderer(); }
        }
        if (hasMusicSession && settings.ShowTrackInfo && caption is null && !(string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist)))
        {
            caption = TrackCaption.Create(title, artist, settings.CaptionOpacity, settings.CaptionTextShadow);
            try { renderer?.SetCaption(caption); } catch { DropRenderer(); }
        }
        var region = settings.ContrastMode == ContrastMode.WholeImage ? new[] { new ShadeRegion(0, 0, Width, Height) } : settings.ContrastMode == ContrastMode.Adaptive ? shades : [];
        if (!hasMusicSession)
            scene.BuildIdle(Width, Height, settings.CoverSize, new(settings.PositionX, settings.PositionY),
                settings.NoSessionMode.HasImage() && artwork is not null, settings.NoSessionMode.HasClock() && caption is not null, settings.ClockStyle, region);
        else
        {
            float bottomY = Math.Clamp((screen.WorkingArea.Bottom - screen.Bounds.Top) / (float)Math.Max(1, Height) - settings.BottomMargin, .2f, .98f);
            var offset = still ? Vector2.Zero : pointer;
            scene.Build(new(Width, Height, still ? 0 : phase, new(settings.PositionX + offset.X, settings.PositionY + offset.Y),
                settings.CoverSize, settings.Strength, settings.Sensitivity, settings.EnableTunnel,
                settings.VisualizerMode.HasCircle(),
                settings.VisualizerMode.HasBottom(), bottomY, settings.QualityLayers, primary, secondary, settings.BarThickness,
                settings.ShowTrackInfo && caption is not null, settings.CircleBarHeight, settings.BottomBarHeight,
                 new BarAppearance(settings.CircleBarHeight, settings.CircleBarThickness, settings.CircleBarOpacity, settings.CircleBarRounded),
                 new BarAppearance(settings.BottomBarHeight, settings.BottomBarThickness, settings.BottomBarOpacity, settings.BottomBarRounded),
                 settings.BottomBarWidth, settings.Gradient, settings.GradientStrength),
                still ? [] : spectrum, region);
        }
    }
    public Task<Bitmap?> CreateWindowsWallpaperAsync()
    {
        if (artwork is null || !hasMusicSession || Width < 1 || Height < 1)
            return Task.FromResult<Bitmap?>(null);
        BuildFrame(still: true);
        var bounds = ClientRectangle;
        var vertices = scene.Vertices.ToArray();
        var coverCopy = new Bitmap(artwork);
        var backdropCopy = backdrop is null ? null : new Bitmap(backdrop);
        var captionCopy = caption is null ? null : new Bitmap(caption);
        return Task.Run<Bitmap?>(() =>
        {
            using (coverCopy)
            using (backdropCopy)
            using (captionCopy)
            {
                var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb);
                try
                {
                    using var graphics = Graphics.FromImage(image);
                    graphics.Clear(Color.FromArgb(8, 8, 12));
                    SoftwareSceneRenderer.Draw(graphics, bounds, vertices, coverCopy, backdropCopy, captionCopy);
                    return image;
                }
                catch { image.Dispose(); throw; }
            }
        });
    }
    private void RenderCompatibility(Graphics g)
    {
        g.Clear(Color.FromArgb(8, 8, 12));
        SoftwareSceneRenderer.Draw(g, ClientRectangle, scene.Vertices, artwork, backdrop, caption);
    }
    private void DropRenderer() { renderer?.Dispose(); renderer = null; DoubleBuffered = true; }
    private static Vector4 ToVector(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, 1);
    protected override void Dispose(bool disposing)
    { if (disposing) { timer.Dispose(); artwork?.Dispose(); brightnessSample?.Dispose(); caption?.Dispose(); backdrop?.Dispose(); softwareFrame?.Dispose(); DropRenderer(); } base.Dispose(disposing); }
}
