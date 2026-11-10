using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.Wave;

namespace EQVisualizer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new WallpaperContext());
    }
}

internal sealed class Settings
{
    public int Fps { get; set; } = 45;
    public float Strength { get; set; } = 1f;
    public bool Enabled { get; set; } = true;
}

internal sealed class WallpaperContext : ApplicationContext
{
    private readonly List<WallpaperForm> forms = [];
    private readonly NotifyIcon tray;
    private readonly AudioLevel audio = new();
    private readonly Settings settings;
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EQVisualizer", "settings.json");

    public WallpaperContext()
    {
        try { settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)) ?? new(); }
        catch { settings = new(); }
        settings.Fps = Math.Clamp(settings.Fps, 1, 120);
        settings.Strength = Math.Clamp(settings.Strength, 0, 3);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Activar / desactivar", null, (_, _) => Toggle());
        menu.Items.Add("30 FPS", null, (_, _) => SetFps(30));
        menu.Items.Add("45 FPS", null, (_, _) => SetFps(45));
        menu.Items.Add("60 FPS", null, (_, _) => SetFps(60));
        menu.Items.Add("Menos intensidad", null, (_, _) => SetStrength(settings.Strength - .25f));
        menu.Items.Add("Más intensidad", null, (_, _) => SetStrength(settings.Strength + .25f));
        menu.Items.Add("Salir", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "EQ Visualizer (prototipo)", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Toggle();
        try { audio.Start(); }
        catch (Exception e) { tray.ShowBalloonTip(4000, "EQ Visualizer", "Captura de audio no disponible: " + e.Message, ToolTipIcon.Warning); }
        foreach (var screen in Screen.AllScreens)
        {
            var form = new WallpaperForm(screen, settings, audio);
            forms.Add(form);
            form.Show();
            if (!DesktopHost.Attach(form.Handle))
                tray.ShowBalloonTip(5000, "EQ Visualizer", "No se pudo integrar detrás de los iconos. Cerrando.", ToolTipIcon.Error);
        }
        if (forms.Count == 0 || forms.Any(f => !f.Attached)) ExitThread();
    }

    private void Toggle() { settings.Enabled = !settings.Enabled; Save(); foreach (var f in forms) f.RefreshState(); }
    private void SetFps(int fps) { settings.Fps = fps; Save(); foreach (var f in forms) f.RefreshState(); }
    private void SetStrength(float strength) { settings.Strength = Math.Clamp(strength, 0, 3); Save(); }
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings));
    }
    protected override void ExitThreadCore()
    {
        foreach (var f in forms) { f.Close(); f.Dispose(); }
        audio.Dispose();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}

internal sealed class AudioLevel : IDisposable
{
    private WasapiLoopbackCapture? capture;
    private volatile float level;
    public float Level => level;
    public void Start()
    {
        capture = new WasapiLoopbackCapture();
        capture.DataAvailable += (_, e) =>
        {
            // WASAPI loopback is normally 32-bit float PCM; handle 16-bit PCM too.
            var format = capture.WaveFormat;
            double sum = 0;
            int count = 0;
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                for (int i = 0; i + 4 <= e.BytesRecorded; i += 4)
                {
                    float sample = BitConverter.ToSingle(e.Buffer, i);
                    if (float.IsFinite(sample)) { sum += sample * sample; count++; }
                }
            }
            else if (format.BitsPerSample == 16)
            {
                for (int i = 0; i + 2 <= e.BytesRecorded; i += 2)
                {
                    float sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
                    sum += sample * sample; count++;
                }
            }
            level = count == 0 ? 0 : Math.Clamp((float)Math.Sqrt(sum / count) * 5f, 0, 1);
        };
        capture.StartRecording();
    }
    public void Dispose() { capture?.StopRecording(); capture?.Dispose(); }
}

internal sealed class WallpaperForm : Form
{
    private readonly Settings settings;
    private readonly AudioLevel audio;
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly Screen screen;
    private float phase;
    public bool Attached { get; private set; }
    public WallpaperForm(Screen screen, Settings settings, AudioLevel audio)
    {
        this.screen = screen; this.settings = settings; this.audio = audio;
        Bounds = screen.Bounds; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
        timer.Tick += (_, _) =>
        {
            bool visible = settings.Enabled && !DesktopHost.IsCovered(screen.Bounds);
            if (!visible) return;
            phase += .015f;
            Invalidate();
        };
        RefreshState();
    }
    public void RefreshState()
    {
        timer.Interval = Math.Max(8, 1000 / settings.Fps);
        timer.Enabled = settings.Enabled;
        if (!settings.Enabled) Invalidate();
    }
    public void SetAttached() => Attached = true;
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(9, 12, 24));
        if (!settings.Enabled) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float cx = Width / 2f, cy = Height / 2f;
        float unit = Math.Min(Width, Height);
        float energy = audio.Level * settings.Strength;
        for (int i = 14; i >= 0; i--)
        {
            float depth = ((i / 15f + phase) % 1f);
            float size = unit * (.08f + depth * .72f);
            int alpha = (int)(90 * (1 - depth));
            using var pen = new Pen(Color.FromArgb(alpha, 35 + (int)(energy * 160), 130, 230), 2 + energy * 12);
            g.DrawRectangle(pen, cx - size / 2, cy - size / 2, size, size);
        }
        int bars = 64;
        float bandY = Height - Math.Max(30, Height * .08f);
        float bandW = Width * .7f, x0 = (Width - bandW) / 2;
        using var brush = new SolidBrush(Color.FromArgb(170, 53, 218, 255));
        for (int i = 0; i < bars; i++)
        {
            float wave = .2f + .8f * Math.Abs(MathF.Sin(i * .31f + phase * 4));
            float h = 3 + energy * wave * Math.Min(100, Height * .15f);
            g.FillRectangle(brush, x0 + i * bandW / bars, bandY - h, bandW / bars * .55f, h);
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}

internal static class DesktopHost
{
    private const uint SMTO_NORMAL = 0;
    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000;
    private const long WS_POPUP = unchecked((int)0x80000000);
    private static IntPtr worker;
    public static bool Attach(IntPtr hwnd)
    {
        if (worker == IntPtr.Zero)
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return false;
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SMTO_NORMAL, 1000, out _);
            EnumWindows((top, _) =>
            {
                if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }
        if (worker == IntPtr.Zero) return false;
        SetWindowLongPtr(hwnd, GWL_STYLE, (GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64() | WS_CHILD) & ~WS_POPUP);
        if (SetParent(hwnd, worker) == IntPtr.Zero && Marshal.GetLastWin32Error() != 0) return false;
        var form = Control.FromHandle(hwnd) as WallpaperForm;
        if (form is null) return false;
        form.SetAttached();
        return true;
    }
    public static bool IsCovered(Rectangle bounds)
    {
        IntPtr front = GetForegroundWindow();
        if (front == IntPtr.Zero || !GetWindowRect(front, out var r) || IsIconic(front)) return false;
        var overlap = Rectangle.Intersect(bounds, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
        return overlap.Width >= bounds.Width * .95 && overlap.Height >= bounds.Height * .95;
    }
    private delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? name);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, long value);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    private struct RECT { public int Left, Top, Right, Bottom; }
}
