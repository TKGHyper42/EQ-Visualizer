using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace EQVisualizer;

internal readonly record struct PreviewOptions(VisualizerMode Mode, float CoverSize, float X, float Y,
    float Margin, float Strength, float Sensitivity, float Thickness, int Layers, bool Tunnel,
    bool TrackInfo, float Blur, Color Accent, ContrastMode Contrast, IdleMode IdleMode,
    bool PreviewIdle, string? IdlePath, InterfaceTheme Theme, IdleClockStyle ClockStyle);

// Uses the same scene geometry as the wallpaper. The spectrum is deliberately simulated.
internal sealed class SettingsPreview : Control
{
    private readonly VisualScene scene = new();
    private readonly Image artwork;
    private readonly bool sampleArtwork;
    private readonly float[] spectrum = new float[64];
    private readonly Bitmap caption = TrackCaption.Create("Vista previa", "EQ Visualizer")!;
    private Bitmap? clockCaption;
    private IdleClockStyle cachedClockStyle = (IdleClockStyle)(-1);
    private Color cachedClockAccent;
    private Image? idleArtwork;
    private string? cachedIdlePath;
    private Bitmap? backdrop;
    private float cachedBlur = -1;
    private PreviewOptions options;
    private Vector4 primary, secondary;

    public SettingsPreview(Image? currentArtwork)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        artwork = currentArtwork ?? SampleArtwork();
        sampleArtwork = currentArtwork is null;
        using var sample = new Bitmap(artwork, 32, 32);
        var colors = new Vector3[1024];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            var c = sample.GetPixel(x, y); colors[y * 32 + x] = new(c.R / 255f, c.G / 255f, c.B / 255f);
        }
        (primary, secondary) = ArtworkPalette.Create(colors);
        for (int i = 0; i < spectrum.Length; i++)
            spectrum[i] = .18f + .48f * MathF.Pow(MathF.Sin(i * .38f) * MathF.Cos(i * .11f), 2);
        AccessibleName = "Vista previa del wallpaper con señal de audio simulada";
    }

    public void SetOptions(PreviewOptions value)
    {
        if (cachedIdlePath != value.IdlePath)
        {
            idleArtwork?.Dispose(); idleArtwork = LoadImage(value.IdlePath); cachedIdlePath = value.IdlePath;
        }
        if (cachedBlur != value.Blur || options.PreviewIdle != value.PreviewIdle ||
            options.IdlePath != value.IdlePath) { backdrop?.Dispose(); backdrop = null; cachedBlur = -1; }
        if (clockCaption is null || cachedClockStyle != value.ClockStyle || cachedClockAccent != value.Accent)
        {
            clockCaption?.Dispose(); clockCaption = ClockFace.Create(DateTime.Now, value.ClockStyle, value.Accent);
            cachedClockStyle = value.ClockStyle; cachedClockAccent = value.Accent;
        }
        options = value; Invalidate();
    }

    public static bool CanLoadImage(string? path)
    {
        using var image = LoadImage(path);
        return image is not null;
    }
    private static Image? LoadImage(string? path)
    {
        return LocalImage.Load(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var palette = InterfacePalettes.For(options.Theme);
        g.Clear(Parent?.BackColor ?? Ui.Background);
        var frame = new Rectangle(1, 1, Width - 3, Height - 3);
        if (frame.Width < 20 || frame.Height < 60) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var border = Ui.Round(frame, 15))
        using (var fill = new SolidBrush(palette.Surface))
        using (var edge = new Pen(palette.Line)) { g.FillPath(fill, border); g.DrawPath(edge, border); }
        using (var dot = new SolidBrush(options.Accent)) g.FillEllipse(dot, 17, 15, 8, 8);
        TextRenderer.DrawText(g, "VISTA PREVIA", Ui.Font(8.5f, FontStyle.Bold), new Rectangle(31, 10, 160, 20), palette.Text);
        string detail = options.PreviewIdle ? "Modo sin música · sin audio" :
            sampleArtwork ? "Portada de muestra · señal simulada" : "Portada actual · señal simulada";
        TextRenderer.DrawText(g, detail,
            Ui.Font(8), new Rectangle(190, 10, Math.Max(10, Width - 205), 20), palette.Muted,
            TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
        var canvas = new Rectangle(12, 37, Width - 26, Height - 50);
        if (canvas.Width < 10 || canvas.Height < 10) return;
        g.SetClip(canvas);
        Image selectedArtwork = options.PreviewIdle && idleArtwork is not null ? idleArtwork : artwork;
        bool idleImage = options.PreviewIdle && options.IdleMode.HasImage() && idleArtwork is not null;
        if ((idleImage || !options.PreviewIdle) && (backdrop is null || cachedBlur != options.Blur))
        {
            backdrop?.Dispose(); backdrop = BackdropImage.Create(selectedArtwork, options.Blur); cachedBlur = options.Blur;
        }
        if (options.PreviewIdle)
            scene.BuildIdle(canvas.Width, canvas.Height, options.CoverSize, new(options.X, options.Y),
                idleImage, options.IdleMode.HasClock(), options.ClockStyle);
        else
        {
            float bottom = Math.Clamp(.96f - options.Margin, .2f, .98f);
            var shades = options.Contrast == ContrastMode.WholeImage
                ? new[] { new ShadeRegion(0, 0, canvas.Width, canvas.Height, .35f) } : [];
            scene.Build(new SceneOptions(canvas.Width, canvas.Height, .3f, new(options.X, options.Y),
                options.CoverSize, options.Strength, options.Sensitivity, options.Tunnel,
                options.Mode.HasCircle(), options.Mode.HasBottom(), bottom, options.Layers,
                primary, secondary, options.Thickness, options.TrackInfo), spectrum, shades);
        }
        DrawScene(g, canvas, selectedArtwork);
        g.ResetClip();
    }

    private void DrawScene(Graphics g, Rectangle canvas, Image selectedArtwork)
    {
        g.InterpolationMode = InterpolationMode.Bilinear;
        PointF Pixel(Vector2 p) => new(canvas.X + (p.X + 1) * canvas.Width / 2, canvas.Y + (1 - p.Y) * canvas.Height / 2);
        var vertices = scene.Vertices;
        for (int i = 0; i + 5 < vertices.Length; i += 6)
        {
            var a = vertices[i]; var b = vertices[i + 1]; var c = vertices[i + 2]; var d = vertices[i + 5];
            if (a.Textured > .5f)
            {
                Image? image = a.Textured > 2.5f ? backdrop : a.Textured > 1.5f ? options.PreviewIdle ? clockCaption : caption : selectedArtwork;
                if (image is null) continue;
                using var attr = new ImageAttributes();
                attr.SetColorMatrix(new ColorMatrix { Matrix00 = a.Color.X, Matrix11 = a.Color.Y,
                    Matrix22 = a.Color.Z, Matrix33 = a.Color.W, Matrix44 = 1 });
                g.DrawImage(image, [Pixel(a.Position), Pixel(b.Position), Pixel(d.Position)],
                    new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, attr);
            }
            else
            {
                static Color Ink(Vector4 color)
                {
                    var v = Vector4.Clamp(color, Vector4.Zero, Vector4.One);
                    return Color.FromArgb((int)(v.W * 255), (int)(v.X * 255), (int)(v.Y * 255), (int)(v.Z * 255));
                }
                var p1 = Pixel((a.Position + b.Position) / 2); var p2 = Pixel((c.Position + d.Position) / 2);
                using Brush brush = a.Color == c.Color ? new SolidBrush(Ink(a.Color)) :
                    new LinearGradientBrush(p1, p2, Ink(a.Color), Ink(c.Color));
                g.FillPolygon(brush, [Pixel(a.Position), Pixel(b.Position), Pixel(c.Position), Pixel(d.Position)]);
            }
        }
    }

    private static Bitmap SampleArtwork()
    {
        var image = new Bitmap(256, 256);
        using var g = Graphics.FromImage(image);
        using var background = new LinearGradientBrush(new Rectangle(0, 0, 256, 256),
            Color.FromArgb(14, 39, 81), Color.FromArgb(127, 46, 164), 45);
        g.FillRectangle(background, 0, 0, 256, 256);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var glow = new SolidBrush(Color.FromArgb(70, 99, 235, 224));
        g.FillEllipse(glow, 27, 38, 210, 210);
        using var tile = new SolidBrush(Color.FromArgb(45, 225, 243, 250));
        using var shape = Ui.Round(new RectangleF(62, 60, 132, 132), 27);
        g.FillPath(tile, shape);
        return image;
    }

    protected override void Dispose(bool disposing)
    { if (disposing) { artwork.Dispose(); idleArtwork?.Dispose(); backdrop?.Dispose(); caption.Dispose(); clockCaption?.Dispose(); } base.Dispose(disposing); }
}
