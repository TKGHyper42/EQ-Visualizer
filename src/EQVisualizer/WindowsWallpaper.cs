using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EQVisualizer;

internal static class WindowsWallpaper
{
    private const uint SetDesktopWallpaper = 0x0014;
    private const uint UpdateUserProfile = 0x0001;
    private const uint SendChange = 0x0002;
    private static readonly string directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EQVisualizer");
    private static readonly string wallpaperPath = Path.Combine(directory, "music-wallpaper.png");
    private static readonly string backupPath = Path.Combine(directory, "previous-wallpaper.txt");
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static int generation;
    public static void InvalidatePending() => Interlocked.Increment(ref generation);

    // Takes ownership of the bitmap. Only the most recent request can apply it.
    public static void Apply(Bitmap image, Action? applied = null)
    {
        int version = Interlocked.Increment(ref generation);
        _ = Task.Run(async () =>
        {
            using (image)
            {
                await gate.WaitAsync();
                try
                {
                    if (version != Volatile.Read(ref generation)) return;
                    Directory.CreateDirectory(directory);
                    string temporary = wallpaperPath + ".tmp";
                    try
                    {
                        image.Save(temporary, ImageFormat.Png);
                        if (version != Volatile.Read(ref generation)) return;
                        string current = CurrentWallpaper();
                        if (!IsOurs(current)) File.WriteAllText(backupPath, current);
                        File.Move(temporary, wallpaperPath, overwrite: true);
                        SetWallpaper(wallpaperPath);
                        if (version == Volatile.Read(ref generation)) applied?.Invoke();
                    }
                    finally { File.Delete(temporary); }
                }
                catch (Exception error) { Trace.TraceWarning("EQ Visualizer wallpaper: {0}", error.Message); }
                finally { gate.Release(); }
            }
        });
    }

    public static void Restore() => RestoreAsync().GetAwaiter().GetResult();

    public static Task RestoreAsync()
    {
        int version = Interlocked.Increment(ref generation);
        return Task.Run(async () =>
        {
            await gate.WaitAsync();
            try
            {
                if (version != Volatile.Read(ref generation)) return;
                // Leave any wallpaper the user applied while EQ Visualizer was open.
                string? saved = File.Exists(backupPath) ? File.ReadAllText(backupPath) : null;
                using var history = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");
                var candidates = Enumerable.Range(0, 5).Select(i => history?.GetValue($"BackgroundHistoryPath{i}") as string ?? "").ToArray();
                string? original = WallpaperRecovery.Choose(CurrentWallpaper(), wallpaperPath, saved, candidates, File.Exists);
                if (original is not null) SetWallpaper(original);
                if (File.Exists(wallpaperPath) && !IsOurs(CurrentWallpaper())) File.Delete(wallpaperPath);
            }
            catch (Exception error) { Trace.TraceWarning("EQ Visualizer wallpaper: {0}", error.Message); }
            finally { gate.Release(); }
        });
    }

    private static string CurrentWallpaper()
    {
        using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        return desktop?.GetValue("WallPaper") as string ?? "";
    }

    private static bool IsOurs(string path) => string.Equals(path, wallpaperPath, StringComparison.OrdinalIgnoreCase);

    private static void SetWallpaper(string path)
    {
        if (!SystemParametersInfo(SetDesktopWallpaper, 0, path, UpdateUserProfile | SendChange))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, string value, uint flags);
}
