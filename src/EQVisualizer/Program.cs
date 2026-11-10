using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.Wave;
using NAudio.CoreAudioApi;

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
    public string? AudioDeviceId { get; set; }
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
        var deviceMenu = new ToolStripMenuItem("Salida de audio");
        deviceMenu.DropDownItems.Add("Predeterminada", null, (_, _) => SetDevice(null));
        try
        {
            using var endpoints = new MMDeviceEnumerator();
            foreach (var device in endpoints.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                string id = device.ID;
                deviceMenu.DropDownItems.Add(device.FriendlyName, null, (_, _) => SetDevice(id));
            }
        }
        catch { deviceMenu.Enabled = false; }
        menu.Items.Add(deviceMenu);
        menu.Items.Add("Salir", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "EQ Visualizer (prototipo)", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Toggle();
        try { audio.Start(settings.AudioDeviceId); }
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
    private void SetDevice(string? id)
    {
        string? previous = settings.AudioDeviceId;
        try { audio.Start(id); settings.AudioDeviceId = id; Save(); }
        catch (Exception e)
        {
            tray.ShowBalloonTip(4000, "EQ Visualizer", "Dispositivo no disponible: " + e.Message, ToolTipIcon.Warning);
            try { audio.Start(previous); } catch { }
        }
    }
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
    private float[] bands = new float[64];
    public float Level => level;
    public float[] Bands => Volatile.Read(ref bands);

    public void Start(string? deviceId)
    {
        capture?.StopRecording();
        capture?.Dispose();
        level = 0;
        Volatile.Write(ref bands, new float[64]);
        using var endpoints = new MMDeviceEnumerator();
        var source = deviceId is null ? new WasapiLoopbackCapture() : new WasapiLoopbackCapture(endpoints.GetDevice(deviceId));
        capture = source;
        source.DataAvailable += (_, e) =>
        {
            var format = source.WaveFormat;
            const int fftSize = 1024;
            var fft = new NAudio.Dsp.Complex[fftSize];
            double sum = 0;
            int count = 0;
            int stride = format.BitsPerSample / 8;
            if (stride != 2 && stride != 4) return;
            int channels = Math.Max(1, format.Channels);
            int frames = Math.Min(fftSize, e.BytesRecorded / (stride * channels));
            for (int frame = 0; frame < frames; frame++)
            {
                int offset = frame * stride * channels;
                float sample = stride == 4
                    ? BitConverter.ToSingle(e.Buffer, offset)
                    : BitConverter.ToInt16(e.Buffer, offset) / 32768f;
                if (!float.IsFinite(sample)) sample = 0;
                sum += sample * sample;
                count++;
                float window = .5f * (1 - MathF.Cos(2 * MathF.PI * frame / (fftSize - 1)));
                fft[frame].X = sample * window;
                fft[frame].Y = 0;
            }
            level = count == 0 ? 0 : Math.Clamp((float)Math.Sqrt(sum / count) * 5f, 0, 1);
            if (frames < fftSize) return;
            NAudio.Dsp.FastFourierTransform.FFT(true, 10, fft);
            var next = new float[64];
            for (int band = 0; band < next.Length; band++)
            {
                int lo = Math.Max(1, (int)MathF.Pow(512, band / 64f));
                int hi = Math.Min(512, Math.Max(lo + 1, (int)MathF.Pow(512, (band + 1) / 64f)));
                float peak = 0;
                for (int bin = lo; bin < hi; bin++)
                {
                    float magnitude = MathF.Sqrt(fft[bin].X * fft[bin].X + fft[bin].Y * fft[bin].Y);
                    peak = Math.Max(peak, magnitude);
                }
                next[band] = Math.Clamp(peak / 12f, 0, 1);
            }
            Volatile.Write(ref bands, next);
        };
        source.StartRecording();
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
        var spectrum = audio.Bands;
        float bandY = Height - Math.Max(30, Height * .08f);
        float bandW = Width * .7f, x0 = (Width - bandW) / 2;
        using var brush = new SolidBrush(Color.FromArgb(170, 53, 218, 255));
        for (int i = 0; i < bars; i++)
        {
            float h = 3 + spectrum[i] * settings.Strength * Math.Min(100, Height * .15f);
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
    private const long WS_POPUP = 0x80000000L;
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
        if (IntPtr.Size == 4) SetWindowLong32(hwnd, GWL_STYLE, (int)((GetWindowLong32(hwnd, GWL_STYLE) | WS_CHILD) & ~WS_POPUP));
        else SetWindowLongPtr(hwnd, GWL_STYLE, (GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64() | WS_CHILD) & ~WS_POPUP);
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
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, long value);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    private struct RECT { public int Left, Top, Right, Bottom; }
}
