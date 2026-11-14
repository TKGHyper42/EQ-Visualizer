using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using Microsoft.Win32;
using System.Numerics;
using System.Diagnostics;

namespace EQVisualizer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SetCurrentProcessExplicitAppUserModelID("TKGHyper42.EQVisualizer");
        ApplicationConfiguration.Initialize();
        Application.SetColorMode(StoredInterfaceTheme().IsDark() ? SystemColorMode.Dark : SystemColorMode.Classic);
        if (args.Length == 2 && args[0] == "--smoke-test") return SmokeTest.Run(args[1]);
        using var openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\EQVisualizer.OpenSettings");
        using var instance = new Mutex(true, "Local\\EQVisualizer.MusicWallpaper", out bool first);
        if (!first)
        {
            openSettings.Set();
            return 0;
        }
        if (args.Contains("--settings", StringComparer.Ordinal)) openSettings.Set();
        Application.Run(new WallpaperContext(openSettings));
        return 0;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    private static InterfaceTheme StoredInterfaceTheme()
    {
        try
        {
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EQVisualizer", "settings.json");
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.TryGetProperty("InterfaceTheme", out var value) && value.TryGetInt32(out int index) &&
                Enum.IsDefined((InterfaceTheme)index)) return (InterfaceTheme)index;
        }
        catch { }
        return InterfaceTheme.Carbon;
    }
}

internal enum ContrastMode { Adaptive, WholeImage, Off }

internal sealed class Settings
{
    public int Fps { get; set; } = 45;
    public float Strength { get; set; } = 1f;
    public bool Enabled { get; set; } = true;
    public string? AudioDeviceId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int AudioProcessId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public long AudioProcessStartTicks { get; set; }
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
    public bool EnableTunnel { get; set; }
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
    public bool ShowTrackInfo { get; set; } = true;
    public float CaptionOpacity { get; set; } = .65f;
    public bool CaptionTextShadow { get; set; }
    public BarGradient Gradient { get; set; }
    public float GradientStrength { get; set; } = 1;
    public bool UseGdiRenderer { get; set; }
    public float BarThickness { get; set; } = 1.6f;
    public float CircleBarHeight { get; set; } = 1;
    public float BottomBarHeight { get; set; } = 1;
    public float CircleBarThickness { get; set; } = 1.6f;
    public float BottomBarThickness { get; set; } = 1.6f;
    public float CircleBarOpacity { get; set; } = 1f;
    public float BottomBarOpacity { get; set; } = 1f;
    public bool CircleBarRounded { get; set; }
    public bool BottomBarRounded { get; set; }
    public float BottomBarWidth { get; set; } = .52f;
    public float BackgroundBlur { get; set; } = 35;
    public InterfaceTheme InterfaceTheme { get; set; } = InterfaceTheme.Carbon;
    public int InterfaceAccent { get; set; }
    public int InterfaceOpacity { get; set; } = 96;
    public IdleMode NoSessionMode { get; set; } = IdleMode.Off;
    public IdleClockStyle ClockStyle { get; set; } = IdleClockStyle.Digital;
    public string? NoSessionImagePath { get; set; }
}

internal sealed class WallpaperContext : ApplicationContext
{
    private readonly List<WallpaperForm> forms = [];
    private readonly NotifyIcon tray;
    private readonly AudioLevel audio = new();
    private readonly MediaSessionMonitor media;
    private readonly Settings settings;
    private readonly EventWaitHandle openSettings;
    private SettingsForm? activeSettings;
    private DebugForm? debugWindow;
    private readonly HotkeyWindow hotkey = new();
    private readonly ToolStripMenuItem sourceMenu = new("Reproductor");
    private readonly System.Windows.Forms.Timer recoveryTimer = new() { Interval = 500 };
    private readonly System.Windows.Forms.Timer pauseTimer = new() { Interval = 600 };
    private NowPlayingChangedEventArgs? currentTrack;
    private PlaybackActivity activity;
    private bool showingTrack;
    private readonly DesktopIconContrast iconContrast = new();
    private readonly ToolStripMenuItem deviceMenu = new("Salida de audio (mezcla completa)");
    private readonly ToolStripMenuItem captureSourceMenu = new("Capturar sonido de");
    private bool exiting;
    private int pollCount;
    private bool systemPaused;
    private bool sessionLocked, powerSuspended;
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EQVisualizer", "settings.json");

