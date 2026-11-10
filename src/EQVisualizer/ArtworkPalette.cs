using System.Numerics;
namespace EQVisualizer;

internal static class ArtworkPalette
{
    public static (Vector4 Primary, Vector4 Secondary) Create(ReadOnlySpan<Vector3> pixels)
    {
        Span<Vector3> sums = stackalloc Vector3[12]; Span<float> weights = stackalloc float[12];
        sums.Clear();weights.Clear();
        foreach (var rgb in pixels)
        {
            float max = Math.Max(rgb.X, Math.Max(rgb.Y, rgb.Z)), min = Math.Min(rgb.X, Math.Min(rgb.Y, rgb.Z));
            float saturation = max == 0 ? 0 : (max - min) / max;
            if (max < .12f || saturation < .08f) continue;
            int bin = (int)(Hue(rgb) / 30) % 12; float weight = .15f + saturation;
            sums[bin] += rgb * weight; weights[bin] += weight;
        }
        int first = 0; for (int i = 1; i < 12; i++) if (weights[i] > weights[first]) first = i;
        if (weights[first] == 0)
        {
            // A monochrome cover keeps a neutral spectrum instead of inventing a hue.
            return (new(.96f, .97f, 1, 1), new(.72f, .78f, .85f, 1));
        }
        int second = first;
        for (int i = 0; i < 12; i++) if (i != first && weights[i] > 0 && (second == first || weights[i] > weights[second])) second = i;
        Vector3 a = sums[first] / weights[first], b = second == first ? a : sums[second] / weights[second];
        float hueA = Hue(a), hueB = Hue(b);
        if (second == first) hueB = (hueA + 22) % 360;
        return (new(FromHsv(hueA, .62f, 1), 1), new(FromHsv(hueB, .38f, 1), 1));
    }
    private static float Hue(Vector3 c)
    {
        float max = Math.Max(c.X, Math.Max(c.Y, c.Z)), min = Math.Min(c.X, Math.Min(c.Y, c.Z)), d = max - min;
        if (d < .00001f) return 0;
        float h = max == c.X ? (c.Y - c.Z) / d : max == c.Y ? (c.Z - c.X) / d + 2 : (c.X - c.Y) / d + 4;
        return (h * 60 + 360) % 360;
    }
    private static Vector3 FromHsv(float h, float s, float v)
    {
        float c = v * s, x = c * (1 - MathF.Abs(h / 60 % 2 - 1)), m = v - c;
        var rgb = h switch { < 60 => new Vector3(c, x, 0), < 120 => new(x, c, 0), < 180 => new(0, c, x), < 240 => new(0, x, c), < 300 => new(x, 0, c), _ => new(c, 0, x) };
        return rgb + new Vector3(m);
    }
}
