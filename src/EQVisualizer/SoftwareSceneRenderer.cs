using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace EQVisualizer;

internal static class SoftwareSceneRenderer
{
    public static void Draw(Graphics g, Rectangle viewport, ReadOnlySpan<GpuVertex> vertices,
        Image? artwork, Image? backdrop, Image? caption)
    {
        g.InterpolationMode = InterpolationMode.Bilinear;
        using var fill = new SolidBrush(Color.Transparent);
        using var attributes = new ImageAttributes();
        PointF Pixel(Vector2 point) => new(viewport.X + (point.X + 1) * viewport.Width / 2,
            viewport.Y + (1 - point.Y) * viewport.Height / 2);

        for (int i = 0; i + 5 < vertices.Length; i += 6)
        {
            var a = vertices[i]; var b = vertices[i + 1]; var c = vertices[i + 2]; var d = vertices[i + 5];
            bool roundedBar = a.Textured == VisualScene.RoundedBar;
            if (a.Textured > 3.5f && !roundedBar)
            {
                var p = Pixel(a.Position); var q = Pixel(c.Position);
                var bounds = RectangleF.FromLTRB(p.X, p.Y, q.X, q.Y);
                if (a.Textured > 4.5f)
                {
                    fill.Color = ToColor(a.Color);
                    g.FillEllipse(fill, bounds);
                }
                else if (artwork is not null)
                {
                    var state = g.Save();
                    using var clip = new GraphicsPath();
                    clip.AddEllipse(bounds);
                    g.SetClip(clip, CombineMode.Intersect);
                    g.DrawImage(artwork, bounds);
                    g.Restore(state);
                }
            }
            else if (a.Textured > .5f && !roundedBar)
            {
                var image = a.Textured > 2.5f ? backdrop : a.Textured > 1.5f ? caption : artwork;
                if (image is null) continue;
                attributes.SetColorMatrix(new ColorMatrix
                {
                    Matrix00 = a.Color.X, Matrix11 = a.Color.Y, Matrix22 = a.Color.Z, Matrix33 = a.Color.W, Matrix44 = 1
                });
                g.DrawImage(image, [Pixel(a.Position), Pixel(b.Position), Pixel(d.Position)],
                    new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, attributes);
            }
            else
            {
                var polygon = new[] { Pixel(a.Position), Pixel(b.Position), Pixel(c.Position), Pixel(d.Position) };
                var start = Pixel((a.Position + b.Position) / 2);
                var end = Pixel((c.Position + d.Position) / 2);
                using var gradient = a.Color != c.Color && start != end
                    ? new LinearGradientBrush(start, end, ToColor(a.Color), ToColor(c.Color))
                    : null;
                fill.Color = ToColor(a.Color);
                Brush brush = gradient is null ? fill : gradient;
                if (roundedBar)
                    FillRoundedBar(g, brush, polygon[0], polygon[1], polygon[3]);
                else
                    g.FillPolygon(brush, polygon);
            }
        }
    }

    private static void FillRoundedBar(Graphics g, Brush brush, PointF a, PointF b, PointF d)
    {
        var across = new Vector2(b.X - a.X, b.Y - a.Y);
        var along = new Vector2(d.X - a.X, d.Y - a.Y);
        float width = across.Length(), length = along.Length();
        float diameter = Math.Min(width, length);
        if (diameter <= 0) return;
        using var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(width - diameter, length - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, length - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var transform = new Matrix(across.X / width, across.Y / width, along.X / length, along.Y / length, a.X, a.Y);
        path.Transform(transform);
        g.FillPath(brush, path);
    }

    private static Color ToColor(Vector4 color)
    {
        color = Vector4.Clamp(color, Vector4.Zero, Vector4.One);
        return Color.FromArgb((int)(color.W * 255), (int)(color.X * 255), (int)(color.Y * 255), (int)(color.Z * 255));
    }
}
