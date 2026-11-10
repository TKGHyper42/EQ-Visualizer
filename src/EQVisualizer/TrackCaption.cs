using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
namespace EQVisualizer;

internal static class TrackCaption
{
    // Updated only when metadata changes, then kept as a texture; no per-frame
    // text rasterization, disk cache, song history or network access.
    public static Bitmap? Create(string title, string artist)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist)) return null;
        var bitmap = new Bitmap(768, 96, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap); g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var background = new SolidBrush(Color.FromArgb(165, 8, 10, 15));
        using var path = new GraphicsPath();
        path.AddArc(0, 0, 24, 24, 180, 90); path.AddArc(743, 0, 24, 24, 270, 90);
        path.AddArc(743, 71, 24, 24, 0, 90); path.AddArc(0, 71, 24, 24, 90, 90); path.CloseFigure(); g.FillPath(background, path);
        using var titleFont = new Font("Segoe UI", 38, FontStyle.Bold, GraphicsUnit.Pixel);
        using var artistFont = new Font("Segoe UI", 28, FontStyle.Regular, GraphicsUnit.Pixel);
        using var titleBrush = new SolidBrush(Color.White);
        using var artistBrush = new SolidBrush(Color.FromArgb(218, 228, 239));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        g.DrawString(title, titleFont, titleBrush, new RectangleF(24, 1, 720, 51), format);
        g.DrawString(artist, artistFont, artistBrush, new RectangleF(24, 50, 720, 41), format);
        return bitmap;
    }
}
