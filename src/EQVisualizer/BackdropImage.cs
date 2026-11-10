using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace EQVisualizer;

internal static class BackdropImage
{
    public static Bitmap Create(Image artwork, float amount)
    {
        const int size = 512;
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(artwork, 0, 0, size, size); }
        int radius = (int)Math.Round(Math.Clamp(amount, 0, 100) * 24 / 100);
        if (radius == 0) return bitmap;
        var data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[size * size * 4]; Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            BackdropBlur.Apply(pixels, size, size, radius); Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally { bitmap.UnlockBits(data); }
        return bitmap;
    }
}
