using System.Runtime.InteropServices;
using Accessibility;
namespace EQVisualizer;

internal sealed class DesktopIconContrast
{
    private int busy;
    private Rectangle[] last = [];
    public string Status { get; private set; } = "Buscando iconos del escritorio";
    public async Task<Rectangle[]> RefreshAsync()
    {
        if (Interlocked.Exchange(ref busy, 1) != 0) return last;
        try
        {
            var result = await Task.Run(Read);
            last = result.Rects; Status = result.Status; return last;
        }
        finally { Volatile.Write(ref busy, 0); }
    }
    private static (Rectangle[] Rects, string Status) Read()
    {
        IntPtr list = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            var view = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view != IntPtr.Zero) list = FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
            return list == IntPtr.Zero;
        }, IntPtr.Zero);
        if (list == IntPtr.Zero) return ([], "No disponible: usa Oscurecer imagen en Ajustes si lo necesitas");
        object? raw = null;
        try
        {
            var iid = typeof(IAccessible).GUID;
            if (AccessibleObjectFromWindow(list, 0xFFFFFFFC, ref iid, out raw) != 0 || raw is not IAccessible accessible)
                return ([], "El Explorador no expone posiciones: elige Oscurecer imagen");
            var rectangles = new List<Rectangle>();
            int children = accessible.accChildCount;
            if (children > 2048) return ([], "Demasiados iconos: elige Oscurecer imagen");
            for (int child = 1; child <= children; child++)
            {
                try
                {
                    if (Convert.ToInt32(accessible.get_accRole(child)) != 34) continue;
                    accessible.accLocation(out int x, out int y, out int w, out int h, child);
                    if (w > 0 && h > 0 && w < 1000 && h < 1000)
                        rectangles.Add(new Rectangle(x - 5, y - 3, w + 10, h + 6));
                }
                catch (COMException) { }
            }
            return (rectangles.ToArray(), rectangles.Count > 0 ? "Adaptativo: posiciones de iconos del Explorador" : "Sin posiciones accesibles; elige Oscurecer imagen si hay iconos visibles");
        }
        catch { return ([], "No disponible; elige Oscurecer imagen en Ajustes"); }
        finally { if (raw is not null && Marshal.IsComObject(raw)) Marshal.FinalReleaseComObject(raw); }
    }
    private delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? name);
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object? accessible);
}
