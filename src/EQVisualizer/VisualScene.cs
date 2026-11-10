using System.Numerics;
using System.Runtime.InteropServices;
namespace EQVisualizer;

[StructLayout(LayoutKind.Sequential)]
internal struct GpuVertex
{
    public Vector2 Position;
    public Vector2 Uv;
    public Vector4 Color;
    public float Textured;
}
internal readonly record struct ShadeRegion(float X, float Y, float Width, float Height, float Opacity = .68f);
internal readonly record struct SceneOptions(float Width, float Height, float Phase,
    Vector2 Center, float CoverSize, float Intensity, float Sensitivity, bool Tunnel,
    bool Ring, bool Bottom, float BottomY, int Layers, Vector4 Primary, Vector4 Secondary,
    float BarThickness = 1.6f, bool ShowTrackInfo = false);

// UVs refer only to the artwork supplied by the current Windows music session.
internal sealed class VisualScene
{
    private readonly GpuVertex[] vertices = new GpuVertex[16384];
    private int count;
    private float width, height;
    public ReadOnlySpan<GpuVertex> Vertices => vertices.AsSpan(0, count);
    public void Build(SceneOptions o, ReadOnlySpan<float> spectrum, ReadOnlySpan<ShadeRegion> shades)
    {
        count = 0; width = Math.Max(1, o.Width); height = Math.Max(1, o.Height);
        float unit = Math.Min(width, height), cx = o.Center.X * width, cy = o.Center.Y * height;
        float background = Math.Max(width, height) * 1.12f;
        Quad(width / 2, height / 2, background, background, new(.2f, .2f, .2f, 1), true);
        for (int i = 0; i < count; i++) vertices[i].Textured = 3;
        if (o.Tunnel)
        {
            int layers = Math.Clamp(o.Layers, 3, 12);
            for (int i = layers; i > 0; i--)
            {
                float d = (i + o.Phase % 1) / layers;
                float size = unit * o.CoverSize * (1 + d * 1.7f);
                float drift = MathF.Sin(o.Phase * 2 + d * 4) * unit * .04f * o.Intensity;
                Quad(cx + drift, cy, size, size, new(1, 1, 1, .07f + (1 - d) * .1f), true);
            }
        }
        float cover = unit * o.CoverSize;
        Quad(cx, cy + 5, cover + 14, cover + 14, new(0, 0, 0, .5f), false);
        Quad(cx, cy, cover, cover, Vector4.One, true);
        for (int i = 0; i < spectrum.Length; i++)
        {
            float signal = Math.Clamp(spectrum[i] * o.Sensitivity, 0, 1);
            var color = Vector4.Lerp(o.Primary, o.Secondary, i / (float)Math.Max(1, spectrum.Length - 1)); color.W = .9f;
            var tip = Vector4.Lerp(o.Secondary, new Vector4(1, 1, 1, 1), .22f); tip.W = .98f;
            if (o.Bottom)
            {
                float bw = width * .72f, h = 2 + signal * unit * .16f * o.Intensity;
                float x = width * .14f + (i + .5f) * bw / spectrum.Length, y = height * o.BottomY - h / 2, w = bw / spectrum.Length * Math.Clamp(.55f * o.BarThickness, .2f, .94f);
                Quad(x, y, w + 2, h + 2, new(0, 0, 0, .7f), false);
                int first = count; Quad(x, y, w, h, color, false);
                vertices[first].Color = tip; vertices[first + 1].Color = tip; vertices[first + 3].Color = tip;
            }
            if (o.Ring)
            {
                float angle = MathF.Tau * i / Math.Max(1, spectrum.Length) - MathF.PI / 2, radius = cover * .76f;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var start = new Vector2(cx, cy) + direction * radius;
                var end = start + direction * (2 + signal * unit * .12f * o.Intensity);
                float thickness = Math.Max(2, unit * .0035f) * o.BarThickness;
                Line(start, end, thickness + 2, new(0, 0, 0, .7f));
                int first = count; Line(start, end, thickness, color);
                vertices[first + 2].Color = tip; vertices[first + 4].Color = tip; vertices[first + 5].Color = tip;
            }
        }
        if (o.ShowTrackInfo)
        {
            float w = unit * .55f, h = w / 8;
            int first = count;
            Quad(cx, cy + cover / 2 + unit * .014f + h / 2, w, h, Vector4.One, true);
            for (int i = first; i < count; i++) vertices[i].Textured = 2;
        }
        foreach (var r in shades) Quad(r.X + r.Width / 2, r.Y + r.Height / 2, r.Width, r.Height, new(0, 0, 0, r.Opacity), false);
    }
    private GpuVertex Vertex(Vector2 p, Vector2 uv, Vector4 color, bool textured) => new()
    {
        Position = new(p.X / width * 2 - 1, 1 - p.Y / height * 2),
        Uv = uv,
        Color = color,
        Textured = textured ? 1 : 0
    };
    private void Quad(float cx, float cy, float w, float h, Vector4 color, bool textured)
    {
        Add(new(cx - w / 2, cy - h / 2), new(cx + w / 2, cy - h / 2), new(cx + w / 2, cy + h / 2), new(cx - w / 2, cy + h / 2), color, textured);
    }
    private void Line(Vector2 a, Vector2 b, float thickness, Vector4 color)
    {
        var diff = b - a; var side = new Vector2(-diff.Y, diff.X) / Math.Max(.001f, diff.Length()) * thickness / 2;
        Add(a - side, a + side, b + side, b - side, color, false);
    }
    private void Add(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 color, bool textured)
    {
        if (count + 6 > vertices.Length) return;
        vertices[count++] = Vertex(a, new(0, 0), color, textured); vertices[count++] = Vertex(b, new(1, 0), color, textured);
        vertices[count++] = Vertex(c, new(1, 1), color, textured); vertices[count++] = Vertex(a, new(0, 0), color, textured);
        vertices[count++] = Vertex(c, new(1, 1), color, textured); vertices[count++] = Vertex(d, new(0, 1), color, textured);
    }
}
