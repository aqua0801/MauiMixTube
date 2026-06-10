using AngleSharp.Dom;
using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Audio;
using MauiMixTube.Extensions;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Managers.Media;
using MauiMixTube.Messages;
using MauiMixTube.Models;
using MauiMixTube.Models.Playlist;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MauiMixTube.Managers
{
    public class PlaylistManager : IAsyncDisposable
    {
        private readonly FetchManager _fetchManager;
        private readonly AudioPipeline _audioPipeline;
        private readonly SettingsManager _settingsManager;
        private readonly IMediaControlsService _mediaControls;

        private int _fetchPageIndex = 0;
        private readonly SemaphoreSlim _fetchPageLock = new(1,1);
        private readonly SemaphoreSlim _startLock = new(1, 1);
        private readonly Lock _shuffleLock = new();

        private List<QueueEntry> _originalQueue = new ();
        private List<QueueEntry> _shuffledQueue = new ();
        private Dictionary<string, AudioInfo> _prefetchedMetadata = new();

        private AudioInfo? _currentTrack;
        private Task? _playTask;

        private volatile bool _isPaused;
        private volatile bool _shuffle;
        private volatile bool _isStarted;
        private volatile bool _isDirectJump;

        private bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    _mediaControls.SetPlaybackStatus(IsPlaying);
                }
            }
        }

        private bool IsStarted
        {
            get => _isStarted;
            set
            {
                if (_isStarted != value)
                {
                    _isStarted = value;
                    _mediaControls.SetPlaybackStatus(IsPlaying);
                }
            }
        }

        private bool IsPlaying => IsStarted && !IsPaused;

        private RepeatMode _repeatMode = RepeatMode.PlayOnce;
        private int _playingIndex;

        private CancellationTokenSource _skipCts = new();
        private CancellationTokenSource _stopCts = new();

        private ChunkedAudioStream? _currentAudio;
        private readonly PcmPlayer _pcmPlayer;
      
        private int _disposeGuard;

        private List<QueueEntry> _playQueue => this._shuffle ? _shuffledQueue : _originalQueue;

        public event Action<TrackDisplayItem>? TrackChanged;
        public event Action? QueueReset;

        public PlaylistManager(
            FetchManager fetchManager , 
            AudioPipeline audioPipeline , 
            SettingsManager settingsManager,
            IMediaControlsService mediaControls)
        {
            _fetchManager = fetchManager;
            _audioPipeline = audioPipeline;
            _settingsManager = settingsManager;
            _mediaControls = mediaControls;

            _pcmPlayer = new(_settingsManager);
            _pcmPlayer.SetVolume((float)settingsManager.Volume / 100f);

            WeakReferenceMessenger.Default
                .Register<VolumeChangedMessage>(this, (r, m) =>
                    _pcmPlayer?.SetVolume((float)m.Value / 100f));


            WeakReferenceMessenger.Default
                .Register<MediaKeyMessage>(this, (r, m) =>
                {
                    switch (m.Key)
                    {
                        case MediaKey.PlayPause: _ = TogglePlayPauseAsync(); break;
                        case MediaKey.Next: Skip(); break;
                        case MediaKey.Previous: Previous(); break;
                    }
                });

        }

        public async Task EnqueueAsync(QueueEntry entry , CancellationToken ct)
        {
            _playQueue.Add(entry);

            if(_playQueue.Count == 1)
            {
                await InvokeTrackChangedEventAsync(ct);
            }
        }

        public void Enqueue(IEnumerable<QueueEntry> entries) 
            => _playQueue.AddRange(entries);

        public bool Start()
        {
            if (_playQueue == null || _playQueue.Count == 0)
                return false;

            IsStarted = true;
            _isDirectJump = false;
            _playTask = RunPlaybackLoopAsync();
            _playTask.ContinueWith(t => 
            {
                IsStarted = false;
            });
            return true;
        }

        public async Task LoadAsync(UserPlaylist playlist, CancellationToken ct)
        {
            if (IsStarted)
                await StopAsync(ct);

            _originalQueue.Clear();
            _shuffledQueue.Clear();
            _prefetchedMetadata.Clear();
            _stopCts = new CancellationTokenSource(); 
            _playingIndex= 0;
            _fetchPageIndex = 0;
            Interlocked.Exchange(ref _disposeGuard, 0);

            foreach (var source in playlist.Sources)
            {
                if (source.IsPlaylist)
                {
                    var entries = await _fetchManager.ExpandPlaylistAsync(source , ct);

                    foreach(var entry in entries)
                    {
                        if (!entry.IsFromPlaylist)
                            _originalQueue.Add(entry with {IsFromPlaylist = true });
                        else
                            _originalQueue.Add(entry);
                    }
                }
                else
                {
                    _originalQueue.Add(new QueueEntry(source.Tag, source.Url,false));
                }
            }

            if (_shuffle)
                ShufflePlaylist(false);

            QueueReset?.Invoke();

            if (_playQueue.Count > 0)
                await PreviewTrackAsync(_playQueue[0], ct);
        }
        public async Task<bool> TogglePlayPauseAsync()
        {
            await _startLock.WaitAsync();
            try
            {
                if (!IsStarted)
                    Start();
                else
                    TogglePause();
            }
            finally
            {
                _startLock.Release();
            }
            return IsPlaying;
        }
        public void Skip()
        {
            _skipCts.Cancel();
            _pcmPlayer.Stop();
        }

        public void Previous()
        {
            if (
                CurrentPosition >= TimeSpan.FromSeconds(5)
                || _playingIndex < 1
                || _repeatMode == RepeatMode.RepeatOne)
            {
                _ = _pcmPlayer.SeekAsync(TimeSpan.Zero).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Console.WriteLine($"[Playlist] SeekAsync failed: {t.Exception?.Flatten()}");
                }, TaskScheduler.Default);
            }
            else
            {
                _playingIndex--;
                _isDirectJump = true;
                Skip();
            }
        }
        public bool ToggleShuffle()
        {
            lock (_shuffleLock)
            {
                _shuffle = !_shuffle;

                if (_shuffle)
                {
                    ShufflePlaylist(IsStarted);
                }
                else
                    DisableShuffle();

                QueueReset?.Invoke();

                return _shuffle;
            }
        }

        public RepeatMode ToggleRepeat()
        {
            _repeatMode = _repeatMode.Next();
            return _repeatMode;
        }

        public async Task SeekAsync(TimeSpan seek)
        {
            if(_pcmPlayer is not null)
            {
                await _pcmPlayer.SeekAsync(seek);
            }
        }

        public async Task StopAsync(CancellationToken ct)
        {
            _stopCts.Cancel();
            _currentAudio?.Dispose();
            _fetchPageIndex = 0;
            _playingIndex = 0;
            IsStarted = false;
            _isDirectJump = false;

            var linked = CancellationTokenSource.CreateLinkedTokenSource(
                ct,
                new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token);

            try
            {
                await _playTask.Safe.WaitAsync(linked.Token);
            }
            catch { }
        }

        public async Task JumpToIndexAsync(int trackIndex , CancellationToken ct)
        {
            if (_playQueue is null || trackIndex < 0 || trackIndex >= _playQueue.Count)
                return;
            _playingIndex = trackIndex;
            _isDirectJump = true;
           

            if(!IsPlaying)
                await InvokeTrackChangedEventAsync(ct);
            else
                Skip();
        }

        public async IAsyncEnumerable<TrackDisplayItem> FetchNextPageAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            List<QueueEntry>? batch = null;

            await _fetchPageLock.WaitAsync(ct);

            try
            {
                if (_playQueue is null) yield break;

                var endIndex = Math.Min(_fetchPageIndex + _settingsManager.FetchPageSize, _playQueue.Count);
                batch = new(capacity: endIndex - _fetchPageIndex);
                for (; _fetchPageIndex < endIndex; _fetchPageIndex++)
                {
                    batch.Add(_playQueue[_fetchPageIndex]);
                }
            }
            finally
            {
                _fetchPageLock.Release();
            }

            var batchStartIndex = _fetchPageIndex - batch.Count;

            var maxConcurrent = _settingsManager.Current.Fetch.MaxConcurrentFetches;
            maxConcurrent = Math.Max(1, maxConcurrent);
            using var semaphore = new SemaphoreSlim(maxConcurrent);

            var tasks = batch.Select(async (queue, i) =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var info = await _fetchManager.ResolveMetadataAsync(queue, ct);
                    return (index: i, info);
                }
                finally { semaphore.Release(); }
            }).ToList();

            var buffer = new (bool ready, TrackDisplayItem? item)[batch.Count];
            int nextOut = 0;

            await foreach (var task in Task.WhenEach(tasks).WithCancellation(ct))
            {
                var (i, info) = await task;
                var queue = batch[i];
                int trackIndex = batchStartIndex + i;

                buffer[i] = (ready: true, item: info is null ? null : new TrackDisplayItem
                {
                    Entry = queue,
                    Title = info.Title,
                    OrderNum = trackIndex + 1,
                    Artist = info.Artist,
                    ThumbnailUrl = info.Fetch?.ThumbnailUrl,
                    Duration = info.Duration,
                    IsFromFlattenedPlaylist = queue.IsFromPlaylist,
                    IsCurrent = _playingIndex == trackIndex
                });

                if (info is not null)
                    _prefetchedMetadata[queue.Url] = info;

                while (nextOut < batch.Count && buffer[nextOut].ready)
                {
                    if (buffer[nextOut].item is { } item)
                        yield return item;
                    nextOut++;
                }
            }

        }

        public async Task<LyricsSet> FetchCurrentTrackLyricsAsync(CancellationToken ct)
        {
            var entry = GetCurrentTrack();

            if(entry != QueueEntry.None)
                return await _fetchManager.ResolveLyricsAsync(entry,ct);

            return LyricsSet.Empty;
        }

        public async Task<bool> TryRemoveQueue(int index , CancellationToken ct)
        {
            if (_playQueue is null || index < 0 || index >= _playQueue.Count)
                return false;

            _playQueue.RemoveAt(index);

            if(_fetchPageIndex > index)
                _fetchPageIndex--;

            if (index == _playingIndex)
                await InvokeTrackChangedEventAsync(ct);

            return true;
        }

        public QueueEntry GetCurrentTrack()
        {
            if (_playQueue is null || _playingIndex < 0 || _playingIndex >= _playQueue.Count)
                return QueueEntry.None;
            return _playQueue[_playingIndex];
        }   

        private async Task PreviewTrackAsync(QueueEntry entry, CancellationToken ct)
        {
            var info = await _fetchManager.ResolveMetadataAsync(entry, ct);
            if (info is null) return;

            _currentTrack = info;
            InvokeTrackChangeEvent(info);
        }


        private async Task InvokeTrackChangedEventAsync(CancellationToken ct)
            => InvokeTrackChangeEvent(await ResolveAudioInfoAsync(_playingIndex<_playQueue.Count?_playQueue[_playingIndex] : QueueEntry.None , ct));
        private void InvokeTrackChangeEvent(AudioInfo? info)
        {
            _mediaControls.UpdateNowPlaying(info);

            if (info is null)
            {
                this.TrackChanged?.Invoke(new TrackDisplayItem()
                {
                    Entry = QueueEntry.None,
                    Title = string.Empty,
                    Artist = string.Empty,
                    ThumbnailUrl = null,
                    Duration = TimeSpan.Zero,
                    IsFromFlattenedPlaylist = false,
                    IsCurrent = true,
                    OrderNum = _playingIndex
                });
                return;
            }

            if (_playQueue is null)
                return;

            var queue = _playQueue[_playingIndex];

            this.TrackChanged?.Invoke(new TrackDisplayItem()
            {
                Entry = queue,
                Title = info?.Title ?? queue.Url,
                Artist = info?.Artist ?? string.Empty,
                ThumbnailUrl = info?.Fetch.ThumbnailUrl,
                Duration = info?.Duration ?? TimeSpan.Zero,
                IsFromFlattenedPlaylist = queue.IsFromPlaylist,
                IsCurrent = true,
                OrderNum = _playingIndex 
            });
        }

        private async Task RunPlaybackLoopAsync()
        {
            try
            {
                while (!_stopCts.IsCancellationRequested && _playQueue!=null && _playingIndex < _playQueue.Count)
                {
                    _skipCts = new CancellationTokenSource();
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                        _skipCts.Token, _stopCts.Token);

                    await PlayCurrentTrackAsync(linked.Token);

                    if (_stopCts.IsCancellationRequested) break;

                    if(_isDirectJump)
                    {
                        _isDirectJump = false;
                        continue;
                    }

                    if (_repeatMode != RepeatMode.RepeatOne) _playingIndex++;
                    if (_repeatMode == RepeatMode.Repeat && _playingIndex >= _playQueue.Count)
                        _playingIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Playlist] Exception occurs at RunPlaybackLoopAsync：{ex}");
            }
            finally
            {
                await StopAsync(CancellationToken.None);
            }
        }

        private async Task<AudioInfo?> ResolveAudioInfoAsync(QueueEntry entry, CancellationToken ct)
        {
            if(entry == QueueEntry.None)
                return null;
            if (!_prefetchedMetadata.TryGetValue(entry.Url, out var info))
                info = await _fetchManager.ResolveMetadataAsync(entry, ct);
            return info;
        }

        private async Task PlayCurrentTrackAsync(CancellationToken ct)
        {
            try
            {
                var entry = _playQueue[_playingIndex];

                AudioInfo? info = await ResolveAudioInfoAsync(entry, ct);

                if (info is null)
                {
                    Console.WriteLine($"[Playlist] Cannot fetch URL , skipping：{entry.Url}");
                    return;
                }

                _currentTrack = info;
                InvokeTrackChangeEvent(_currentTrack);

                _currentAudio = await _audioPipeline.OpenAsync(info, ct);
                await StreamAudioAsync(_currentAudio, ct);
            }
            finally
            {
                _currentAudio?.Dispose();
                _currentAudio = null;
            }
        }

        private async Task StreamAudioAsync(ChunkedAudioStream currentAudio , CancellationToken ct)
        {
            try
            {
                await _pcmPlayer.StreamAsync(currentAudio, ct);
            }
            catch (OperationCanceledException)  { }
            finally
            {
                _pcmPlayer.Stop();
            }
        }

        private void TogglePause()
        {
            if (IsPaused)
            {
                _pcmPlayer?.Resume();
            }
            else
            {
                _pcmPlayer?.Pause();
            }
            IsPaused = !IsPaused;
        }

        private void ShufflePlaylist(bool respectCurrentTrack)
        {
            _shuffledQueue = _originalQueue.ToList();

            if (_shuffledQueue.Count == 0)
            {
                _playingIndex = 0;
                _fetchPageIndex = 0;
                return;
            }

            if (respectCurrentTrack)
            {
                var currentEntry = _originalQueue[_playingIndex];
                _shuffledQueue.RemoveAt(_playingIndex);
                _shuffledQueue.Insert(0, currentEntry);

                var remainingItems = CollectionsMarshal.AsSpan(_shuffledQueue).Slice(1);
                Random.Shared.Shuffle(remainingItems);

                _playingIndex = 0;
            }
            else
            {
                var allItems = CollectionsMarshal.AsSpan(_shuffledQueue);
                Random.Shared.Shuffle(allItems);

                _playingIndex = 0;
            }

            _fetchPageIndex = 0;
        }


        private void DisableShuffle()
        {
            if (_originalQueue.Count < 1)
                return;

            var index = _originalQueue.IndexOf(_playQueue[_playingIndex]);
            var span = CollectionsMarshal.AsSpan(_originalQueue);
            span.Slice(0, index).Reverse();
            span.Slice(index).Reverse();
            span.Reverse();

            _shuffledQueue.Clear();
            _playingIndex = 0;
            _fetchPageIndex = 0;
        }


        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposeGuard, 1, 0) != 0) return;

            await StopAsync(CancellationToken.None);
            _pcmPlayer?.Dispose();
        }

        public TimeSpan CurrentPosition => _pcmPlayer?.CurrentPosition ?? TimeSpan.Zero;
        public TimeSpan TotalDuration => _currentTrack?.Duration ?? TimeSpan.Zero;
    }
}
