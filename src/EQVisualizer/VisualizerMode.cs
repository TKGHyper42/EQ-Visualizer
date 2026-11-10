namespace EQVisualizer;

// Preserve the numeric values written by earlier builds to settings.json.
internal enum VisualizerMode { Circle = 0, Bottom = 1, Both = 2 }

internal static class VisualizerModes
{
    public static readonly VisualizerMode[] All = [VisualizerMode.Circle, VisualizerMode.Bottom, VisualizerMode.Both];
    public static bool HasCircle(this VisualizerMode mode) => mode is VisualizerMode.Circle or VisualizerMode.Both;
    public static bool HasBottom(this VisualizerMode mode) => mode is VisualizerMode.Bottom or VisualizerMode.Both;
    public static string Label(this VisualizerMode mode) => mode switch
    {
        VisualizerMode.Circle => "Solo círculo",
        VisualizerMode.Bottom => "Solo franja inferior",
        _ => "Círculo y franja"
    };
}
