using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
namespace EQVisualizer;

internal static class TrackCaption
{
    // Updated only when metadata changes, then kept as a texture; no per-frame
    // text rasterization, disk cache, song history or network access.
    public static Bitmap? Create(string title, string artist, float backgroundOpacity = .65f, bool textShadow = false)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist)) return null;
        var bitmap = new Bitmap(768, 96, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap); g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var background = new SolidBrush(Color.FromArgb((int)MathF.Round(Math.Clamp(backgroundOpacity, 0, 1) * 255), 8, 10, 15));
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
        var titleBounds = new RectangleF(24, 1, 720, 51);
        var artistBounds = new RectangleF(24, 50, 720, 41);
        if (textShadow)
        {
            using var shadow = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
            foreach (var offset in new[] { new PointF(-3, 3), new PointF(0, 5), new PointF(3, 3) })
            {
                var titleShadow = titleBounds;
                var artistShadow = artistBounds;
                titleShadow.Offset(offset);
                artistShadow.Offset(offset);
                g.DrawString(title, titleFont, shadow, titleShadow, format);
                g.DrawString(artist, artistFont, shadow, artistShadow, format);
            }
        }
        g.DrawString(title, titleFont, titleBrush, titleBounds, format);
        g.DrawString(artist, artistFont, artistBrush, artistBounds, format);
        return bitmap;
    }
}
