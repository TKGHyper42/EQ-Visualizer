namespace EQVisualizer;

internal enum IdleMode { Off, Clock, Image, ImageAndClock }

internal static class IdleModes
{
    public static bool HasClock(this IdleMode mode) => mode is IdleMode.Clock or IdleMode.ImageAndClock;
    public static bool HasImage(this IdleMode mode) => mode is IdleMode.Image or IdleMode.ImageAndClock;
}
