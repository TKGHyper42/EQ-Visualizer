namespace EQVisualizer;

internal enum IdleMode { Off, Clock, Image, ImageAndClock }
internal enum PlaybackActivity { Stopped, Paused, Playing }

internal static class IdleModes
{
    public static bool HasClock(this IdleMode mode) => mode is IdleMode.Clock or IdleMode.ImageAndClock;
    public static bool HasImage(this IdleMode mode) => mode is IdleMode.Image or IdleMode.ImageAndClock;
    public static bool ShowTrack(this IdleMode mode, PlaybackActivity activity, bool pauseGrace) =>
        activity == PlaybackActivity.Playing ||
        activity == PlaybackActivity.Paused && mode == IdleMode.Off;
}
