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
            renderer.Resize(640, 360); renderer.Render(scene.Vertices);
            renderer.SetArtwork(null); renderer.Render([]);
            renderer.SetArtwork(cover); renderer.Render(scene.Vertices);
            renderer.SetCaption(null); renderer.Render([]); renderer.SetCaption(info); renderer.Render(scene.Vertices);
            using var clearBackdrop = BackdropImage.Create(cover, 0); renderer.SetBackground(clearBackdrop); renderer.Render(scene.Vertices); renderer.SetBackground(backdrop);
            Step("Capturing settings form");
            using (var number = new UiNumber { Minimum = 0, Maximum = 10, Increment = .25m, DecimalPlaces = 0, Value = 2.5m })
            {
                if (number.Value != 2.5m) throw new InvalidOperationException("Numeric field rounded an untouched setting.");
                var entry = number.Controls.OfType<TextBox>().Single(); entry.Text = "7";
                if (number.Value != 7) throw new InvalidOperationException("Numeric field did not accept typed input.");
                entry.Text = "999"; if (number.Value != 10) throw new InvalidOperationException("Numeric field exceeded its limit.");
            }
            var values = new Settings();
            var before = System.Text.Json.JsonSerializer.Serialize(values);
            using var settings = new SettingsForm(values);
            settings.Show(); PumpAnimations();
            using var screenshot = new Bitmap(settings.Width, settings.Height);
            settings.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
            screenshot.Save(Path.Combine(directory, "settings.png"), System.Drawing.Imaging.ImageFormat.Png);
            for (int i = 0; i < settings.SectionCount; i++)
            {
                settings.SelectSection(i); PumpAnimations();
                settings.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
                screenshot.Save(Path.Combine(directory, $"settings-{i}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            settings.DialogResult = DialogResult.OK; settings.Close();
            if (System.Text.Json.JsonSerializer.Serialize(values) != before) throw new InvalidOperationException("Settings changed without an edit.");
            using var welcome = new WelcomeForm(new Settings());
            welcome.Show(); PumpAnimations();
            using var welcomeImage = new Bitmap(welcome.Width, welcome.Height);
            welcome.DrawToBitmap(welcomeImage, new Rectangle(Point.Empty, welcomeImage.Size));
            welcomeImage.Save(Path.Combine(directory, "welcome.png"), System.Drawing.Imaging.ImageFormat.Png);
            welcome.Close();
            File.WriteAllText(Path.Combine(directory, "smoke-result.txt"), "PASS: embedded shaders, texture upload, draw, resize, artwork removal/reload. Renderer: " + (renderer.UsesSoftware ? "WARP" : "hardware") + ". Desktop integration and real audio not exercised.");
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
}
