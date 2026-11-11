using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace EQVisualizer;

internal sealed class NowPlayingChangedEventArgs(string title, string artist, Image? artwork, bool isPlaying, bool hasSession) : EventArgs
{
    public string Title { get; } = title;
    public string Artist { get; } = artist;
    public Image? Artwork { get; } = artwork;
    public bool IsPlaying { get; } = isPlaying;
    public bool HasSession { get; } = hasSession;
}

internal sealed record MediaSessionChoice(string Id, string Label);
internal sealed class PlaybackStateChangedEventArgs(bool playing) : EventArgs
{
    public bool IsPlaying { get; } = playing;
}

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
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackChanged;
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
        catch { Publish("", "", null, false, false); }
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
        try { await SelectCurrentSessionAsync(); } catch { Publish("", "", null, false, false); }
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
                    : FindPreferredSession(manager, preferredSourceId));
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
            if (session is null) { Publish("", "", null, false, false); return; }
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            await PublishSessionAsync();
        }
        finally { selectionGate.Release(); }
    }

    private static GlobalSystemMediaTransportControlsSession? FindPreferredSession(
        GlobalSystemMediaTransportControlsSessionManager manager, string sourceId)
    {
        var sessions = manager.GetSessions();
        return sessions.FirstOrDefault(item => string.Equals(item.SourceAppUserModelId, sourceId, StringComparison.Ordinal))
            ?? sessions.FirstOrDefault(item => MediaSourceIdentity.Matches(item.SourceAppUserModelId, sourceId));
    }

    private async void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    { try { await PublishSessionAsync(); await PublishSessionsAsync(); } catch { } }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        if (disposed || !ReferenceEquals(sender, session)) return;
        context.Post(_ =>
        {
            if (disposed || !ReferenceEquals(sender, session)) return;
            try
            {
                bool playing = sender.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                PlaybackChanged?.Invoke(this, new PlaybackStateChangedEventArgs(playing));
            }
            catch { }
        }, null);
    }

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
            if (!IsMusic(properties)) { Publish("", "", null, false, false); return; }
            Image? image = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (properties.Thumbnail is not null)
                {
                    try { image = await ReadArtworkAsync(properties.Thumbnail); } catch { }
                    if (image is not null) break;
                }
                if (attempt == 3) break;
                await Task.Delay(125);
                if (disposed || version != generation || !ReferenceEquals(current, session)) return;
                properties = await current.TryGetMediaPropertiesAsync();
            }
            bool playing = current.GetPlaybackInfo().PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (disposed || version != generation || !ReferenceEquals(current, session)) { image?.Dispose(); return; }
            Publish(properties.Title ?? "", properties.Artist ?? "", image, playing, true);
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
        return await Task.Run(() =>
        {
            using var memory = new MemoryStream(bytes);
            using var decoded = Image.FromStream(memory);
            return NormalizeArtwork(decoded);
        });
    }

    // The media API can deliver huge compressed images. Keep only a small,
    // aspect-ratio-preserving bitmap after decoding; never retain full resolution.
    private static Bitmap? NormalizeArtwork(Image decoded)
    {
        const int maxSourceSide = 8192;
        const long maxSourcePixels = 16_777_216;
        const int maxOutputSide = 1024;
        if (decoded.Width < 1 || decoded.Height < 1 ||
            decoded.Width > maxSourceSide || decoded.Height > maxSourceSide ||
            (long)decoded.Width * decoded.Height > maxSourcePixels)
            return null;

        float scale = Math.Min(1f, maxOutputSide / (float)Math.Max(decoded.Width, decoded.Height));
        int width = Math.Max(1, (int)MathF.Round(decoded.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(decoded.Height * scale));
        var output = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(output);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(decoded, 0, 0, width, height);
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private void Publish(string title, string artist, Image? artwork, bool playing, bool hasSession)
    {
        int version = generation;
        context.Post(_ =>
        {
            if (disposed || version != generation) { artwork?.Dispose(); return; }
            var handler = Changed;
            if (handler is null) { artwork?.Dispose(); return; }
            handler(this, new NowPlayingChangedEventArgs(title, artist, artwork, playing, hasSession));
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
