using System.Runtime.InteropServices;
namespace EQVisualizer;

internal static class DesktopHost
{
    private const uint SMTO_NORMAL = 0;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CHILD = 0x40000000;
    private const long WS_POPUP = 0x80000000L;
    private const long WS_EX_TRANSPARENT = 0x20;
    private const long WS_EX_LAYERED = 0x80000;
    private static IntPtr worker;
    private static IntPtr iconLayer;
    public static bool Attach(IntPtr hwnd)
    {
        var form = Control.FromHandle(hwnd) as WallpaperForm;
        if (form is null) return false;
        var bounds = form.MonitorBounds;
        if (worker != IntPtr.Zero && !IsWindow(worker)) { worker = IntPtr.Zero; iconLayer = IntPtr.Zero; }
        if (worker == IntPtr.Zero)
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return false;
            // Windows 11 places the wallpaper WorkerW inside Progman, alongside
            // SHELLDLL_DefView; it is no longer necessarily a top-level sibling.
            IntPtr childWorker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            IntPtr desktopIcons = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (UsableWorker(childWorker) && desktopIcons != IntPtr.Zero)
            {
                // Windows replaces its own WorkerW when the static wallpaper
                // changes. Use the stable parent and stay immediately below
                // the desktop icons, so our surface survives those updates.
                worker = progman;
                iconLayer = desktopIcons;
            }
            if (worker == IntPtr.Zero)
            {
                SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SMTO_NORMAL, 1000, out _);
                EnumWindows((top, _) =>
                {
                    if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero) return true;
                    worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                    if (!UsableWorker(worker)) worker = IntPtr.Zero;
                    return worker == IntPtr.Zero;
                }, IntPtr.Zero);
            }
        }
        if (worker == IntPtr.Zero) return false;
        if (IntPtr.Size == 4) SetWindowLong32(hwnd, GWL_STYLE, (int)((GetWindowLong32(hwnd, GWL_STYLE) | WS_CHILD) & ~WS_POPUP));
        else SetWindowLongPtr(hwnd, GWL_STYLE, (GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64() | WS_CHILD) & ~WS_POPUP);
        Marshal.SetLastPInvokeError(0);
        if (SetParent(hwnd, worker) == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0) return false;
        var origin = new POINT { X = bounds.X, Y = bounds.Y };
        ScreenToClient(worker, ref origin);
        return SetWindowPos(hwnd, iconLayer, origin.X, origin.Y, bounds.Width, bounds.Height, iconLayer == IntPtr.Zero ? 0x0014u : 0x0010u);
    }
    private static bool UsableWorker(IntPtr window) => window != IntPtr.Zero && IsWindow(window) &&
        GetWindowRect(window, out var rect) && rect.Right - rect.Left > 200 && rect.Bottom - rect.Top > 200 &&
        FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero;
    public static bool IsAttached(IntPtr hwnd)
        => IsWindow(hwnd) && worker != IntPtr.Zero && IsWindow(worker) && GetParent(hwnd) == worker;
    public static void KeepBelowIcons(IntPtr hwnd)
    {
        if (iconLayer == IntPtr.Zero || !IsAttached(hwnd)) return;
        if (!IsWindow(iconLayer)) iconLayer = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (iconLayer == IntPtr.Zero) return;
        var background = FindWindowEx(worker, IntPtr.Zero, "WorkerW", null);
        for (var next = GetWindow(iconLayer, 2); next != IntPtr.Zero && next != background; next = GetWindow(next, 2))
            if (next == hwnd) return;
        SetWindowPos(hwnd, iconLayer, 0, 0, 0, 0, 0x0013); // no move, size or activation
    }

    public static bool IsCovered(Rectangle bounds)
    {
        bool covered = false;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window) || window == worker || GetParent(window) == worker) return true;
            long extendedStyle = IntPtr.Size == 4 ? GetWindowLong32(window, GWL_EXSTYLE) : GetWindowLongPtr(window, GWL_EXSTYLE).ToInt64();
            // Click-through layers such as cursor highlights can span a whole monitor.
            const long clickThroughLayer = WS_EX_LAYERED | WS_EX_TRANSPARENT;
            if ((extendedStyle & clickThroughLayer) == clickThroughLayer) return true;
            var cls = new System.Text.StringBuilder(256); GetClassName(window, cls, cls.Capacity);
            if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int));
            if (cloaked != 0 || !GetWindowRect(window, out var r)) return true;
            var area = IsZoomed(window) ? Screen.FromRectangle(bounds).WorkingArea : bounds;
            var overlap = Rectangle.Intersect(area, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
            covered = overlap.Width >= area.Width * .95 && overlap.Height >= area.Height * .95;
            return !covered;
        }, IntPtr.Zero);
        return covered;
    }
    private delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hwnd, ref POINT p);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out int value, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int max);
    private struct POINT { public int X, Y; }
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
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    private struct RECT { public int Left, Top, Right, Bottom; }
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x4551;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    public event EventHandler? ToggleRequested;
    public event EventHandler? EffectsRequested;
    public bool Registered { get; private set; }

    public HotkeyWindow() => CreateHandle(new CreateParams());

    public bool Configure(Settings settings)
    {
        UnregisterHotKey(Handle, HotkeyId); UnregisterHotKey(Handle, HotkeyId + 1);
        uint modifiers = 0;
        if (settings.HotkeyAlt) modifiers |= ModAlt;
        if (settings.HotkeyControl) modifiers |= ModControl;
        if (settings.HotkeyShift) modifiers |= ModShift;
        Registered = RegisterHotKey(Handle, HotkeyId, modifiers | 0x4000, (uint)settings.HotkeyKey);
        bool effects = RegisterHotKey(Handle, HotkeyId + 1, modifiers | 0x4000, (uint)settings.EffectsHotkeyKey);
        if (!effects || !Registered) { UnregisterHotKey(Handle, HotkeyId); UnregisterHotKey(Handle, HotkeyId + 1); Registered = false; }
        return Registered;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey && message.WParam.ToInt32() == HotkeyId)
            ToggleRequested?.Invoke(this, EventArgs.Empty);
        if (message.Msg == WmHotkey && message.WParam.ToInt32() == HotkeyId + 1) EffectsRequested?.Invoke(this, EventArgs.Empty);
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        if (Registered) UnregisterHotKey(Handle, HotkeyId);
        UnregisterHotKey(Handle, HotkeyId + 1);
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
