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

internal sealed class MediaSessionMonitor(SynchronizationContext context) : IDisposable
{
    private bool disposed;
    private int generation;
    private readonly SemaphoreSlim selectionGate = new(1, 1);
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
            if (disposed) return;
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
        try { if (preferredSourceId is null) await SelectCurrentSessionAsync(); } catch { }
    }

    private async void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        try { await PublishSessionsAsync(); await SelectCurrentSessionAsync(); } catch { }
    }

    public async Task SelectSourceAsync(string? sourceId)
    {
        preferredSourceId = sourceId;
        try { await SelectCurrentSessionAsync(); } catch { Publish("", "", null, false); }
    }

    private async Task SelectCurrentSessionAsync()
    {
        await selectionGate.WaitAsync();
        try
        {
            if (disposed) return;
            Interlocked.Increment(ref generation);
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
            if (preferredSourceId is null && manager is not null)
            {
                bool currentIsMusic = false;
                if (session is not null)
                {
                    try { currentIsMusic = IsMusic(await session.TryGetMediaPropertiesAsync()); } catch { }
                }
                if (!currentIsMusic)
                {
                    session = null;
                    foreach (var candidate in manager.GetSessions())
                    {
                        try
                        {
                            if (!IsMusic(await candidate.TryGetMediaPropertiesAsync())) continue;
                            session = candidate;
                            if (candidate.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) break;
                        }
                        catch { }
                    }
                }
            }
            if (session is null) { Publish("", "", null, false); return; }
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            await PublishSessionAsync();
        }
        finally { selectionGate.Release(); }
    }

    private async void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    { try { await PublishSessionAsync(); await PublishSessionsAsync(); } catch { } }

    private async void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => await PublishSessionAsync();

    private async Task PublishSessionsAsync()
    {
        if (disposed || manager is null) return;
        var choices = new List<MediaSessionChoice>();
        foreach (var item in manager.GetSessions())
        {
            string label = item.SourceAppUserModelId;
            try
            {
                var properties = await item.TryGetMediaPropertiesAsync();
                if (!IsMusic(properties)) continue;
                if (!string.IsNullOrWhiteSpace(properties.Title))
                    label = string.IsNullOrWhiteSpace(properties.Artist)
                        ? properties.Title
                        : $"{properties.Title} — {properties.Artist}";
            }
            catch { continue; }
            choices.Add(new MediaSessionChoice(item.SourceAppUserModelId, label + " · " + item.SourceAppUserModelId));
        }
        context.Post(_ => { if (!disposed) SessionsChanged?.Invoke(this, new MediaSessionsChangedEventArgs(choices)); }, null);
    }

    private async Task PublishSessionAsync()
    {
        int version = Interlocked.Increment(ref generation);
        var current = session;
        if (disposed || current is null) return;
        try
        {
            var properties = await current.TryGetMediaPropertiesAsync();
            if (disposed || version != generation || !ReferenceEquals(current, session)) return;
            if (!IsMusic(properties)) { Publish("", "", null, false); return; }
            Image? image = properties.Thumbnail is null ? null : await ReadArtworkAsync(properties.Thumbnail);
            bool playing = current.GetPlaybackInfo().PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (disposed || version != generation || !ReferenceEquals(current, session)) { image?.Dispose(); return; }
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
        if (decoded.Width > 8192 || decoded.Height > 8192) return null;
        return new Bitmap(decoded);
    }

    private void Publish(string title, string artist, Image? artwork, bool playing)
    {
        int version = generation;
        context.Post(_ =>
        {
            if (disposed || version != generation) { artwork?.Dispose(); return; }
            var handler = Changed;
            if (handler is null) { artwork?.Dispose(); return; }
            handler(this, new NowPlayingChangedEventArgs(title, artist, artwork, playing));
        }, null);
    }
    private static bool IsMusic(GlobalSystemMediaTransportControlsSessionMediaProperties properties) =>
        properties.PlaybackType == Windows.Media.MediaPlaybackType.Music ||
        properties.PlaybackType is null && !string.IsNullOrWhiteSpace(properties.Artist);

    public void Dispose()
    {
        disposed = true; Interlocked.Increment(ref generation);
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

