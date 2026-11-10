using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using Microsoft.Win32;
using System.Numerics;

namespace EQVisualizer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--smoke-test") return SmokeTest.Run(args[1]);
        using var instance = new Mutex(true, "Local\\EQVisualizer.MusicWallpaper", out bool first);
        if (!first) return 0;
        Application.Run(new WallpaperContext());
        return 0;
    }
}

internal enum ContrastMode { Adaptive, WholeImage, Off }

internal sealed class Settings
{
    public int Fps { get; set; } = 45;
    public float Strength { get; set; } = 1f;
    public bool Enabled { get; set; } = true;
    public string? AudioDeviceId { get; set; }
    public string? MediaSourceId { get; set; }
    public Keys HotkeyKey { get; set; } = Keys.V;
    public bool HotkeyControl { get; set; } = true;
    public bool HotkeyAlt { get; set; } = true;
    public bool HotkeyShift { get; set; }
    public VisualizerMode VisualizerMode { get; set; } = VisualizerMode.Both;
    public bool EnableParallax { get; set; } = true;
    public float ParallaxStrength { get; set; } = 1f;
    public bool HasCompletedWelcome { get; set; }
    public bool CaptureAudio { get; set; } = true;
    public bool EnableTunnel { get; set; } = true;
    public float Sensitivity { get; set; } = 1f;
    public float Smoothing { get; set; } = .12f;
    public float RenderScale { get; set; } = 1f;
    public int QualityLayers { get; set; } = 8;
    public float PositionX { get; set; } = .5f;
    public float PositionY { get; set; } = .48f;
    public float CoverSize { get; set; } = .36f;
    public float BottomMargin { get; set; } = .025f;
    public ContrastMode ContrastMode { get; set; } = ContrastMode.Adaptive;
    public Keys EffectsHotkeyKey { get; set; } = Keys.E;
    public bool ShowTrackInfo { get; set; }
    public float BarThickness { get; set; } = 1.6f;
    public float BackgroundBlur { get; set; } = 35;
}

internal sealed class WallpaperContext : ApplicationContext
{
    private readonly List<WallpaperForm> forms = [];
    private readonly NotifyIcon tray;
    private readonly AudioLevel audio = new();
    private readonly MediaSessionMonitor media;
    private readonly Settings settings;
    private readonly HotkeyWindow hotkey = new();
    private readonly ToolStripMenuItem sourceMenu = new("Reproductor");
    private readonly System.Windows.Forms.Timer recoveryTimer = new() { Interval = 500 };
    private readonly DesktopIconContrast iconContrast = new();
    private readonly ToolStripMenuItem deviceMenu = new("Salida de audio (mezcla completa)");
    private bool exiting;
    private int pollCount;
    private bool systemPaused;
    private bool sessionLocked, powerSuspended;
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EQVisualizer", "settings.json");

