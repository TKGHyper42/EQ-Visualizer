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
internal enum BarGradient { Cover, Inverted, LightTips, Solid }

internal readonly record struct BarAppearance(float Height, float Thickness, float Opacity, bool Rounded);
internal readonly record struct SceneOptions(float Width, float Height, float Phase,
    Vector2 Center, float CoverSize, float Intensity, float Sensitivity, bool Tunnel,
    bool Ring, bool Bottom, float BottomY, int Layers, Vector4 Primary, Vector4 Secondary,
    float BarThickness = 1.6f, bool ShowTrackInfo = false, float CircleBarHeight = 1, float BottomBarHeight = 1,
    BarAppearance? CircleAppearance = null, BarAppearance? BottomAppearance = null, float BottomWidth = .52f,
    BarGradient Gradient = BarGradient.Cover, float GradientStrength = 1);

internal sealed class VisualScene
{
    internal const float RoundedBar = 6;
    private static readonly Vector2[] tunnelOutline = Enumerable.Range(0, 32)
        .Select(index => MathF.Tau * index / 32)
        .Select(angle => new Vector2(MathF.Cos(angle), MathF.Sin(angle)))
        .ToArray();
    private Vector2[] circleDirections = [];
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

        var circle = ResolveAppearance(o.CircleAppearance, o.CircleBarHeight, o.BarThickness, 3);
        var bottom = ResolveAppearance(o.BottomAppearance, o.BottomBarHeight, o.BarThickness, 6);
        bool drawCircle = o.Ring && circle.Opacity > 0 && !spectrum.IsEmpty;
        bool drawBottom = o.Bottom && bottom.Opacity > 0;
        float intensity = Math.Clamp(o.Intensity, 0, 3);
        float barScale = intensity <= 1 ? intensity / (1 + intensity) * (4f / 3) : 2f / 3 + (intensity - 1) * .32f;
        float sensitivity = Math.Max(0, o.Sensitivity);
        float sensitivityBoost = Math.Max(0, sensitivity - 1);

        float stripWidth = width * Math.Clamp(o.BottomWidth, .25f, 1);
        float stripLeft = (width - stripWidth) / 2;
        float pitch = stripWidth / Math.Max(1, spectrum.Length);
        float fillRatio = bottom.Thickness <= 1.6f ? .55f * bottom.Thickness : .88f + (bottom.Thickness - 1.6f) / 14;
        float bottomThickness = pitch * fillRatio;
        float bottomOutline = Math.Min(bottomThickness + 1, pitch * .99f);
        float bottomAnchor = Math.Clamp(height * o.BottomY, .5f, height - .5f);
        float extraBottomHeight = Math.Max(0, bottom.Height - 1);
        float bottomPeak = unit * .038f * barScale * (bottom.Height + .25f * extraBottomHeight * extraBottomHeight);

        var center = new Vector2(cx, cy);
        float circleRadius = cover / 2 + 5;
        float circleThickness = Math.Max(2, unit * .0035f) * circle.Thickness;
        float circlePeak = unit * .022f * barScale * circle.Height;
        float captionWidth = unit * .55f, captionHeight = captionWidth / 8, captionGap = unit * .014f;
        float captionTop = cy + cover / 2 + captionGap;
        bool captionAbove = false;
        if (o.ShowTrackInfo)
        {
            float extent = cover / 2;
            if (drawCircle)
            {
                // Reserve space for the loudest bar, including the outline's outer corners.
                extent = new Vector2(circleRadius + 1.5f + circlePeak, (circleThickness + 1) / 2).Length();
            }
            captionAbove = cy + extent + captionGap + captionHeight > height && cy > height / 2;
            captionTop = captionAbove ? cy - extent - captionGap - captionHeight : cy + extent + captionGap;
            captionTop = Math.Clamp(captionTop, 0, height - captionHeight);
        }

