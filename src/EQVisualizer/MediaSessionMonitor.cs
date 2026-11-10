using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace EQVisualizer;

internal sealed class NowPlayingChangedEventArgs(string title, string artist, Image? artwork, bool isPlaying) : EventArgs
{
    public string Title { get; } = title;
    public string Artist { get; } = artist;
    public Image? Artwork { get; } = artwork;
    public bool IsPlaying { get; } = isPlaying;
}

internal sealed class MediaSessionMonitor : IDisposable
{
    private readonly SynchronizationContext context = SynchronizationContext.Current ?? new SynchronizationContext();
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    public event EventHandler<NowPlayingChangedEventArgs>? Changed;

    public async Task StartAsync()
    {
        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            manager.CurrentSessionChanged += OnCurrentSessionChanged;
            await SelectCurrentSessionAsync();
        }
        catch { Publish("", "", null, false); }
    }

    private async void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => await SelectCurrentSessionAsync();

    private async Task SelectCurrentSessionAsync()
    {
        if (session is not null)
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
        session = manager?.GetCurrentSession();
        if (session is null) { Publish("", "", null, false); return; }
        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        await PublishSessionAsync();
    }

    private async void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => await PublishSessionAsync();

    private async void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => await PublishSessionAsync();

    private async Task PublishSessionAsync()
    {
        var current = session;
        if (current is null) return;
        try
        {
            var properties = await current.TryGetMediaPropertiesAsync();
            Image? image = properties.Thumbnail is null ? null : await ReadArtworkAsync(properties.Thumbnail);
            bool playing = current.GetPlaybackInfo().PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            Publish(properties.Title ?? "", properties.Artist ?? "", image, playing);
        }
        catch { }
    }

    private static async Task<Image?> ReadArtworkAsync(IRandomAccessStreamReference reference)
    {
        using var stream = await reference.OpenReadAsync();
        if (stream.Size == 0 || stream.Size > 20 * 1024 * 1024) return null;
        uint length = checked((uint)stream.Size);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync(length);
        byte[] bytes = new byte[length];
        reader.ReadBytes(bytes);
        using var memory = new MemoryStream(bytes);
        using var decoded = Image.FromStream(memory);
        return new Bitmap(decoded);
    }

    private void Publish(string title, string artist, Image? artwork, bool playing)
    {
        context.Post(_ => Changed?.Invoke(this,
            new NowPlayingChangedEventArgs(title, artist, artwork, playing)), null);
    }

    public void Dispose()
    {
        if (manager is not null) manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        if (session is not null)
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
    }
}
