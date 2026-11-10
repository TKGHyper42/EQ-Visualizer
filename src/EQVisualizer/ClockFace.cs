using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EQVisualizer;

internal static class ClockFace
{
    public static Bitmap Create(DateTime now, IdleClockStyle style, Color accent)
        => style == IdleClockStyle.Analog ? Analog(now, accent) : Digital(now, accent);

    private static Bitmap Digital(DateTime now, Color accent)
    {
        var image = new Bitmap(768, 240, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using (var shadow = Ui.Round(new RectangleF(16, 16, 736, 208), 42))
        using (var fill = new SolidBrush(Color.FromArgb(224, 13, 15, 18))) g.FillPath(fill, shadow);
        using (var edge = new Pen(Color.FromArgb(150, accent), 3))
        using (var shape = Ui.Round(new RectangleF(18, 18, 732, 204), 40)) g.DrawPath(edge, shape);
        using (var mark = new SolidBrush(accent)) g.FillEllipse(mark, 58, 48, 12, 12);
        using var timeFont = new Font("Segoe UI", 106, FontStyle.Bold, GraphicsUnit.Pixel);
        using var dateFont = new Font("Segoe UI", 30, FontStyle.Regular, GraphicsUnit.Pixel);
        using var ink = new SolidBrush(Color.White);
        using var soft = new SolidBrush(Color.FromArgb(216, 226, 232));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(now.ToString("HH:mm"), timeFont, ink, new RectangleF(32, 26, 704, 135), format);
        g.DrawString(now.ToString("dddd, d MMMM", System.Globalization.CultureInfo.CurrentCulture),
            dateFont, soft, new RectangleF(36, 158, 696, 47), format);
        return image;
    }

    private static Bitmap Analog(DateTime now, Color accent)
    {
        var image = new Bitmap(512, 512, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var shade = new SolidBrush(Color.FromArgb(172, 0, 0, 0))) g.FillEllipse(shade, 24, 29, 464, 464);
        using (var face = new SolidBrush(Color.FromArgb(238, 18, 20, 23))) g.FillEllipse(face, 16, 16, 480, 480);
        using (var edge = new Pen(Color.FromArgb(176, accent), 5)) g.DrawEllipse(edge, 20, 20, 472, 472);
        for (int i = 0; i < 60; i++)
        {
            float angle = MathF.Tau * i / 60 - MathF.PI / 2;
            bool hour = i % 5 == 0;
            float inner = hour ? 196 : 208;
            using var tick = new Pen(hour ? Color.White : Color.FromArgb(126, 142, 149), hour ? 4 : 2)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(tick, 256 + MathF.Cos(angle) * inner, 256 + MathF.Sin(angle) * inner,
                256 + MathF.Cos(angle) * 222, 256 + MathF.Sin(angle) * 222);
        }
        float hourAngle = MathF.Tau * ((now.Hour % 12 + now.Minute / 60f) / 12) - MathF.PI / 2;
        float minuteAngle = MathF.Tau * (now.Minute / 60f) - MathF.PI / 2;
        using (var hour = new Pen(Color.White, 17) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(hour, 256, 256, 256 + MathF.Cos(hourAngle) * 113, 256 + MathF.Sin(hourAngle) * 113);
        using (var minute = new Pen(accent, 11) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(minute, 256, 256, 256 + MathF.Cos(minuteAngle) * 170, 256 + MathF.Sin(minuteAngle) * 170);
        using (var center = new SolidBrush(Color.White)) g.FillEllipse(center, 246, 246, 20, 20);
        using (var core = new SolidBrush(accent)) g.FillEllipse(core, 251, 251, 10, 10);
        return image;
    }
}
