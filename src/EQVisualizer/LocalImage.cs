using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EQVisualizer;

internal static class LocalImage
{
    // A user-selected image is decoded in memory, center-cropped without distortion,
    // and bounded before it becomes a GPU texture. The source file is never copied.
    public static Bitmap? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 20 * 1024 * 1024) return null;
            using var source = Image.FromFile(file.FullName);
            if (source.Width > 8192 || source.Height > 8192 ||
                (long)source.Width * source.Height > 16_777_216) return null;
            var result = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb);
            try
            {
                using var g = Graphics.FromImage(result);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                int side = Math.Min(source.Width, source.Height);
                g.DrawImage(source, new Rectangle(0, 0, 1024, 1024),
                    new Rectangle((source.Width - side) / 2, (source.Height - side) / 2, side, side), GraphicsUnit.Pixel);
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        catch { return null; }
    }
}