        float gradientStrength = Math.Clamp(o.GradientStrength, 0, 1);
        if (drawCircle && circleDirections.Length != spectrum.Length)
        {
            circleDirections = new Vector2[spectrum.Length];
            for (int i = 0; i < circleDirections.Length; i++)
            {
                float angle = MathF.Tau * i / circleDirections.Length - MathF.PI / 2;
                circleDirections[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        }
        for (int i = 0; (drawCircle || drawBottom) && i < spectrum.Length; i++)
        {
            float band = Math.Clamp(spectrum[i], 0, 1);
            // Boost quiet bands while keeping loud bands distinct at high sensitivity.
            float signal = band * sensitivity / (1 + band * sensitivityBoost);
            var paletteColor = Vector4.Lerp(o.Primary, o.Secondary, i / (float)Math.Max(1, spectrum.Length - 1));
            var color = paletteColor * .42f;
            var tip = o.Gradient switch
            {
                BarGradient.LightTips => Vector4.Lerp(paletteColor, Vector4.One, .65f),
                BarGradient.Solid => paletteColor,
                _ => o.Secondary
            };
            if (o.Gradient == BarGradient.Solid) color = tip;
            if (o.Gradient == BarGradient.Inverted) (color, tip) = (tip, color);
            color = Vector4.Lerp(paletteColor, color, gradientStrength) with { W = .85f };
            tip = Vector4.Lerp(paletteColor, tip, gradientStrength) with { W = .85f };
            if (drawBottom && bottomAnchor > .5f)
            {
                float length = 1 + signal * bottomPeak;
                float halfRoom = (bottomAnchor - .5f) / 2;
                // Compress peaks smoothly as they approach the viewport edge.
                if (length > halfRoom)
                {
                    float excess = length - halfRoom;
                    length = halfRoom + halfRoom * excess / (halfRoom + excess);
                }
                var start = new Vector2(stripLeft + (i + .5f) * pitch, bottomAnchor - length);
                DrawBar(start, Vector2.UnitY, length, bottomThickness, bottomOutline, tip, color, bottom);
            }
            if (drawCircle)
            {
                var direction = circleDirections[i];
                var start = center + direction * circleRadius;
                float length = 1 + signal * circlePeak;
                float towardCaption = captionAbove ? -direction.Y : direction.Y;
                if (o.ShowTrackInfo && towardCaption > 0)
                {
                    float room = captionAbove ? start.Y - captionTop - captionHeight : captionTop - start.Y;
                    float outlineSide = MathF.Abs(direction.X) * (circleThickness + 1) / 2;
                    length = Math.Min(length, (room - captionGap - outlineSide) / towardCaption - .5f);
                }
                if (length > 0)
                    DrawBar(start, direction, length, circleThickness, circleThickness + 1, color, tip, circle);
            }
        }
        if (o.ShowTrackInfo)
        {
            float captionX = Math.Clamp(cx, captionWidth / 2, width - captionWidth / 2);
            int first = count;
            Quad(captionX, captionTop + captionHeight / 2, captionWidth, captionHeight, Vector4.One, true);
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
        float radiusRatio = nearRadius / farRadius;
        for (int layer = 0; layer < layers; layer++)
        {
            float depth = (layer + offset) / layers;
            // Rings spread apart as they approach the viewer.
            float radius = farRadius * MathF.Pow(radiusRatio, depth);
            float thickness = scale * (1 + depth * 2.5f);
            float fade = Math.Min(1, Math.Min(layer + offset, layers - layer - offset));
            var color = Vector4.Lerp(options.Primary, options.Secondary, depth) with { W = fade * (.18f + depth * .29f) };
            float innerRadius = radius - thickness;
            var outer = center + tunnelOutline[0] * radius;
            var inner = center + tunnelOutline[0] * innerRadius;
            for (int index = 0; index < tunnelOutline.Length; index++)
            {
                var b = tunnelOutline[(index + 1) % tunnelOutline.Length];
                var nextOuter = center + b * radius;
                var nextInner = center + b * innerRadius;
                Add(outer, nextOuter, nextInner, inner, color, false);
                outer = nextOuter;
                inner = nextInner;
            }
        }
    }
    private static BarAppearance ResolveAppearance(BarAppearance? appearance, float height, float thickness, float maxHeight)
    {
        var style = appearance ?? new(height, thickness, 1, false);
        return new(Math.Clamp(style.Height, .5f, maxHeight), Math.Clamp(style.Thickness, .5f, 3),
            Math.Clamp(style.Opacity, 0, 1), style.Rounded);
    }
    private void DrawBar(Vector2 start, Vector2 direction, float length, float thickness, float outlineThickness,
        Vector4 startColor, Vector4 endColor, BarAppearance style)
    {
        if (count + 12 > vertices.Length) return;
        float shape = style.Rounded ? RoundedBar : 0;
        var end = start + direction * length;
        var across = new Vector2(direction.Y, -direction.X);
        var side = across * thickness / 2;
        var outlineSide = across * outlineThickness / 2;
        var outlineStart = start - direction * .5f;
        var outlineEnd = end + direction * .5f;
        var outline = new Vector4(0, 0, 0, .25f * style.Opacity);
        Add(outlineStart - outlineSide, outlineStart + outlineSide, outlineEnd + outlineSide, outlineEnd - outlineSide,
            outline, outline, shape);
        Add(start - side, start + side, end + side, end - side,
            startColor with { W = startColor.W * style.Opacity }, endColor with { W = endColor.W * style.Opacity }, shape);
    }
    private GpuVertex Vertex(Vector2 p, Vector2 uv, Vector4 color, float texture) => new()
    {
        Position = new(p.X / width * 2 - 1, 1 - p.Y / height * 2),
        Uv = uv,
        Color = color,
        Textured = texture
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
    private void Add(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 color, bool textured) =>
        Add(a, b, c, d, color, color, textured ? 1 : 0);
    private void Add(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 startColor, Vector4 endColor, float texture)
    {
        if (count + 6 > vertices.Length) return;
        var first = Vertex(a, new(0, 0), startColor, texture);
        var third = Vertex(c, new(1, 1), endColor, texture);
        vertices[count++] = first;
        vertices[count++] = Vertex(b, new(1, 0), startColor, texture);
        vertices[count++] = third;
        vertices[count++] = first;
        vertices[count++] = third;
        vertices[count++] = Vertex(d, new(0, 1), endColor, texture);
    }
}
