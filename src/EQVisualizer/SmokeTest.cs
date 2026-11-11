using System.Numerics;
namespace EQVisualizer;

internal static class SmokeTest
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        void Step(string text) => File.AppendAllText(Path.Combine(directory, "smoke-progress.txt"), text + Environment.NewLine);
        try
        {
            Step("Creating test window");
            using var window = new Form { ClientSize = new Size(960, 540), ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None };
            _ = window.Handle;
            using var cover = new Bitmap(256, 256);
            using (var g = Graphics.FromImage(cover))
            {
                using var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 256, 256), Color.Cyan, Color.Magenta, 40);
                g.FillRectangle(gradient, 0, 0, 256, 256);
                using var pen = new Pen(Color.FromArgb(180, 255, 255, 255), 9);
                for (int i = 0; i < 7; i++) g.DrawEllipse(pen, 30 + i * 11, 20 + i * 7, 160 - i * 12, 190 - i * 15);
            }
            Step("Creating Direct3D renderer");
            using var renderer = new D3D11Renderer(window.Handle, 960, 540);
            Step("Uploading artwork");
            renderer.SetArtwork(cover);
            using var info = TrackCaption.Create("Demo track", "EQ Visualizer");
            renderer.SetCaption(info);
            using var backdrop = BackdropImage.Create(cover, 35); renderer.SetBackground(backdrop);
            var scene = new VisualScene();
            var spectrum = Enumerable.Range(0, 64).Select(i => .25f + .25f * MathF.Sin(i * .3f)).ToArray();
            scene.Build(new(960, 540, .35f, new(.5f, .48f), .36f, 1, 1, true, true, true, .94f, 8, new(0, 1, 1, 1), new(1, 0, 1, 1)), spectrum, []);
            Step("Drawing and reading pixels");
            renderer.Render(scene.Vertices, Path.Combine(directory, "renderer.png"));
            foreach (var mode in VisualizerModes.All)
            {
                scene.Build(new(960, 540, .35f, new(.5f, .48f), .36f, 1, 1, true, mode.HasCircle(), mode.HasBottom(), .94f, 8, new(0, 1, 1, 1), new(1, 0, 1, 1), 1.6f, true), spectrum, []);
                renderer.Render(scene.Vertices, Path.Combine(directory, $"mode-{(int)mode}.png"));
            }
            using var clock = ClockFace.Create(new DateTime(2026, 11, 10, 12, 34, 0), IdleClockStyle.Digital, Color.MediumAquamarine);
            renderer.SetCaption(clock);
            scene.BuildIdle(960, 540, .36f, new(.5f, .48f), true, true);
            renderer.Render(scene.Vertices, Path.Combine(directory, "idle-image-clock.png"));
            renderer.SetArtwork(null);
            scene.BuildIdle(960, 540, .36f, new(.5f, .48f), false, true);
            renderer.Render(scene.Vertices, Path.Combine(directory, "idle-clock.png"));
            using var analog = ClockFace.Create(new DateTime(2026, 11, 10, 12, 34, 0), IdleClockStyle.Analog, Color.MediumAquamarine);
            renderer.SetCaption(analog);
            scene.BuildIdle(960, 540, .36f, new(.5f, .48f), false, true, IdleClockStyle.Analog);
            renderer.Render(scene.Vertices, Path.Combine(directory, "idle-analog.png"));
            renderer.SetArtwork(cover);
            scene.BuildIdle(960, 540, .36f, new(.5f, .48f), true, true, IdleClockStyle.Analog);
            renderer.Render(scene.Vertices, Path.Combine(directory, "idle-image-analog.png"));
            renderer.SetArtwork(cover); renderer.SetCaption(info);
            renderer.Resize(640, 360); renderer.Render(scene.Vertices);
            renderer.SetArtwork(null); renderer.Render([]);
            renderer.SetArtwork(cover); renderer.Render(scene.Vertices);
            renderer.SetCaption(null); renderer.Render([]); renderer.SetCaption(info); renderer.Render(scene.Vertices);
            using var clearBackdrop = BackdropImage.Create(cover, 0); renderer.SetBackground(clearBackdrop); renderer.Render(scene.Vertices); renderer.SetBackground(backdrop);
            using (var edge = new Bitmap(512, 512))
            {
                using (var graphics = Graphics.FromImage(edge))
                {
                    graphics.Clear(Color.Black);
                    graphics.FillRectangle(Brushes.White, 256, 0, 256, 512);
                }
                using var clear = BackdropImage.Create(edge, 0);
                using var blurred = BackdropImage.Create(edge, 35);
                if (clear.GetPixel(232, 256).R > 10 || blurred.GetPixel(232, 256).R < 35)
                    throw new InvalidOperationException("Default blur did not visibly soften the background edge.");
            }
            var softwareOptions = new Settings { UseGdiRenderer = true, EnableTunnel = true };
            using (var audio = new AudioLevel())
            using (var wallpaper = new WallpaperForm(Screen.PrimaryScreen!, softwareOptions, audio))
            {
                wallpaper.SetNowPlaying(cover, false, "Demo track", "EQ Visualizer");
                using var windowsImage = wallpaper.CreateWindowsWallpaper()
                    ?? throw new InvalidOperationException("The Windows wallpaper image was not rendered.");
                int x = (int)(windowsImage.Width * softwareOptions.PositionX);
                int y = (int)(windowsImage.Height * softwareOptions.PositionY);
                var center = windowsImage.GetPixel(x, y);
                if (wallpaper.TopLevel || Math.Max(center.B, Math.Max(center.R, center.G)) < 128)
                    throw new InvalidOperationException("The software wallpaper lost the cover or became a top-level window.");
                windowsImage.Save(Path.Combine(directory, "windows-wallpaper.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            Step("Capturing settings form");
            using (var slider = new UiSlider("Intensidad", 0, 10, .25m, 2.505m))
            {
                if (slider.Value != 2.505m) throw new InvalidOperationException("Slider rounded an untouched setting.");
                var track = slider.Controls.OfType<TrackBar>().Single();
                var entry = slider.Controls.OfType<TextBox>().Single();
                int changes = 0;
                slider.ValueChanged += (_, _) => changes++;
                track.Value = 375;
                if (slider.Value != 3.75m || changes != 1)
                    throw new InvalidOperationException("Moving the slider did not update its value exactly once.");
                entry.Text = "7";
                if (slider.Value != 7 || track.Value != 700)
                    throw new InvalidOperationException("Typing a value did not move the slider.");
                entry.Text = "999";
                if (slider.Value != 10 || track.Value != track.Maximum)
                    throw new InvalidOperationException("The slider exceeded its limit.");
            }
            var values = new Settings();
            var processSettings = System.Text.Json.JsonSerializer.Serialize(new Settings { AudioProcessId = 1234, AudioProcessStartTicks = 5678 });
            if (processSettings.Contains("AudioProcessId") || processSettings.Contains("AudioProcessStartTicks"))
                throw new InvalidOperationException("A temporary audio process identity was persisted.");
            var before = System.Text.Json.JsonSerializer.Serialize(values);
            using var settings = new SettingsForm(values, new Bitmap(cover));
            settings.Show(); PumpAnimations();
            var preview = FindPreview(settings);
            if (preview is null || !preview.Visible || preview.Width < 100 || preview.Height < 100)
                throw new InvalidOperationException("The settings preview is not visible.");
            using (var previewImage = new Bitmap(preview.Width, preview.Height))
            {
                preview.DrawToBitmap(previewImage, new Rectangle(Point.Empty, previewImage.Size));
                if (previewImage.GetPixel(previewImage.Width / 2, previewImage.Height / 2).ToArgb() == Ui.Background.ToArgb())
                    throw new InvalidOperationException("The settings preview did not draw the current artwork.");
                previewImage.Save(Path.Combine(directory, "settings-preview.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            using var screenshot = new Bitmap(settings.Width, settings.Height);
            settings.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
            var previewPoint = settings.PointToClient(preview.PointToScreen(new Point(preview.Width / 2, preview.Height / 2)));
            using (var singlePreview = new Bitmap(preview.Width, preview.Height))
            {
                preview.DrawToBitmap(singlePreview, new Rectangle(Point.Empty, singlePreview.Size));
                if (screenshot.GetPixel(previewPoint.X, previewPoint.Y).ToArgb() !=
                    singlePreview.GetPixel(preview.Width / 2, preview.Height / 2).ToArgb())
                    throw new InvalidOperationException("The preview is covered by another settings control.");
            }
            screenshot.Save(Path.Combine(directory, "settings.png"), System.Drawing.Imaging.ImageFormat.Png);
            for (int i = 0; i < settings.SectionCount; i++)
            {
                settings.SelectSection(i); PumpAnimations();
                settings.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
                screenshot.Save(Path.Combine(directory, $"settings-{i}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            settings.DialogResult = DialogResult.OK; settings.Close();
            if (System.Text.Json.JsonSerializer.Serialize(values) != before) throw new InvalidOperationException("Settings changed without an edit.");
            Application.SetColorMode(SystemColorMode.Classic);
            Ui.ApplyTheme(InterfaceTheme.Pearl, 0);
            using (var light = new SettingsForm(new Settings { InterfaceTheme = InterfaceTheme.Pearl }))
            {
                light.Show(); light.SelectSection(1); PumpAnimations();
                using var lightImage = new Bitmap(light.Width, light.Height);
                light.DrawToBitmap(lightImage, new Rectangle(Point.Empty, lightImage.Size));
                lightImage.Save(Path.Combine(directory, "settings-light.png"), System.Drawing.Imaging.ImageFormat.Png);
                light.Close();
            }
            Application.SetColorMode(SystemColorMode.Dark);
            Ui.ApplyTheme(InterfaceTheme.Carbon, 0);
            using var welcome = new WelcomeForm(new Settings());
            welcome.Show(); PumpAnimations();
            using var welcomeImage = new Bitmap(welcome.Width, welcome.Height);
            welcome.DrawToBitmap(welcomeImage, new Rectangle(Point.Empty, welcomeImage.Size));
            welcomeImage.Save(Path.Combine(directory, "welcome.png"), System.Drawing.Imaging.ImageFormat.Png);
            welcome.Close();
            File.WriteAllText(Path.Combine(directory, "smoke-result.txt"), "PASS: Direct3D, software wallpaper, blur, and settings controls. Renderer: " + (renderer.UsesSoftware ? "WARP" : "hardware") + ". Desktop integration and real audio not exercised.");
            Step("Completed");
            return 0;
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(directory, "smoke-result.txt"), e.ToString()); return 1; }
    }
    private static void PumpAnimations()
    {
        var time = System.Diagnostics.Stopwatch.StartNew();
        while (time.ElapsedMilliseconds < 220) { Application.DoEvents(); Thread.Sleep(10); }
    }
    private static SettingsPreview? FindPreview(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is SettingsPreview preview) return preview;
            var nested = FindPreview(child);
            if (nested is not null) return nested;
        }
        return null;
    }
}
