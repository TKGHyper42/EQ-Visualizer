namespace EQVisualizer;

// Three bounded box passes approximate Gaussian blur. This runs when artwork or
// the blur setting changes, never for each animation frame.
internal static class BackdropBlur
{
    public static void Apply(byte[] pixels, int width, int height, int radius)
    {
        if (width < 1 || height < 1 || pixels.Length != checked(width * height * 4)) throw new ArgumentException("Invalid pixel buffer");
        radius = Math.Clamp(radius, 0, 64); if (radius == 0) return;
        var scratch = new byte[pixels.Length]; int divisor = radius * 2 + 1;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < height; y++) for (int channel = 0; channel < 4; channel++)
            {
                int sum = 0;
                for (int i = -radius; i <= radius; i++) sum += pixels[(y * width + Math.Clamp(i, 0, width - 1)) * 4 + channel];
                for (int x = 0; x < width; x++)
                {
                    scratch[(y * width + x) * 4 + channel] = (byte)(sum / divisor);
                    sum -= pixels[(y * width + Math.Clamp(x - radius, 0, width - 1)) * 4 + channel];
                    sum += pixels[(y * width + Math.Clamp(x + radius + 1, 0, width - 1)) * 4 + channel];
                }
            }
            for (int x = 0; x < width; x++) for (int channel = 0; channel < 4; channel++)
            {
                int sum = 0;
                for (int i = -radius; i <= radius; i++) sum += scratch[(Math.Clamp(i, 0, height - 1) * width + x) * 4 + channel];
                for (int y = 0; y < height; y++)
                {
                    pixels[(y * width + x) * 4 + channel] = (byte)(sum / divisor);
                    sum -= scratch[(Math.Clamp(y - radius, 0, height - 1) * width + x) * 4 + channel];
                    sum += scratch[(Math.Clamp(y + radius + 1, 0, height - 1) * width + x) * 4 + channel];
                }
            }
        }
    }
}
