using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DesktopHtml.App;

/// <summary>
/// Now-playing media info via the Windows System Media Transport Controls
/// (the same source the volume flyout uses). Event-driven: subscriptions
/// rewire SMTC change events, no polling.
/// </summary>
public sealed class MediaService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _watchedSession;
    private Action? _changed;
    private System.Threading.Timer? _debounce;
    private readonly object _gate = new();

    public async Task<object?> GetNowPlayingAsync(bool includeThumbnail)
    {
        var manager = await GetManagerAsync().ConfigureAwait(false);
        var session = manager.GetCurrentSession();
        if (session is null)
        {
            return null;
        }

        var properties = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();

        string? thumbnail = null;
        if (includeThumbnail && properties.Thumbnail is not null)
        {
            thumbnail = await ReadThumbnailAsync(properties.Thumbnail).ConfigureAwait(false);
        }

        return new
        {
            title = properties.Title,
            artist = properties.Artist,
            album = properties.AlbumTitle,
            sourceApp = session.SourceAppUserModelId,
            status = playback.PlaybackStatus.ToString().ToLowerInvariant(),
            positionSeconds = Math.Round(timeline.Position.TotalSeconds, 1),
            durationSeconds = Math.Round((timeline.EndTime - timeline.StartTime).TotalSeconds, 1),
            thumbnail
        };
    }

    public async Task<bool> ControlAsync(string action)
    {
        var manager = await GetManagerAsync().ConfigureAwait(false);
        var session = manager.GetCurrentSession();
        if (session is null)
        {
            return false;
        }

        return action switch
        {
            "play" => await session.TryPlayAsync(),
            "pause" => await session.TryPauseAsync(),
            "playPause" => await session.TryTogglePlayPauseAsync(),
            "next" => await session.TrySkipNextAsync(),
            "previous" => await session.TrySkipPreviousAsync(),
            "stop" => await session.TryStopAsync(),
            _ => throw new InvalidOperationException($"Unknown media action '{action}'. Use play, pause, playPause, next, previous, or stop.")
        };
    }

    /// <summary>Starts pushing change notifications (debounced) to <paramref name="changed"/>.</summary>
    public async Task SubscribeAsync(Action changed)
    {
        var manager = await GetManagerAsync().ConfigureAwait(false);
        lock (_gate)
        {
            _changed = changed;
            manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            manager.CurrentSessionChanged += OnCurrentSessionChanged;
            WatchSession(manager.GetCurrentSession());
        }
    }

    public void Unsubscribe()
    {
        lock (_gate)
        {
            _changed = null;
            if (_manager is not null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }

            WatchSession(null);
            _debounce?.Dispose();
            _debounce = null;
        }
    }

    public void Dispose() => Unsubscribe();

    private async Task<GlobalSystemMediaTransportControlsSessionManager> GetManagerAsync() =>
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        lock (_gate)
        {
            WatchSession(sender.GetCurrentSession());
        }

        Poke();
    }

    private void WatchSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_watchedSession is not null)
        {
            _watchedSession.MediaPropertiesChanged -= OnSessionChanged;
            _watchedSession.PlaybackInfoChanged -= OnSessionChanged;
        }

        _watchedSession = session;
        if (session is not null && _changed is not null)
        {
            session.MediaPropertiesChanged += OnSessionChanged;
            session.PlaybackInfoChanged += OnSessionChanged;
        }
    }

    private void OnSessionChanged<TArgs>(GlobalSystemMediaTransportControlsSession sender, TArgs args) => Poke();

    private void Poke()
    {
        lock (_gate)
        {
            if (_changed is null)
            {
                return;
            }

            // SMTC fires bursts (properties + playback together); coalesce.
            _debounce?.Dispose();
            _debounce = new System.Threading.Timer(_ =>
            {
                try
                {
                    _changed?.Invoke();
                }
                catch
                {
                }
            }, null, 250, Timeout.Infinite);
        }
    }

    private static async Task<string?> ReadThumbnailAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 2 * 1024 * 1024)
            {
                return null;
            }

            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            var contentType = string.IsNullOrWhiteSpace(stream.ContentType) ? "image/png" : stream.ContentType;
            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            return null;
        }
    }
}
