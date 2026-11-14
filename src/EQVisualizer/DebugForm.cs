using System.Diagnostics;

namespace EQVisualizer;

internal sealed class DebugForm : ModernForm
{
    private readonly IReadOnlyList<WallpaperForm> wallpapers;
    private readonly Settings settings;
    private readonly Process process = Process.GetCurrentProcess();
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 1000 };
    private readonly Dictionary<WallpaperForm, long> previousFrames = [];
    private readonly Label metrics = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        Padding = new Padding(18, 14, 18, 14),
        Font = Ui.Font(11)
    };
    private long previousTime;
    private TimeSpan previousCpu;

    public DebugForm(IReadOnlyList<WallpaperForm> wallpapers, Settings settings)
        : base("EQ Visualizer / Debug", windowControls: true)
    {
        this.wallpapers = wallpapers;
        this.settings = settings;
        ShowInTaskbar = true;
        ClientSize = new Size(620, 290);
        MinimumSize = new Size(460, 240);
        metrics.Text = "Midiendo rendimiento…";
        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        content.Controls.Add(Ui.BindColors(metrics, foreground: static () => Ui.Text));
        Controls.Add(content);
        Chrome.BringToFront();
        refresh.Tick += (_, _) => Sample();
        Shown += (_, _) =>
        {
            previousTime = Stopwatch.GetTimestamp();
            previousCpu = process.TotalProcessorTime;
            foreach (var wallpaper in wallpapers) previousFrames[wallpaper] = wallpaper.PresentedFrames;
            refresh.Start();
        };
    }

    private void Sample()
    {
        long now = Stopwatch.GetTimestamp();
        double seconds = Stopwatch.GetElapsedTime(previousTime, now).TotalSeconds;
        process.Refresh();
        var cpu = process.TotalProcessorTime;
        double cpuPercent = (cpu - previousCpu).TotalSeconds / seconds / Environment.ProcessorCount * 100;
        previousTime = now;
        previousCpu = cpu;

        var lines = new List<string>
        {
            $"CPU de EQ: {cpuPercent:F1} % de la capacidad total",
            $"RAM residente: {process.WorkingSet64 / 1048576d:F1} MiB   ·   Memoria privada: {process.PrivateMemorySize64 / 1048576d:F1} MiB",
            $"Límite configurado: {settings.Fps} FPS",
            ""
        };
        foreach (var removed in previousFrames.Keys.Where(window => !wallpapers.Contains(window)).ToArray())
            previousFrames.Remove(removed);
        foreach (var wallpaper in wallpapers)
        {
            long frames = wallpaper.PresentedFrames;
            string fps = previousFrames.TryGetValue(wallpaper, out long before)
                ? $"{(frames - before) / seconds:F1} FPS"
                : "Midiendo FPS…";
            previousFrames[wallpaper] = frames;
            var bounds = wallpaper.MonitorBounds;
            lines.Add($"{bounds.Width} × {bounds.Height} ({bounds.X}, {bounds.Y}) · {fps} · {wallpaper.DebugState}");
            lines.Add(wallpaper.RendererName);
        }
        metrics.Text = string.Join(Environment.NewLine, lines);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refresh.Dispose();
            process.Dispose();
        }
        base.Dispose(disposing);
    }
}
