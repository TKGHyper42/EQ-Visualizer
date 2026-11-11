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
    float BarThickness = 1.6f, bool ShowTrackInfo = false, float CircleBarHeight = 1, float BottomBarHeight = 1);

// UVs refer only to the artwork supplied by the current Windows music session.
internal sealed class VisualScene
{
    private static readonly Vector2[] tunnelOutline = Enumerable.Range(0, 32)
        .Select(index => MathF.Tau * index / 32)
        .Select(angle => new Vector2(MathF.Cos(angle), MathF.Sin(angle)))
        .ToArray();
    private readonly GpuVertex[] vertices = new GpuVertex[16384];
    private int count;
    private float width, height;
    public ReadOnlySpan<GpuVertex> Vertices => vertices.AsSpan(0, count);
    public void BuildIdle(float viewWidth, float viewHeight, float coverSize, Vector2 center,
        bool showImage, bool showClock, IdleClockStyle clockStyle = IdleClockStyle.Digital,
        ReadOnlySpan<ShadeRegion> shades = default)
    {
        count = 0; width = Math.Max(1, viewWidth); height = Math.Max(1, viewHeight);
        Quad(width / 2, height / 2, width, height, new(.055f, .058f, .065f, 1), false);
        float unit = Math.Min(width, height), size = unit * coverSize;
        float cx = center.X * width, cy = center.Y * height;
        if (showImage)
        {
            int first = count;
            float background = Math.Max(width, height) * 1.12f;
            Quad(width / 2, height / 2, background, background, new(.35f, .35f, .35f, 1), true);
            for (int i = first; i < count; i++) vertices[i].Textured = 3;
            Quad(cx, cy, size + 14, size + 14, new(0, 0, 0, .55f), false);
            Quad(cx, cy, size, size, Vector4.One, true);
        }
        if (showClock)
        {
            bool analog = clockStyle == IdleClockStyle.Analog;
            float w = unit * (analog ? showImage ? .28f : .46f : showImage ? .55f : .68f);
            float h = analog ? w : w * 240 / 768;
            float clockX = analog && showImage ? cx + size / 2 + w / 2 + unit * .03f : cx;
            float clockY = analog || !showImage ? cy : cy + size / 2 + unit * .025f + h / 2;
            // Keep the optional clock fully visible at every allowed position/size.
            clockX = Math.Clamp(clockX, w / 2, width - w / 2);
            clockY = Math.Clamp(clockY, h / 2, height - h / 2);
            int first = count;
            Quad(clockX, clockY, w, h, Vector4.One, true);
            for (int i = first; i < count; i++) vertices[i].Textured = 2;
        }
        foreach (var region in shades)
            Quad(region.X + region.Width / 2, region.Y + region.Height / 2,
                region.Width, region.Height, new(0, 0, 0, region.Opacity), false);
    }
    public void Build(SceneOptions o, ReadOnlySpan<float> spectrum, ReadOnlySpan<ShadeRegion> shades)
    {
        count = 0; width = Math.Max(1, o.Width); height = Math.Max(1, o.Height);
        float unit = Math.Min(width, height), cx = o.Center.X * width, cy = o.Center.Y * height;
        float background = Math.Max(width, height) * 1.12f;
        Quad(width / 2, height / 2, background, background, new(.38f, .38f, .38f, 1), true);
        for (int i = 0; i < count; i++) vertices[i].Textured = 3;
        float cover = unit * o.CoverSize;
        if (o.Tunnel) DrawTunnel(o, new(cx, cy), cover, unit);
        int shadowStart = count;
        Quad(cx, cy + 5, cover + 14, cover + 14, new(0, 0, 0, .5f), false);
        if (o.Ring) for (int i = shadowStart; i < count; i++) vertices[i].Textured = 5;
        int artworkStart = count;
        Quad(cx, cy, cover, cover, Vector4.One, true);
        if (o.Ring) for (int i = artworkStart; i < count; i++) vertices[i].Textured = 4;
        float intensity = Math.Clamp(o.Intensity, 0, 3);
        float barScale = intensity / (1 + intensity) * (4f / 3);
        for (int i = 0; i < spectrum.Length; i++)
        {
            float signal = Math.Clamp(spectrum[i] * o.Sensitivity, 0, 1);
            var color = Vector4.Lerp(o.Primary, o.Secondary, i / (float)Math.Max(1, spectrum.Length - 1)); color.W = .7f;
            var tip = Vector4.Lerp(color, o.Secondary, .65f); tip.W = .85f;
            if (o.Bottom)
            {
                float bw = width * .52f, h = 1 + signal * unit * .038f * barScale * o.BottomBarHeight;
                float x = width * .24f + (i + .5f) * bw / spectrum.Length, y = height * o.BottomY - h / 2, w = bw / spectrum.Length * Math.Clamp(.55f * o.BarThickness, .2f, .94f);
                Quad(x, y, w + 1, h + 1, new(0, 0, 0, .25f), false);
                int first = count; Quad(x, y, w, h, color, false);
                vertices[first].Color = tip; vertices[first + 1].Color = tip; vertices[first + 3].Color = tip;
            }
            if (o.Ring)
            {
                float angle = MathF.Tau * i / Math.Max(1, spectrum.Length) - MathF.PI / 2, radius = cover * .5f + 5;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var start = new Vector2(cx, cy) + direction * radius;
                var end = start + direction * (1 + signal * unit * .022f * barScale * o.CircleBarHeight);
                float thickness = Math.Max(2, unit * .0035f) * o.BarThickness;
                Line(start, end, thickness + 1, new(0, 0, 0, .25f));
                int first = count; Line(start, end, thickness, color);
                vertices[first + 2].Color = tip; vertices[first + 4].Color = tip; vertices[first + 5].Color = tip;
            }
        }
        if (o.ShowTrackInfo)
        {
            float w = unit * .55f, h = w / 8;
            float ringClearance = o.Ring ? 6 + unit * .022f * barScale * o.CircleBarHeight : 0;
            int first = count;
            Quad(cx, cy + cover / 2 + ringClearance + unit * .014f + h / 2, w, h, Vector4.One, true);
            for (int i = first; i < count; i++) vertices[i].Textured = 2;
        }
        foreach (var r in shades) Quad(r.X + r.Width / 2, r.Y + r.Height / 2, r.Width, r.Height, new(0, 0, 0, r.Opacity), false);
    }
    private void DrawTunnel(SceneOptions options, Vector2 center, float cover, float unit)
    {
        int layers = Math.Clamp(options.Layers, 3, 12);
        float offset = options.Phase - MathF.Floor(options.Phase);
        float farRadius = cover / 2 + unit * .07f;
        float nearRadius = new Vector2(Math.Max(center.X, width - center.X),
            Math.Max(center.Y, height - center.Y)).Length();
        float scale = Math.Max(.8f, unit / 768);
        var rail = Vector4.Lerp(options.Primary, options.Secondary, .5f) with { W = .13f };
        for (int index = 0; index < tunnelOutline.Length; index += 4)
        {
            var direction = tunnelOutline[index];
            Line(center + direction * farRadius, center + direction * nearRadius, scale, rail);
        }
        for (int layer = 0; layer < layers; layer++)
        {
            float depth = (layer + offset) / layers;
            // Rings spread apart as they approach the viewer.
            float radius = farRadius * MathF.Pow(nearRadius / farRadius, depth);
            float thickness = scale * (1 + depth * 2.5f);
            float fade = Math.Min(1, Math.Min(layer + offset, layers - layer - offset));
            var color = Vector4.Lerp(options.Primary, options.Secondary, depth) with { W = fade * (.18f + depth * .29f) };
            for (int index = 0; index < tunnelOutline.Length; index++)
            {
                var a = tunnelOutline[index];
                var b = tunnelOutline[(index + 1) % tunnelOutline.Length];
                Add(center + a * radius, center + b * radius,
                    center + b * (radius - thickness), center + a * (radius - thickness), color, false);
            }
        }
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