    public WallpaperContext(EventWaitHandle openSettings)
    {
        this.openSettings = openSettings;
        try
        {
            string json = File.ReadAllText(settingsPath);
            settings = JsonSerializer.Deserialize<Settings>(json) ?? new();
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(nameof(Settings.CircleBarThickness), out _))
                settings.CircleBarThickness = settings.BarThickness;
            if (!document.RootElement.TryGetProperty(nameof(Settings.BottomBarThickness), out _))
                settings.BottomBarThickness = settings.BarThickness;
        }
        catch { settings = new(); }
        settings.CaptionOpacity = Math.Clamp(settings.CaptionOpacity, 0, 1);
        settings.GradientStrength = Math.Clamp(settings.GradientStrength, 0, 1);
        if (!Enum.IsDefined(settings.Gradient)) settings.Gradient = BarGradient.Cover;
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
        settings.CircleBarHeight = Math.Clamp(settings.CircleBarHeight, .5f, 3);
        settings.BottomBarHeight = Math.Clamp(settings.BottomBarHeight, .5f, 6);
        settings.CircleBarThickness = Math.Clamp(settings.CircleBarThickness, .5f, 3);
        settings.BottomBarThickness = Math.Clamp(settings.BottomBarThickness, .5f, 3);
        settings.CircleBarOpacity = Math.Clamp(settings.CircleBarOpacity, 0, 1);
        settings.BottomBarOpacity = Math.Clamp(settings.BottomBarOpacity, 0, 1);
        settings.BottomBarWidth = Math.Clamp(settings.BottomBarWidth, .25f, 1);
        settings.BackgroundBlur = Math.Clamp(settings.BackgroundBlur, 0, 100);
        settings.InterfaceOpacity = Math.Clamp(settings.InterfaceOpacity, 80, 100);
        if (!Enum.IsDefined(settings.VisualizerMode)) settings.VisualizerMode = VisualizerMode.Both;
        if (!Enum.IsDefined(settings.ContrastMode)) settings.ContrastMode = ContrastMode.Adaptive;
        if (!Enum.IsDefined(settings.NoSessionMode)) settings.NoSessionMode = IdleMode.Off;
        if (!Enum.IsDefined(settings.ClockStyle)) settings.ClockStyle = IdleClockStyle.Digital;
        if (!Enum.IsDefined(settings.InterfaceTheme)) settings.InterfaceTheme = InterfaceTheme.Carbon;
        Ui.ApplyTheme(settings.InterfaceTheme, settings.InterfaceAccent);
        var menu = new ContextMenuStrip { Renderer = new UiMenuRenderer(), BackColor = Ui.Surface, ForeColor = Ui.Text, Font = Ui.Font(10), ShowImageMargin = false, Padding = new Padding(6) };
        menu.Items.Add("Activar / desactivar", null, (_, _) => Toggle());
        menu.Items.Add("Ajustes...", null, (_, _) => ShowSettings());
        menu.Items.Add("Debug...", null, (_, _) => ShowDebug());
        menu.Items.Add("Abrir bienvenida...", null, (_, _) => ShowWelcome(force: true));
        menu.Items.Add("Crear acceso directo en el escritorio", null, (_, _) => CreateDesktopShortcut());
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
        captureSourceMenu.DropDownOpening += (_, _) => RefreshCaptureSources();
        menu.Items.Add(captureSourceMenu);
        deviceMenu.DropDownOpening += (_, _) => RefreshDevices();
        menu.Items.Add(deviceMenu);
        menu.Items.Add("Salir", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = Ui.CreateIcon(), Text = "EQ Visualizer", ContextMenuStrip = menu, Visible = true };
        media = new MediaSessionMonitor(SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext());
        menu.Items.Insert(2, new ToolStripMenuItem("Estado", null, (_, _) => MessageBox.Show(
            string.Join("\n", forms.Select(f => f.RendererName)) + "\nContraste: " + iconContrast.Status +
            "\nAudio: " + (audio.Error ?? (settings.CaptureAudio ? settings.AudioProcessId > 0 ? $"Proceso {settings.AudioProcessId} (aislado)" : "Mezcla de la salida elegida" : "Desactivado")), "EQ Visualizer")));
        tray.DoubleClick += (_, _) => ShowSettings();
        hotkey.ToggleRequested += (_, _) => Toggle();
        hotkey.EffectsRequested += (_, _) =>
        {
            settings.EnableTunnel = !settings.EnableTunnel;
            settings.EnableParallax = settings.EnableTunnel;
            Save();
            foreach (var form in forms) form.RefreshState(true);
            UpdateWindowsWallpaper();
        };
        if (!hotkey.Configure(settings))
            tray.ShowBalloonTip(4000, "EQ Visualizer", "El atajo Ctrl + Alt + V ya está en uso.", ToolTipIcon.Warning);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        recoveryTimer.Tick += (_, _) =>
        {
            RecoverDesktop();
            if (openSettings.WaitOne(0)) ShowSettings();
        };
        recoveryTimer.Start();
        pauseTimer.Tick += (_, _) => { pauseTimer.Stop(); RefreshMediaPresentation(); };
        CreateWallpapers();
        media.Changed += OnMediaChanged;
        media.PlaybackChanged += (_, e) =>
        {
            SetMediaActivity(e.Activity);
            RefreshMediaPresentation();
        };
        media.SessionsChanged += OnSessionsChanged;
        _ = media.StartAsync(settings.MediaSourceId);
        if (forms.Count > 0) forms[0].BeginInvoke(ShowWelcomeIfNeeded);
    }

    private void CreateWallpapers()
    {
        foreach (var screen in Screen.AllScreens.OrderByDescending(screen => screen.Primary))
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
        forms[0].BeginInvoke(() =>
        {
            if (exiting) return;
            foreach (var form in forms) form.Dispose();
            forms.Clear();
            CreateWallpapers();
            RefreshMediaPresentation(trackChanged: true);
        });
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

    private void RefreshCaptureSources()
    {
        captureSourceMenu.DropDownItems.Clear();
        var mix = (ToolStripMenuItem)captureSourceMenu.DropDownItems.Add("Mezcla completa de la salida", null, (_, _) => SetCaptureProcess(0));
        mix.Checked = settings.AudioProcessId == 0;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
        {
            captureSourceMenu.DropDownItems.Add("Aislamiento: requiere Windows build 20348+").Enabled = false;
            return;
        }
        var found = new HashSet<int>();
        bool selectedAvailable = false;
        try
        {
            using var endpoints = new MMDeviceEnumerator();
            foreach (var device in endpoints.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            using (device)
            {
                var sessions = device.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    using var session = sessions[i];
                    int pid = (int)session.GetProcessID;
                    if (pid <= 0 || found.Contains(pid)) continue;
                    string name; long startTicks;
                    try
                    {
                        using var process = Process.GetProcessById(pid);
                        name = process.ProcessName; startTicks = process.StartTime.ToUniversalTime().Ticks;
                    }
                    catch { continue; }
                    found.Add(pid);
                    var item = (ToolStripMenuItem)captureSourceMenu.DropDownItems.Add($"{name} · PID {pid}", null, (_, _) => SetCaptureProcess(pid));
                    item.Checked = settings.AudioProcessId == pid && settings.AudioProcessStartTicks == startTicks;
                    selectedAvailable |= item.Checked;
                }
            }
        }
        catch { }
        if (settings.AudioProcessId > 0 && !selectedAvailable)
        {
            var missing = (ToolStripMenuItem)captureSourceMenu.DropDownItems.Add($"PID {settings.AudioProcessId} · ya no disponible");
            missing.Checked = true; missing.Enabled = false;
        }
        if (found.Count == 0) captureSourceMenu.DropDownItems.Add("No hay procesos de audio visibles").Enabled = false;
    }

    private void SetCaptureProcess(int pid)
    {
        long startTicks = 0;
        if (pid > 0)
        {
            try { using var process = Process.GetProcessById(pid); startTicks = process.StartTime.ToUniversalTime().Ticks; }
            catch { tray.ShowBalloonTip(4000, "EQ Visualizer", "Ese proceso de audio ya no está disponible.", ToolTipIcon.Warning); return; }
        }
        settings.AudioProcessId = pid;
        settings.AudioProcessStartTicks = startTicks;
        deviceMenu.Enabled = pid == 0;
        audio.Update(settings.CaptureAudio && forms.Any(f => f.NeedsAudio), settings.AudioDeviceId, pid, startTicks);
    }

    private void ShowWelcomeIfNeeded() => ShowWelcome(force: false);

    private void ShowWelcome(bool force)
    {
        if (settings.HasCompletedWelcome && !force) return;
        using var welcome = new WelcomeForm(settings);
        if (welcome.ShowDialog() != DialogResult.OK) return;
        settings.Enabled = true;
        settings.HasCompletedWelcome = true;
        Save();
        RefreshMediaPresentation(trackChanged: true);
        foreach (var form in forms) form.RefreshState(true);
        UpdateWindowsWallpaper();
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
            item.Checked = settings.MediaSourceId is { } selected && MediaSourceIdentity.Matches(id, selected);
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
        if (currentTrack is { } previous && previous.SameTrack(e))
        {
            e.Artwork?.Dispose();
            SetMediaActivity(e.Activity);
            RefreshMediaPresentation();
            return;
        }
        currentTrack?.Artwork?.Dispose();
        currentTrack = e.HasSession ? e : null;
        if (currentTrack is null) e.Artwork?.Dispose();
        SetMediaActivity(e.Activity);
        RefreshMediaPresentation(trackChanged: true);
    }

    private void SetMediaActivity(PlaybackActivity next)
    {
        if (activity == next) return;
        activity = next;
        pauseTimer.Stop();
        if (activity == PlaybackActivity.Paused) pauseTimer.Start();
    }

    private bool RefreshMediaPresentation(bool trackChanged = false)
    {
        var track = settings.NoSessionMode.ShowTrack(activity, pauseTimer.Enabled) ? currentTrack : null;
        bool showTrack = track is not null;
        bool replaceScene = showingTrack != showTrack || trackChanged && showTrack;
        bool playing = activity == PlaybackActivity.Playing;
        if (replaceScene)
        {
            showingTrack = showTrack;
            foreach (var form in forms)
                form.SetNowPlaying(track?.Artwork, playing, track?.Title ?? "", track?.Artist ?? "", showTrack);
            string label = string.IsNullOrWhiteSpace(track?.Title) ? "EQ Visualizer" :
                string.IsNullOrWhiteSpace(track.Artist) ? track.Title : $"{track.Title} — {track.Artist}";
            tray.Text = label.Length > 63 ? label[..63] : label;
        }
        else if (showTrack)
        {
            foreach (var form in forms) form.SetPlaybackState(playing);
        }
        audio.Update(settings.CaptureAudio && forms.Any(f => f.NeedsAudio), settings.AudioDeviceId, settings.AudioProcessId, settings.AudioProcessStartTicks);
        if ((replaceScene || trackChanged) && (!showTrack || !staticWallpaperRequested ||
            forms.FirstOrDefault()?.Attached != true))
            UpdateWindowsWallpaper();
        return replaceScene;
    }

    private void Toggle()
    {
        settings.Enabled = !settings.Enabled;
        Save();
        foreach (var f in forms) f.RefreshState();
        UpdateWindowsWallpaper();
        tray.Text = settings.Enabled ? "EQ Visualizer" : "EQ Visualizer (pausado)";
    }

    private int wallpaperGeneration;
    private bool staticWallpaperRequested;
    private bool primaryAttached;

    private async void UpdateWindowsWallpaper()
    {
        int version = ++wallpaperGeneration;
        WindowsWallpaper.InvalidatePending();
        if (!settings.Enabled || forms.FirstOrDefault() is not { HasMusicSession: true } form)
        {
            staticWallpaperRequested = false;
            _ = WindowsWallpaper.RestoreAsync();
            return;
        }
        staticWallpaperRequested = true;
        try
        {
            var image = await form.CreateWindowsWallpaperAsync();
            if (exiting || version != wallpaperGeneration)
            {
                image?.Dispose();
                return;
            }
            if (image is not null)
                WindowsWallpaper.Apply(image, () =>
                {
                    if (exiting || form.IsDisposed || !form.IsHandleCreated) return;
                    try
                    {
                        form.BeginInvoke(() =>
                        {
                            if (exiting || version != wallpaperGeneration) return;
                            foreach (var wallpaper in forms) wallpaper.RefreshState(true);
                        });
                    }
                    catch (InvalidOperationException) { }
                });
        }
        catch (Exception error) { Trace.TraceWarning("EQ Visualizer wallpaper: {0}", error.Message); }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (forms.Count > 0 && forms[0].IsHandleCreated && forms[0].InvokeRequired)
        {
            forms[0].BeginInvoke(() => OnSessionSwitch(sender, e));
            return;
        }
        if (e.Reason == SessionSwitchReason.SessionLock) sessionLocked = true;
        else if (e.Reason == SessionSwitchReason.SessionUnlock) sessionLocked = false;
        SetSystemPaused(sessionLocked || powerSuspended);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (forms.Count > 0 && forms[0].IsHandleCreated && forms[0].InvokeRequired)
        {
            forms[0].BeginInvoke(() => OnPowerModeChanged(sender, e));
            return;
        }
        if (e.Mode == PowerModes.Suspend) powerSuspended = true;
        else if (e.Mode == PowerModes.Resume) powerSuspended = false;
        SetSystemPaused(sessionLocked || powerSuspended);
    }

    private void SetSystemPaused(bool paused)
    {
        systemPaused = paused;
        foreach (var f in forms) f.SetSystemPaused(paused);
        audio.Update(!paused && settings.CaptureAudio && forms.Any(f => f.NeedsAudio), settings.AudioDeviceId, settings.AudioProcessId, settings.AudioProcessStartTicks);
    }

    private void RecoverDesktop()
    {
        if (exiting) return;
        bool attached = forms.FirstOrDefault()?.Attached == true;
        if (primaryAttached && !attached && showingTrack && settings.Enabled && !systemPaused)
            UpdateWindowsWallpaper();
        primaryAttached = attached;
        foreach (var form in forms)
        {
            if (!systemPaused && settings.Enabled && pollCount % 4 == 0 && !DesktopHost.IsAttached(form.Handle))
                DesktopHost.Attach(form.Handle);
            form.RefreshState();
        }
        audio.Update(settings.CaptureAudio && forms.Any(f => f.NeedsAudio), settings.AudioDeviceId, settings.AudioProcessId, settings.AudioProcessStartTicks);
        if (++pollCount % 4 == 0 && settings.Enabled && !systemPaused) _ = UpdateIconContrastAsync();
    }
    private async Task UpdateIconContrastAsync()
    {
        if (settings.ContrastMode != ContrastMode.Adaptive) return;
        var rects = await iconContrast.RefreshAsync();
        if (!exiting) foreach (var f in forms) f.SetShades(rects);
    }
    private void ShowDebug()
    {
        if (debugWindow is { } current)
        {
            if (current.WindowState == FormWindowState.Minimized) current.WindowState = FormWindowState.Normal;
            current.Activate();
            return;
        }

        var window = new DebugForm(forms, settings);
        debugWindow = window;
        window.FormClosed += (_, _) => debugWindow = null;
        window.Show();
    }

    private void ShowSettings()
    {
        if (activeSettings is { } current)
        {
            if (current.WindowState == FormWindowState.Minimized) current.WindowState = FormWindowState.Normal;
            current.Activate();
            return;
        }
        bool previousRenderer = settings.UseGdiRenderer;
        var previous = (settings.HotkeyKey, settings.EffectsHotkeyKey, settings.HotkeyControl, settings.HotkeyAlt, settings.HotkeyShift);
        var window = new SettingsForm(settings, forms.Count > 0 ? forms[0].CopyArtwork() : null);
        activeSettings = window;
        window.ChangesSaved += (_, _) =>
        {
            ApplySettings(previousRenderer, previous);
            previousRenderer = settings.UseGdiRenderer;
            previous = (settings.HotkeyKey, settings.EffectsHotkeyKey, settings.HotkeyControl, settings.HotkeyAlt, settings.HotkeyShift);
        };
        window.FormClosed += (_, _) =>
        {
            activeSettings = null;
            if (exiting) return;
            Ui.ApplyTheme(settings.InterfaceTheme, settings.InterfaceAccent);
            if (tray.ContextMenuStrip is { } menu) UiMenuRenderer.ApplyTo(menu);
        };
        window.Show();
    }

    private void ApplySettings(bool previousRenderer, (Keys, Keys, bool, bool, bool) previous)
    {
        if (!hotkey.Configure(settings))
        {
            (settings.HotkeyKey, settings.EffectsHotkeyKey, settings.HotkeyControl, settings.HotkeyAlt, settings.HotkeyShift) = previous;
            hotkey.Configure(settings);
            tray.ShowBalloonTip(4000, "EQ Visualizer", "Atajo ocupado; se conservaron los atajos anteriores.", ToolTipIcon.Warning);
        }
        Save();
        Ui.ApplyTheme(settings.InterfaceTheme, settings.InterfaceAccent);
        Application.SetColorMode(settings.InterfaceTheme.IsDark() ? SystemColorMode.Dark : SystemColorMode.Classic);
        if (tray.ContextMenuStrip is { } menu) UiMenuRenderer.ApplyTo(menu);
        foreach (var form in forms)
        {
            if (previousRenderer != settings.UseGdiRenderer) form.ConfigureRenderer();
            form.RefreshIdleOptions();
        }
        if (!RefreshMediaPresentation()) UpdateWindowsWallpaper();
    }

    private void CreateDesktopShortcut()
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("No se encontró el ejecutable.");
            if (string.IsNullOrEmpty(desktop)) throw new DirectoryNotFoundException("No se encontró la carpeta Escritorio.");
            var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(Path.Combine(desktop, "EQ Visualizer.lnk"));
            shortcut.TargetPath = executable;
            shortcut.Arguments = "--settings";
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.IconLocation = executable + ",0";
            shortcut.Description = "Abrir Ajustes de EQ Visualizer";
            shortcut.Save();
        }
        catch (Exception error)
        {
            MessageBox.Show("No se pudo crear el acceso directo. " + error.Message, "EQ Visualizer");
        }
    }

    private void SetFps(int fps) { settings.Fps = fps; Save(); foreach (var f in forms) f.RefreshState(); }
    private void SetStrength(float strength)
    {
        settings.Strength = Math.Clamp(strength, 0, 3);
        Save();
        foreach (var form in forms) form.RefreshState(true);
    }
    private void SetDevice(string? id)
    {
        string? previous = settings.AudioDeviceId;
        try { settings.AudioDeviceId = id; Save(); audio.Update(settings.CaptureAudio && forms.Any(f => f.NeedsAudio), id, settings.AudioProcessId, settings.AudioProcessStartTicks); }
        catch (Exception e)
        {
            tray.ShowBalloonTip(4000, "EQ Visualizer", "Dispositivo no disponible: " + e.Message, ToolTipIcon.Warning);
            try { audio.Update(settings.CaptureAudio && forms.Any(f => f.NeedsAudio), previous, settings.AudioProcessId, settings.AudioProcessStartTicks); } catch { }
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
        pauseTimer.Dispose();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        hotkey.Dispose();
        media.Dispose();
        activeSettings?.Close();
        debugWindow?.Close();
        currentTrack?.Artwork?.Dispose();
        WindowsWallpaper.Restore();
        foreach (var f in forms) { f.Close(); f.Dispose(); }
        audio.Dispose();
        tray.Visible = false;
        tray.Icon?.Dispose();
        tray.Dispose();
        base.ExitThreadCore();
    }
}
