using System.Drawing;

namespace EQVisualizer;

internal enum InterfaceTheme { Carbon, Graphite, Plum, Forest, Pearl, Sand, Mist, Rose }

internal readonly record struct InterfacePalette(Color Background, Color Sidebar, Color Surface,
    Color Elevated, Color Line, Color Text, Color Muted, Color Accent);

internal static class InterfacePalettes
{
    public static bool IsDark(this InterfaceTheme theme) => theme is InterfaceTheme.Carbon or InterfaceTheme.Graphite or InterfaceTheme.Plum or InterfaceTheme.Forest;
    public static InterfacePalette For(InterfaceTheme theme) => theme switch
    {
        InterfaceTheme.Graphite => new(Color.FromArgb(19, 19, 20), Color.FromArgb(24, 24, 26),
            Color.FromArgb(32, 32, 35), Color.FromArgb(46, 46, 51), Color.FromArgb(70, 70, 76),
            Color.FromArgb(245, 245, 246), Color.FromArgb(180, 180, 186), Color.FromArgb(240, 191, 114)),
        InterfaceTheme.Plum => new(Color.FromArgb(19, 16, 22), Color.FromArgb(24, 20, 27),
            Color.FromArgb(32, 27, 36), Color.FromArgb(46, 39, 52), Color.FromArgb(75, 64, 80),
            Color.FromArgb(247, 243, 249), Color.FromArgb(183, 172, 190), Color.FromArgb(215, 170, 249)),
        InterfaceTheme.Forest => new(Color.FromArgb(16, 19, 17), Color.FromArgb(21, 25, 22),
            Color.FromArgb(28, 34, 30), Color.FromArgb(40, 49, 43), Color.FromArgb(64, 78, 68),
            Color.FromArgb(243, 248, 244), Color.FromArgb(172, 187, 176), Color.FromArgb(149, 225, 164)),
        InterfaceTheme.Pearl => new(Color.FromArgb(244, 245, 247), Color.FromArgb(235, 237, 240),
            Color.FromArgb(255, 255, 255), Color.FromArgb(235, 238, 241), Color.FromArgb(203, 207, 212),
            Color.FromArgb(31, 35, 40), Color.FromArgb(88, 94, 102), Color.FromArgb(15, 105, 103)),
        InterfaceTheme.Sand => new(Color.FromArgb(246, 242, 235), Color.FromArgb(238, 232, 222),
            Color.FromArgb(255, 252, 247), Color.FromArgb(239, 231, 218), Color.FromArgb(201, 191, 175),
            Color.FromArgb(42, 35, 30), Color.FromArgb(99, 87, 76), Color.FromArgb(135, 70, 30)),
        InterfaceTheme.Mist => new(Color.FromArgb(237, 243, 246), Color.FromArgb(225, 233, 237),
            Color.FromArgb(251, 253, 254), Color.FromArgb(224, 233, 238), Color.FromArgb(185, 199, 208),
            Color.FromArgb(25, 39, 47), Color.FromArgb(74, 91, 101), Color.FromArgb(38, 89, 151)),
        InterfaceTheme.Rose => new(Color.FromArgb(248, 241, 244), Color.FromArgb(239, 228, 233),
            Color.FromArgb(255, 251, 253), Color.FromArgb(239, 226, 232), Color.FromArgb(207, 187, 195),
            Color.FromArgb(43, 29, 38), Color.FromArgb(95, 73, 86), Color.FromArgb(130, 52, 86)),
        _ => new(Color.FromArgb(16, 17, 19), Color.FromArgb(20, 21, 23),
            Color.FromArgb(28, 29, 32), Color.FromArgb(40, 42, 46), Color.FromArgb(61, 64, 69),
            Color.FromArgb(244, 245, 247), Color.FromArgb(170, 175, 182), Color.FromArgb(110, 231, 207))
    };

    public static Color EnsureAccent(Color accent, Color surface)
    {
        accent = Color.FromArgb(accent.R, accent.G, accent.B);
        bool lighten = Contrast(Color.White, surface) > Contrast(Color.Black, surface);
        while (Contrast(accent, surface) < 4.5)
            accent = lighten
                ? Color.FromArgb((accent.R + 255) / 2, (accent.G + 255) / 2, (accent.B + 255) / 2)
                : Color.FromArgb(accent.R / 2, accent.G / 2, accent.B / 2);
        return accent;
    }

    public static Color TextOnAccent(Color accent) => Contrast(Color.Black, accent) >= Contrast(Color.White, accent) ? Color.Black : Color.White;

    public static double Contrast(Color a, Color b)
    {
        static double Luminance(Color c)
        {
            static double Channel(byte value)
            {
                double x = value / 255d;
                return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        }
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }
}
