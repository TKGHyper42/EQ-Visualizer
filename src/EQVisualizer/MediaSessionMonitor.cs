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

internal sealed record MediaSessionChoice(string Id, string Label);

internal sealed class MediaSessionsChangedEventArgs(IReadOnlyList<MediaSessionChoice> sessions) : EventArgs
{
    public IReadOnlyList<MediaSessionChoice> Sessions { get; } = sessions;
}

internal sealed class MediaSessionMonitor : IDisposable
{
    private readonly SynchronizationContext context = SynchronizationContext.Current ?? new SynchronizationContext();
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private string? preferredSourceId;
    public event EventHandler<NowPlayingChangedEventArgs>? Changed;
    public event EventHandler<MediaSessionsChangedEventArgs>? SessionsChanged;

    public async Task StartAsync(string? sourceId)
    {
        try
        {
            preferredSourceId = sourceId;
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            manager.CurrentSessionChanged += OnCurrentSessionChanged;
            manager.SessionsChanged += OnSessionsChanged;
            await PublishSessionsAsync();
            await SelectCurrentSessionAsync();
        }
        catch { Publish("", "", null, false); }
    }

    private async void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        if (preferredSourceId is null) await SelectCurrentSessionAsync();
    }

    private async void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        await PublishSessionsAsync();
        await SelectCurrentSessionAsync();
    }

    public async Task SelectSourceAsync(string? sourceId)
    {
        preferredSourceId = sourceId;
        await SelectCurrentSessionAsync();
    }

    private async Task SelectCurrentSessionAsync()
    {
        if (session is not null)
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
        session = manager is null ? null :
            (preferredSourceId is null
                ? manager.GetCurrentSession()
                : manager.GetSessions().FirstOrDefault(item =>
                    string.Equals(item.SourceAppUserModelId, preferredSourceId, StringComparison.Ordinal)));
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

    private async Task PublishSessionsAsync()
    {
        if (manager is null) return;
        var choices = new List<MediaSessionChoice>();
        foreach (var item in manager.GetSessions())
        {
            string label = item.SourceAppUserModelId;
            try
            {
                var properties = await item.TryGetMediaPropertiesAsync();
                if (!string.IsNullOrWhiteSpace(properties.Title))
                    label = string.IsNullOrWhiteSpace(properties.Artist)
                        ? properties.Title
                        : $"{properties.Title} — {properties.Artist}";
            }
            catch { }
            choices.Add(new MediaSessionChoice(item.SourceAppUserModelId, label));
        }
        context.Post(_ => SessionsChanged?.Invoke(this,
            new MediaSessionsChangedEventArgs(choices)), null);
    }

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
        if (manager is not null)
        {
            manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            manager.SessionsChanged -= OnSessionsChanged;
        }
        if (session is not null)
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
    }
}
