using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Win32;

internal static class Program
{
    private static readonly Assembly Product = Assembly.Load("EQVisualizer");
    private static readonly List<object> Results = [];
    private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string output = "";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        output = args.Single(); Directory.CreateDirectory(output);
        try
        {
            CheckSmallRoundedBounds();
            CheckSettingsTaskbarIcon();
            CheckMediaTransitions();
            CheckWallpaperRestoreQueue();
            CaptureSettings();
            CheckIdlePreviewContrast();
            CheckImageFormats();
            CheckArtworkBounds();
            CheckSystemEventThread();
            CheckUnknownSavedMode();
            File.WriteAllText(Path.Combine(output, "observations.json"), JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Review completed. Observations are diagnostic findings, not a claim that every feature passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            File.WriteAllText(Path.Combine(output, "review-error.txt"), error.ToString());
            return 1;
        }
    }

    private static Type Type(string name) => Product.GetType("EQVisualizer." + name, true)!;
    private static void CheckMediaTransitions()
    {
        var activityType = Type("PlaybackActivity");
        object playing = Enum.Parse(activityType, "Playing");
        object paused = Enum.Parse(activityType, "Paused");
        var resolver = Type("MediaSessionMonitor").GetMethod("ResolveActivity", BindingFlags.Static | BindingFlags.NonPublic)!;
        var statusType = resolver.GetParameters()[0].ParameterType;
        foreach (var prior in new[] { playing, paused })
        foreach (string transient in new[] { "Changing", "Opened" })
            if (!Equals(resolver.Invoke(null, [Enum.Parse(statusType, transient), prior]), prior))
                throw new InvalidOperationException("Transient playback status changed the presentation.");
        using var first = new Bitmap(1, 1) { Tag = "same-cover" };
        using var duplicate = new Bitmap(1, 1) { Tag = "same-cover" };
        using var replacement = new Bitmap(1, 1) { Tag = "new-cover" };
        object Track(string title, Image? cover, object activity) =>
            Activator.CreateInstance(Type("NowPlayingChangedEventArgs"), [title, "Artist", cover, activity, true])!;
        var track = Track("First", first, playing);
        bool Same(object candidate) => (bool)Call(track, "SameTrack", candidate);
        if (!Same(Track("First", duplicate, paused)) || Same(Track("Second", duplicate, playing)) ||
            Same(Track("First", replacement, playing)) || Same(Track("First", null, playing)))
            throw new InvalidOperationException("Track deduplication discarded a real change or repeated identical artwork.");
        Observe("Media transitions", false, "Duplicate artwork, changed title/cover and transient playback states checked.");
    }

