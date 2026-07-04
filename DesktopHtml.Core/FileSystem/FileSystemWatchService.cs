using System.Collections.Concurrent;

namespace DesktopHtml.Core.FileSystem;

public sealed record FileSystemChange(string ChangeType, string FullPath, string? OldPath);

/// <summary>
/// Bridge-facing file system watching. Wraps FileSystemWatcher with per-watch
/// debouncing so bursts of shell activity arrive as one batched event instead
/// of dozens. Watches are page-scoped — call <see cref="UnwatchAll"/> when the
/// page navigates away.
/// </summary>
public sealed class FileSystemWatchService : IDisposable
{
    private const int DebounceMs = 300;
    private const int MaxChangesPerBatch = 200;

    private readonly ConcurrentDictionary<string, WatchState> _watches = new();

    /// <summary>Starts watching a directory. Returns the watch id.</summary>
    public string Watch(
        string? watchId,
        string path,
        bool recursive,
        Action<string, IReadOnlyList<FileSystemChange>> onChanges)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"watch path does not exist: {path}");
        }

        var id = string.IsNullOrWhiteSpace(watchId) ? Guid.NewGuid().ToString("n") : watchId;
        Unwatch(id);

        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = recursive,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size
        };

        var state = new WatchState(id, watcher, onChanges);
        _watches[id] = state;

        watcher.Created += (_, e) => state.Enqueue(new FileSystemChange("created", e.FullPath, null));
        watcher.Deleted += (_, e) => state.Enqueue(new FileSystemChange("deleted", e.FullPath, null));
        watcher.Changed += (_, e) => state.Enqueue(new FileSystemChange("changed", e.FullPath, null));
        watcher.Renamed += (_, e) => state.Enqueue(new FileSystemChange("renamed", e.FullPath, e.OldFullPath));
        watcher.Error += (_, _) => state.Enqueue(new FileSystemChange("overflow", path, null));
        watcher.EnableRaisingEvents = true;

        return id;
    }

    public bool Unwatch(string watchId)
    {
        if (!_watches.TryRemove(watchId, out var state))
        {
            return false;
        }

        state.Dispose();
        return true;
    }

    public void UnwatchAll()
    {
        foreach (var key in _watches.Keys.ToArray())
        {
            Unwatch(key);
        }
    }

    public void Dispose() => UnwatchAll();

    private sealed class WatchState : IDisposable
    {
        private readonly string _id;
        private readonly FileSystemWatcher _watcher;
        private readonly Action<string, IReadOnlyList<FileSystemChange>> _onChanges;
        private readonly object _gate = new();
        private readonly List<FileSystemChange> _pending = new();
        private readonly Timer _flushTimer;
        private bool _disposed;

        public WatchState(string id, FileSystemWatcher watcher, Action<string, IReadOnlyList<FileSystemChange>> onChanges)
        {
            _id = id;
            _watcher = watcher;
            _onChanges = onChanges;
            _flushTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Enqueue(FileSystemChange change)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (_pending.Count < MaxChangesPerBatch)
                {
                    _pending.Add(change);
                }

                // Sliding debounce: restart the timer on every event.
                _flushTimer.Change(DebounceMs, Timeout.Infinite);
            }
        }

        private void Flush()
        {
            FileSystemChange[] batch;
            lock (_gate)
            {
                if (_disposed || _pending.Count == 0)
                {
                    return;
                }

                batch = _pending.ToArray();
                _pending.Clear();
            }

            try
            {
                _onChanges(_id, batch);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _pending.Clear();
            }

            _flushTimer.Dispose();
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}
