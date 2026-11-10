using System.Runtime.InteropServices;
namespace EQVisualizer;

internal static class DesktopHost
{
    private const uint SMTO_NORMAL = 0;
    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000;
    private const long WS_POPUP = 0x80000000L;
    private static IntPtr worker;
    public static bool Attach(IntPtr hwnd)
    {
        var form = Control.FromHandle(hwnd) as WallpaperForm;
        if (form is null) return false;
        var bounds = form.MonitorBounds;
        if (worker != IntPtr.Zero && !IsWindow(worker)) worker = IntPtr.Zero;
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
        Marshal.SetLastPInvokeError(0);
        if (SetParent(hwnd, worker) == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0) return false;
        var origin = new POINT { X = bounds.X, Y = bounds.Y };
        ScreenToClient(worker, ref origin);
        SetWindowPos(hwnd, IntPtr.Zero, origin.X, origin.Y, bounds.Width, bounds.Height, 0x0014);
        form.SetAttached();
        return true;
    }
    public static bool IsAttached(IntPtr hwnd)
        => IsWindow(hwnd) && worker != IntPtr.Zero && IsWindow(worker) && GetParent(hwnd) == worker;

    public static bool IsCovered(Rectangle bounds)
    {
        bool covered = false;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window) || window == worker || GetParent(window) == worker) return true;
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