    private static void CheckWallpaperRestoreQueue()
    {
        var wallpaper = Type("WindowsWallpaper");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var gate = (SemaphoreSlim)wallpaper.GetField("gate", flags)!.GetValue(null)!;
        var generation = wallpaper.GetField("generation", flags)!;
        Task restore;
        gate.Wait();
        try
        {
            restore = (Task)wallpaper.GetMethod("RestoreAsync")!.Invoke(null, null)!;
            // Supersede this request before releasing the gate, so this regression
            // never changes the real desktop wallpaper.
            generation.SetValue(null, (int)generation.GetValue(null)! + 1);
            if (restore.IsCompleted) throw new InvalidOperationException("Restore bypassed the wallpaper queue.");
        }
        finally { gate.Release(); }
        if (!restore.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Superseded restore did not finish.");
        Observe("Wallpaper restore queue", false, "Restore returns while the writer is busy and discards superseded requests.");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);

    private static void CheckSettingsTaskbarIcon()
    {
        using var form = SettingsWindow(Settings());
        _ = form.Handle;
        if (!form.ShowInTaskbar || SendMessage(form.Handle, 0x7F, 1, 0) == 0 ||
            SendMessage(form.Handle, 0x7F, 0, 0) == 0)
            throw new InvalidOperationException("Settings must expose both native taskbar icons.");
        Observe("Settings taskbar icon", false, "Both native icon sizes available.");
    }
    private static void CheckSmallRoundedBounds()
    {
        var round = Type("Ui").GetMethod("Round")!;
        foreach (var bounds in new[] { RectangleF.Empty, new RectangleF(0, 0, -1, 20),
            new RectangleF(0, 0, 20, 0), new RectangleF(0, 0, 1, 1) })
        {
            using var path = (System.Drawing.Drawing2D.GraphicsPath)round.Invoke(null, [bounds, 14f])!;
            if ((bounds.Width <= 0 || bounds.Height <= 0) && path.PointCount != 0)
                throw new InvalidOperationException("Empty control bounds must have an empty path.");
        }
        using var square = (System.Drawing.Drawing2D.GraphicsPath)round.Invoke(null, [new RectangleF(0, 0, 20, 20), 0f])!;
        if (square.PointCount == 0) throw new InvalidOperationException("Zero corner radius must draw a rectangle.");
        Observe("Small rounded bounds", false, "Empty, negative, one-pixel and square bounds handled.");
    }
    private static object Settings() => Activator.CreateInstance(Type("Settings"))!;
    private static void Set(object target, string name, object value) => target.GetType().GetProperty(name, Instance)!.SetValue(target, value);
    private static object Call(object target, string name, params object?[] values) => target.GetType().GetMethod(name, Instance)!.Invoke(target, values)!;
    private static void Palette(object theme) => Type("Ui").GetMethod("ApplyTheme")!.Invoke(null, [theme, 0]);
    private static void Observe(string test, bool finding, string detail)
    {
        Results.Add(new { test, finding, detail });
        Console.WriteLine($"{(finding ? "FINDING" : "CHECK")}: {test}: {detail}");
    }
    private static void Pump(int milliseconds = 240)
    {
        var time = Stopwatch.StartNew();
        while (time.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
    }
    private static void Capture(Form form, string name)
    {
        form.PerformLayout(); Pump();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(output, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
    }
    private static Form SettingsWindow(object values) => (Form)Activator.CreateInstance(Type("SettingsForm"), [values, null])!;

    private static void CaptureSettings()
    {
        foreach (var theme in Enum.GetValues(Type("InterfaceTheme")))
        {
            Palette(theme);
            bool dark = (bool)Type("InterfacePalettes").GetMethod("IsDark")!.Invoke(null, [theme])!;
            Application.SetColorMode(dark ? SystemColorMode.Dark : SystemColorMode.Classic);
            var values = Settings(); Set(values, "InterfaceTheme", theme);
            using var form = SettingsWindow(values);
            form.Show(); Call(form, "SelectSection", 1);
            Capture(form, "theme-" + theme);
            form.Close();
        }
        var carbon = Enum.Parse(Type("InterfaceTheme"), "Carbon");
        Palette(carbon); Application.SetColorMode(SystemColorMode.Dark);
        using var settings = SettingsWindow(Settings());
        settings.Show();
        var pages = (IList)Type("SettingsForm").GetField("pages", Instance)!.GetValue(settings)!;
        for (int i = 0; i < pages.Count; i++)
        {
            Call(settings, "SelectSection", i);
            var page = (Panel)pages[i]!;
            page.AutoScrollPosition = Point.Empty;
            Capture(settings, "section-" + i + "-top");
            page.AutoScrollPosition = new Point(0, page.VerticalScroll.Maximum);
            Capture(settings, "section-" + i + "-bottom");
        }
        settings.MinimumSize = Size.Empty;
        settings.Size = new Size(800, 600);
        Call(settings, "SelectSection", 0);
        Capture(settings, "settings-800x600");
        Observe("Settings capture coverage", false, $"{pages.Count} sections at top/bottom, all interface themes, 800x600 window. Live metadata, real audio, and desktop attachment are not exercised.");
        settings.Close();
    }

    private static void CheckArtworkBounds()
    {
        var method = Type("MediaSessionMonitor").GetMethod("NormalizeArtwork",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
            throw new InvalidOperationException("Media artwork must have a bounded resampling path.");
        using var landscape = new Bitmap(1800, 900);
        using var scaled = (Bitmap?)method.Invoke(null, [landscape]);
        if (scaled is null || scaled.Width != 1024 || scaled.Height != 512)
            throw new InvalidOperationException("Landscape artwork was not downsampled without distortion.");
        using var oversized = new Bitmap(8193, 2);
        using var rejected = (Bitmap?)method.Invoke(null, [oversized]);
        if (rejected is not null)
            throw new InvalidOperationException("Oversized artwork dimension was not rejected.");
        Observe("Media artwork dimensions", false,
            "1800x900 artwork resampled to 1024x512; oversized dimension rejected before persistent bitmap copy.");
    }

    private static void CheckImageFormats()
    {
        using var image = new Bitmap(2, 2);
        string png = Path.Combine(output, "image-probe.png"); image.Save(png);
        var loader = Type("LocalImage").GetMethod("Load")!;
        using var valid = (Image?)loader.Invoke(null, [png]);
        if (valid is null) throw new InvalidOperationException("PNG image control failed.");
        string webp = Path.Combine(output, "image-probe.webp");
        File.WriteAllBytes(webp, Convert.FromBase64String("UklGRiwAAABXRUJQVlA4ICAAAABQAQCdASoCAAIAAgA0JZAABAAAAP7khx/+N/fV5VFMAA=="));
        using var decoded = (Image?)loader.Invoke(null, [webp]);
        Observe("WebP picker format", false, decoded is null
            ? "WebP is intentionally not advertised; supported picker formats are PNG, JPG, JPEG and BMP."
            : "WebP decoded on this runner, although it is not advertised.");
    }

    private static void CheckSystemEventThread()
    {
        var values = Settings();
        using var audio = (IDisposable)Activator.CreateInstance(Type("AudioLevel"))!;
        using var form = (Form)Activator.CreateInstance(Type("WallpaperForm"), [Screen.PrimaryScreen!, values, audio])!;
        _ = form.Handle;
        using var cover = new Bitmap(16, 16);
        Call(form, "SetNowPlaying", cover, true, "Original test", "EQ Visualizer", true);
        // A synthetic context tests the real event handler without registering hotkeys,
        // attaching to Explorer, starting audio, or writing application settings.
        object context = RuntimeHelpers.GetUninitializedObject(Type("WallpaperContext"));
        var forms = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Type("WallpaperForm")))!;
        forms.Add(form);
        foreach (var pair in new[] { ("settings", values), ("forms", (object)forms), ("audio", (object)audio) })
            Type("WallpaperContext").GetField(pair.Item1, Instance)!.SetValue(context, pair.Item2);
        Exception? failure = null;
        int uiThread = Environment.CurrentManagedThreadId, eventThread = uiThread;
        using var completed = new ManualResetEventSlim();
        bool previous = Control.CheckForIllegalCrossThreadCalls;
        Control.CheckForIllegalCrossThreadCalls = true;
        try
        {
            SystemEvents.InvokeOnEventsThread(new Action(() =>
            {
                eventThread = Environment.CurrentManagedThreadId;
                try { Call(context, "OnSessionSwitch", null, new SessionSwitchEventArgs(SessionSwitchReason.SessionUnlock)); }
                catch (Exception error) { failure = error.GetBaseException(); }
                finally { completed.Set(); }
            }));
            var time = Stopwatch.StartNew();
            while (!completed.IsSet && time.ElapsedMilliseconds < 5000) Pump(20);
            if (!completed.IsSet) throw new TimeoutException("SystemEvents probe did not complete.");
        }
        finally { Control.CheckForIllegalCrossThreadCalls = previous; }
        Observe("Synthetic worker-thread callback", failure is not null,
            $"UI thread {uiThread}, worker thread {eventThread}: " + (failure?.Message ?? "No exception observed.") +
            " Actual SystemEvents delivery and its synchronization-context marshaling are not exercised by this direct-handler probe.");
    }

    private static void CheckIdlePreviewContrast()
    {
        using var form = SettingsWindow(Settings());
        var fields = Type("SettingsForm");
        var idle = (ComboBox)fields.GetField("idleMode", Instance)!.GetValue(form)!;
        var contrast = (ComboBox)fields.GetField("contrast", Instance)!.GetValue(form)!;
        var toggle = (CheckBox)fields.GetField("previewIdle", Instance)!.GetValue(form)!;
        var preview = (Control)fields.GetField("preview", Instance)!.GetValue(form)!;
        form.Show(); idle.SelectedIndex = 1; toggle.Checked = true;
        Color Sample(int mode, string name)
        {
            contrast.SelectedIndex = mode; Pump();
            using var image = new Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(output, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            return image.GetPixel(50, 55);
        }
        var off = Sample(2, "idle-preview-contrast-off");
        var whole = Sample(1, "idle-preview-contrast-whole");
        Observe("Idle preview whole-image contrast", false,
            $"Off={off.ToArgb():X8}; whole-image={whole.ToArgb():X8}. Preview and wallpaper use the same idle shading path.");
        form.Close();
    }

    private static void CheckUnknownSavedMode()
    {
        var values = Settings(); Set(values, "VisualizerMode", Enum.ToObject(Type("VisualizerMode"), 999));
        Exception? failure = null;
        try { using var form = SettingsWindow(values); }
        catch (Exception error) { failure = error.GetBaseException(); }
        Observe("Unknown saved visualizer mode", false,
            failure?.Message ?? "Unrecognized setting is normalized to a supported visualizer mode.");
    }
}