    public WallpaperContext()
    {
        try { settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)) ?? new(); }
        catch { settings = new(); }
        settings.Fps = Math.Clamp(settings.Fps, 30, 120);
        settings.Strength = Math.Clamp(settings.Strength, 0, 3);
        settings.ParallaxStrength = Math.Clamp(settings.ParallaxStrength, 0, 3);
        settings.RenderScale = Math.Clamp(settings.RenderScale, .5f, 1);
        settings.Sensitivity = Math.Clamp(settings.Sensitivity, .1f, 5);
        settings.Smoothing = Math.Clamp(settings.Smoothing, .015f, 1);
        settings.CoverSize = Math.Clamp(settings.CoverSize, .15f, .65f);
        settings.PositionX = Math.Clamp(settings.PositionX, .1f, .9f);
        settings.PositionY = Math.Clamp(settings.PositionY, .1f, .9f);
        settings.QualityLayers = Math.Clamp(settings.QualityLayers, 3, 12);
        settings.BarThickness = Math.Clamp(settings.BarThickness, .5f, 3);
        settings.BackgroundBlur = Math.Clamp(settings.BackgroundBlur, 0, 100);
        var menu = new ContextMenuStrip { Renderer = new UiMenuRenderer(), BackColor = Ui.Surface, ForeColor = Ui.Text, Font = Ui.Font(10), ShowImageMargin = false, Padding = new Padding(6) };
        menu.Items.Add("Activar / desactivar", null, (_, _) => Toggle());
        menu.Items.Add("Ajustes...", null, (_, _) => ShowSettings());
        var modeMenu = new ToolStripMenuItem("Visualizador");
        foreach (var mode in VisualizerModes.All)
        {
            var choice = (ToolStripMenuItem)modeMenu.DropDownItems.Add(mode.Label(), null, (_, _) =>
            {
                settings.VisualizerMode = mode; Save(); foreach (var form in forms) form.RefreshState(true);
            });
            choice.Tag = mode;
        }
        modeMenu.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripMenuItem choice in modeMenu.DropDownItems) choice.Checked = Equals(choice.Tag, settings.VisualizerMode);
        };
        menu.Items.Add(modeMenu);
        menu.Items.Add("30 FPS", null, (_, _) => SetFps(30));
        menu.Items.Add("45 FPS", null, (_, _) => SetFps(45));
        menu.Items.Add("60 FPS", null, (_, _) => SetFps(60));
        menu.Items.Add("Menos intensidad", null, (_, _) => SetStrength(settings.Strength - .25f));
        menu.Items.Add("Más intensidad", null, (_, _) => SetStrength(settings.Strength + .25f));
        menu.Items.Add(sourceMenu);
        deviceMenu.DropDownOpening += (_, _) => RefreshDevices();
        menu.Items.Add(deviceMenu);
        menu.Items.Add("Salir", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = Ui.CreateIcon(), Text = "EQ Visualizer", ContextMenuStrip = menu, Visible = true };
        media = new MediaSessionMonitor(SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext());
        menu.Items.Insert(2, new ToolStripMenuItem("Estado", null, (_, _) => MessageBox.Show(
            string.Join("\n", forms.Select(f => f.RendererName)) + "\nContraste: " + iconContrast.Status +
            "\nAudio: " + (audio.Error ?? (settings.CaptureAudio ? "Mezcla de la salida elegida" : "Desactivado")), "EQ Visualizer")));
        tray.DoubleClick += (_, _) => Toggle();
        hotkey.ToggleRequested += (_, _) => Toggle();
        hotkey.EffectsRequested += (_, _) => { settings.EnableTunnel = !settings.EnableTunnel; settings.EnableParallax = settings.EnableTunnel; Save(); };
        if (!hotkey.Configure(settings))
            tray.ShowBalloonTip(4000, "EQ Visualizer", "El atajo Ctrl + Alt + V ya está en uso.", ToolTipIcon.Warning);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        recoveryTimer.Tick += (_, _) => RecoverDesktop();
        recoveryTimer.Start();
        CreateWallpapers();
        media.Changed += OnMediaChanged;
        media.SessionsChanged += OnSessionsChanged;
        _ = media.StartAsync(settings.MediaSourceId);
        if (forms.Count > 0) forms[0].BeginInvoke(ShowWelcomeIfNeeded);
    }

    private void CreateWallpapers()
    {
        foreach (var screen in Screen.AllScreens)
        {
            var form = new WallpaperForm(screen, settings, audio);
            forms.Add(form);
            _ = form.Handle;
            if (!DesktopHost.Attach(form.Handle))
            {
                form.Hide();
                tray.ShowBalloonTip(5000, "EQ Visualizer", "Escritorio no disponible. Se reintentará automáticamente.", ToolTipIcon.Warning);
            }
            form.RefreshState();
        }
    }
    private void OnDisplayChanged(object? sender, EventArgs e)
    {
        if (exiting || forms.Count == 0) return;
        forms[0].BeginInvoke(() => { foreach (var f in forms) f.Dispose(); forms.Clear(); CreateWallpapers(); _ = media.SelectSourceAsync(settings.MediaSourceId); });
    }
    private void RefreshDevices()
    {
        deviceMenu.DropDownItems.Clear();
        var automatic = (ToolStripMenuItem)deviceMenu.DropDownItems.Add("Predeterminada", null, (_, _) => SetDevice(null));
        automatic.Checked = settings.AudioDeviceId is null;
        try
        {
            using var endpoints = new MMDeviceEnumerator();
            foreach (var device in endpoints.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                using (device)
                {
                    string id = device.ID;
                    var item = (ToolStripMenuItem)deviceMenu.DropDownItems.Add(device.FriendlyName, null, (_, _) => SetDevice(id));
                    item.Checked = settings.AudioDeviceId == id;
                }
        }
        catch { }
    }

    private void ShowWelcomeIfNeeded()
    {
        if (settings.HasCompletedWelcome) return;
        using var welcome = new WelcomeForm(settings);
        if (welcome.ShowDialog() == DialogResult.OK)
        {
            settings.HasCompletedWelcome = true;
            Save();
            foreach (var form in forms) form.Invalidate();
        }
    }

    private void OnSessionsChanged(object? sender, MediaSessionsChangedEventArgs e)
    {
        sourceMenu.DropDownItems.Clear();
        var automatic = (ToolStripMenuItem)sourceMenu.DropDownItems.Add("Automático", null, async (_, _) => await SelectSourceAsync(null));
        automatic.Checked = settings.MediaSourceId is null;
        foreach (var choice in e.Sessions)
        {
            string id = choice.Id;
            var item = (ToolStripMenuItem)sourceMenu.DropDownItems.Add(choice.Label, null, async (_, _) => await SelectSourceAsync(id));
            item.Checked = string.Equals(settings.MediaSourceId, id, StringComparison.Ordinal);
        }
        sourceMenu.Enabled = e.Sessions.Count > 0;
    }

    private async Task SelectSourceAsync(string? id)
    {
        settings.MediaSourceId = id;
        Save();
        await media.SelectSourceAsync(id);
    }

    private void OnMediaChanged(object? sender, NowPlayingChangedEventArgs e)
    {
        if (exiting) { e.Artwork?.Dispose(); return; }
        string label = string.IsNullOrWhiteSpace(e.Title) ? "EQ Visualizer" :
            string.IsNullOrWhiteSpace(e.Artist) ? e.Title : $"{e.Title} — {e.Artist}";
        tray.Text = label.Length > 63 ? label[..63] : label;
        foreach (var form in forms) form.SetNowPlaying(e.Artwork, e.IsPlaying, e.Title, e.Artist);
        e.Artwork?.Dispose();
    }

    private void Toggle()
    {
        settings.Enabled = !settings.Enabled;
        Save();
        foreach (var f in forms) f.RefreshState();
        tray.Text = settings.Enabled ? "EQ Visualizer" : "EQ Visualizer (pausado)";
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) sessionLocked = true;
        else if (e.Reason == SessionSwitchReason.SessionUnlock) sessionLocked = false;
        SetSystemPaused(sessionLocked || powerSuspended);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) powerSuspended = true;
        else if (e.Mode == PowerModes.Resume) powerSuspended = false;
        SetSystemPaused(sessionLocked || powerSuspended);
    }

    private void SetSystemPaused(bool paused)
    {
        systemPaused = paused;
        foreach (var f in forms) f.SetSystemPaused(paused);
        audio.Update(!paused && settings.CaptureAudio && forms.Any(f => f.NeedsAudio), settings.AudioDeviceId);
    }

    private void RecoverDesktop()
    {
        if (exiting) return;
        foreach (var form in forms)
        {
            if (!systemPaused && settings.Enabled && pollCount % 4 == 0 && !DesktopHost.IsAttached(form.Handle))
                DesktopHost.Attach(form.Handle);
            form.RefreshState();
        }
        audio.Update(settings.CaptureAudio && forms.Any(f => f.NeedsAudio), settings.AudioDeviceId);
        if (++pollCount % 4 == 0 && settings.Enabled && !systemPaused) _ = UpdateIconContrastAsync();
    }
    private async Task UpdateIconContrastAsync()
    {
        if (settings.ContrastMode != ContrastMode.Adaptive) return;
        var rects = await iconContrast.RefreshAsync();
        if (!exiting) foreach (var f in forms) f.SetShades(rects);
    }
    private void ShowSettings()
    {
        var previous = (settings.HotkeyKey, settings.EffectsHotkeyKey, settings.HotkeyControl, settings.HotkeyAlt, settings.HotkeyShift);
        using var window = new SettingsForm(settings);
        if (window.ShowDialog() != DialogResult.OK) return;
        if (!hotkey.Configure(settings))
        {
            (settings.HotkeyKey, settings.EffectsHotkeyKey, settings.HotkeyControl, settings.HotkeyAlt, settings.HotkeyShift) = previous;
            hotkey.Configure(settings);
            tray.ShowBalloonTip(4000, "EQ Visualizer", "Atajo ocupado; se conservaron los atajos anteriores.", ToolTipIcon.Warning);
        }
        Save();
        foreach (var form in forms) form.RefreshState(true);
    }

    private void SetFps(int fps) { settings.Fps = fps; Save(); foreach (var f in forms) f.RefreshState(); }
    private void SetStrength(float strength) { settings.Strength = Math.Clamp(strength, 0, 3); Save(); }
    private void SetDevice(string? id)
    {
        string? previous = settings.AudioDeviceId;
        try { settings.AudioDeviceId = id; Save(); audio.Update(settings.CaptureAudio && forms.Any(f => f.NeedsAudio), id); }
        catch (Exception e)
        {
            tray.ShowBalloonTip(4000, "EQ Visualizer", "Dispositivo no disponible: " + e.Message, ToolTipIcon.Warning);
            try { audio.Start(previous); } catch { }
        }
    }
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath + ".tmp", JsonSerializer.Serialize(settings));
            File.Move(settingsPath + ".tmp", settingsPath, true);
        }
        catch (Exception e) { tray.ShowBalloonTip(4000, "EQ Visualizer", "No se pudieron guardar los ajustes: " + e.Message, ToolTipIcon.Warning); }
    }
    protected override void ExitThreadCore()
    {
        if (exiting) return;
        exiting = true;
        recoveryTimer.Stop();
        recoveryTimer.Dispose();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        hotkey.Dispose();
        media.Dispose();
        foreach (var f in forms) { f.Close(); f.Dispose(); }
        audio.Dispose();
        tray.Visible = false;
        tray.Icon?.Dispose();
        tray.Dispose();
        base.ExitThreadCore();
    }
}

