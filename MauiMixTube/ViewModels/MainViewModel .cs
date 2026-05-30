using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Extensions;
using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Messages;
using MauiMixTube.Models;
using MauiMixTube.Models.Playlist;
using System.Collections.ObjectModel;

namespace MauiMixTube.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly PlaylistManager _playlistManager;
        private readonly SettingsManager _settingsManager;
        private readonly PlaylistRepository _playlistRepository;

        private readonly PeriodicTimer _progressTimer = new(TimeSpan.FromMilliseconds(500));

        private CancellationTokenSource _loadCts = new();
        private CancellationTokenSource _fetchCts = new();

        public bool IsSliderDragging { get; set; } = false;

        [ObservableProperty] public partial string PlayingFrom { get; set; } = string.Empty;
        [ObservableProperty] public partial string SongTitle { get; set; } = string.Empty;
        [ObservableProperty] public partial string Artist { get; set; } = string.Empty;
        [ObservableProperty] public partial double Progress { get; set; }
        [ObservableProperty] public partial string CurrentTime { get; set; } = "0:00";
        [ObservableProperty] public partial string TotalTime { get; set; } = "0:00";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayPauseIcon))]
        public partial bool IsPlaying { get; set; }
        public string PlayPauseIcon => IsPlaying ? IconFont.Pause : IconFont.Play;

        [ObservableProperty] public partial bool ShuffleOn { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RepeatIcon))]
        [NotifyPropertyChangedFor(nameof(IsRepeatActive))]
        public partial RepeatMode RepeatMode { get; set; }
        public string RepeatIcon => RepeatMode == RepeatMode.RepeatOne ? IconFont.RepeatOne : IconFont.Repeat;
        public bool IsRepeatActive => RepeatMode != RepeatMode.PlayOnce;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LikeIcon))]
        public partial bool IsLiked { get; set; }
        public string LikeIcon => IsLiked ? IconFont.Favorite : IconFont.FavoriteBorder;

        [ObservableProperty] public partial string? AlbumArtUrl { get; set; }
        [ObservableProperty] public partial ObservableCollection<UserPlaylist> Playlists { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<UserPlaylist> FilteredPlaylists { get; set; } = new();

        [ObservableProperty] public partial UserPlaylist LikedSongsPlaylists { get; set; } = new();
        [ObservableProperty] public partial UserPlaylist RecentlyPlayedPlaylists { get; set; } = new();
        [ObservableProperty] public partial UserPlaylist? SelectedPlaylist { get; set; }
        [ObservableProperty] public partial bool IsBusy { get; set; }
        [ObservableProperty] public partial ObservableCollection<TrackDisplayItem> AlbumTracks { get; set; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAlbumTabOpening))]
        [NotifyPropertyChangedFor(nameof(IsLyricsTabOpening))]
        [NotifyPropertyChangedFor(nameof(IsSearchTabOpening))]
        public partial BottomPanelTab ActiveTab { get; set; } = BottomPanelTab.None;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAlbumTabOpening))]
        [NotifyPropertyChangedFor(nameof(IsLyricsTabOpening))]
        [NotifyPropertyChangedFor(nameof(IsSearchTabOpening))]
        public partial bool IsBottomExpanderExpanded { get; set; }
        public ObservableCollection<TrackDisplayItem> FilteredAlbumTracks { get; } = new();

        public bool IsFetchingTracks;

        public bool IsAlbumTabOpening => ActiveTab == BottomPanelTab.Album && IsBottomExpanderExpanded;
        public bool IsLyricsTabOpening => ActiveTab == BottomPanelTab.Lyrics && IsBottomExpanderExpanded;
        public bool IsSearchTabOpening => ActiveTab == BottomPanelTab.Search && IsBottomExpanderExpanded;

        [ObservableProperty] public partial string GlobalSearchQuery { get; set; } = String.Empty;
        [ObservableProperty] public partial string SidebarSearchQuery { get; set; } = String.Empty;

        partial void OnActiveTabChanged(BottomPanelTab value) => OnOpeningStatusChanged();
        partial void OnIsBottomExpanderExpandedChanged(bool value) => OnOpeningStatusChanged();

        public double Volume
        {
            get => _settingsManager.Volume;
            set
            {
                _settingsManager.Volume = value;
            }
        }

        public MainViewModel(SettingsManager settingsManager, PlaylistManager playlistManager, PlaylistRepository playlistRepository)
        {
            _settingsManager = settingsManager;
            _playlistManager = playlistManager;
            _playlistRepository = playlistRepository;

            _ = StartProgressLoopAsync();
            _ = LoadPlayListAsync();

            _playlistManager.TrackChanged += info =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    SongTitle = info.Title;
                    Artist = info.Artist;
                    AlbumArtUrl = info.ThumbnailUrl;
                    TotalTime = FormatTime(info.Duration);
                    Progress = 0;

                    IsLiked = LikedSongsPlaylists.Sources
                        .Any(s=>!s.IsPlaylist &&
                                s.Url == info.Entry.Url &&
                                s.Tag == info.Entry.Tag);

                    if(IsPlaying)
                    {
                        await AddToRecentlyPlayedAsync(info.Entry);
                    }

                    if(info.IsCurrent && AlbumTracks.Count > info.OrderNum)
                    {
                        for(int i=0;i<AlbumTracks.Count;i++)
                            AlbumTracks[i].IsCurrent = i == info.OrderNum;
                    }
                });
            };

            _playlistManager.QueueReset += () =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _loadCts.TryReset();
                    _fetchCts.TryReset();
                    await ResetAlbumTracksAndTryLoadAsync();
                });
            };

            WeakReferenceMessenger.Default.Send(new ToastMessage("Successfully loaded !"));
        }

        private async Task LoadPlayListAsync()
        {
            var playlists =  _playlistRepository.GetAll().Where(p => !p.IsSystemPlaylist);

            foreach (var playlist in playlists)
                playlist.IsActive = false;

            Playlists = new ObservableCollection<UserPlaylist>(playlists);
            FilteredPlaylists = new ObservableCollection<UserPlaylist>(playlists);
            LikedSongsPlaylists = _playlistRepository.GetLikedSongs();
            RecentlyPlayedPlaylists = _playlistRepository.GetRecentlyPlayed();
        }

        private async Task ResetAlbumTracksAndTryLoadAsync()
        {
            AlbumTracks.Clear();
            if (IsAlbumTabOpening)
                await FetchNextPageAsync();
        }

        private async Task StartProgressLoopAsync(CancellationToken ct = default)
        {
            while (await _progressTimer.WaitForNextTickAsync(ct))
            {
                if(!IsSliderDragging)
                {
                    var ratio = _playlistManager.TotalDuration.TotalSeconds != 0 ? 
                        _playlistManager.CurrentPosition.TotalSeconds / _playlistManager.TotalDuration.TotalSeconds : 
                        0;
                    Progress = Math.Clamp(ratio, 0, 1);
                }

                CurrentTime = FormatTime(_playlistManager.CurrentPosition);
            }
        }

        private async Task AddToRecentlyPlayedAsync(QueueEntry entry)
        {
            var existing = RecentlyPlayedPlaylists.Sources
                .FirstOrDefault(s=>s.Url==entry.Url && s.Tag==entry.Tag);

            if(existing is not null)
                RecentlyPlayedPlaylists.Sources.Remove(existing);

            RecentlyPlayedPlaylists.Sources.Insert(0, new PlaylistSource
            {
                Tag = entry.Tag,
                Url = entry.Url,
                IsPlaylist = false
            });


            while (RecentlyPlayedPlaylists.Sources.Count > _settingsManager.RecentlyPlayedCount)
                RecentlyPlayedPlaylists.Sources.RemoveAt(RecentlyPlayedPlaylists.Sources.Count-1);

            _playlistRepository.Update(RecentlyPlayedPlaylists);
        }


        private void OnOpeningStatusChanged()
        {
            OpeningStatusChangedCommand.Execute(null);
        }

        partial void OnIsLikedChanged(bool value)
        {
            var track = _playlistManager.GetCurrentTrack();

            if (track == QueueEntry.None)
                return;

            if (LikedSongsPlaylists.Sources
                .Any(s => s.Url == track.Url &&
                        s.Tag == track.Tag))
                return;

            if (value)
            {
                var source = new PlaylistSource
                {
                    IsPlaylist = false,
                    Url = track.Url,
                    Tag = track.Tag
                };

                LikedSongsPlaylists.Sources.Add(source);
            }
            else
                LikedSongsPlaylists.Sources.RemoveAll(s=>s.Url==track.Url && s.Tag == track.Tag);
             
            
            _playlistRepository.Update(LikedSongsPlaylists);
        }

        partial void OnSelectedPlaylistChanged(UserPlaylist? value)
        {
            if (value == null) return;

            SelectPlaylistCommand.Execute(value);
        }

        partial void OnGlobalSearchQueryChanged(string value)
        {
            ActiveTab = String.IsNullOrWhiteSpace(value)?
                BottomPanelTab.None : BottomPanelTab.Search;

            if(ActiveTab != BottomPanelTab.Search)
            {
                IsBottomExpanderExpanded = false;
                FilteredAlbumTracks.Clear();
                return;
            }

            IsBottomExpanderExpanded = true;

            var results = AlbumTracks.Select(t => 
                    (Score : Math.Max(FuzzySearch.HybridScoreSimilarity(value, t.Title) ,
                              FuzzySearch.HybridScoreSimilarity(value, t.Artist)) ,
                     Track : t))
                    .Where(t=>t.Score > 0.5)
                    .OrderByDescending(t=>t.Score)
                    .Select(t=>t.Track)
                    .ToList();

            FilteredAlbumTracks.Clear();
            foreach (var result in results)
                FilteredAlbumTracks.Add(result);
        }

        partial void OnSidebarSearchQueryChanged(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                FilteredPlaylists.Clear();
                foreach(var playlist in Playlists)
                    FilteredPlaylists.Add(playlist);
                return;
            }

            var results = Playlists.Select(p => (
                 Score : FuzzySearch.HybridScoreSimilarity(value , p.Name),
                 Playlists : p))
                .Where(p=>p.Score > 0.5)
                .OrderByDescending(p=>p.Score)
                .Select(p=>p.Playlists)
                .ToList();

            FilteredPlaylists.Clear();
            foreach(var result in results)
                FilteredPlaylists.Add(result);
        }

        [RelayCommand]
        private async Task RecentlyPlayedTappedAsync()
        {
            await SelectPlaylistAsync(RecentlyPlayedPlaylists);
        }

        [RelayCommand]
        private async Task LikedSongsTappedAsync()
        {
            await SelectPlaylistAsync(LikedSongsPlaylists);
        }

        [RelayCommand]
        private async Task OpeningStatusChangedAsync()
        {
            if(IsAlbumTabOpening)
            {
                if (AlbumTracks.Count < 1)
                    await FetchNextPageAsync();
            }
        }

        [RelayCommand]
        private void SwitchTab(string tab)
            => ActiveTab = Enum.Parse<BottomPanelTab>(tab);

        [RelayCommand]
        private async Task PlayPauseAsync()
        {
            IsPlaying = await _playlistManager.TogglePlayPauseAsync();
        }

        [RelayCommand]
        private async Task ShuffleAsync()
        {
            ShuffleOn = _playlistManager.ToggleShuffle();
        }

        [RelayCommand]
        private async Task RepeatAsync()
        {
            RepeatMode = _playlistManager.ToggleRepeat();
        }

        [RelayCommand]
        private async Task LikeAsync()
        {
            IsLiked = !IsLiked;
        }


        [RelayCommand]
        private async Task SelectPlaylistAsync(UserPlaylist playlist)
        {
            try
            {
                IsBusy = true;
                PlayingFrom = playlist.Name;

                if (IsPlaying)
                    IsPlaying = await _playlistManager.TogglePlayPauseAsync();

                await _playlistManager.LoadAsync(playlist, _loadCts.Token);

                foreach (var p in Playlists)
                    p.IsActive = p.Id == playlist.Id;

                SelectedPlaylist = playlist;
                WeakReferenceMessenger.Default.Send(new CloseSidebarMessage());
            }
            catch (Exception ex)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage($"Failed to load playlist : {ex.Message}"));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task FetchNextPageAsync()
        {
            if (!IsFetchingTracks)
            {
                try
                {
                    IsFetchingTracks = true;
                    await foreach (var info in _playlistManager.FetchNextPageAsync(_fetchCts.Token))
                    {
                        AlbumTracks.Add(info);
                    }
                }
                finally
                {
                    IsFetchingTracks = false;
                }
            }
        }

        [RelayCommand]
        private async Task NextAsync() => _playlistManager.Skip();

        [RelayCommand]
        private async Task PreviousAsync() => _playlistManager.Previous();

        [RelayCommand]
        private async Task PlayTrackAsync(TrackDisplayItem track)
        {
            await _playlistManager.JumpToIndexAsync(track.OrderNum-1 , new CancellationToken());
        }


        public async Task LoadPlaylistsAsync()
        {
            _ = ResolveThumbnailsAsync(Playlists);
        }

        private async Task ResolveThumbnailsAsync(
            IEnumerable<UserPlaylist> playlists)
        {
            foreach (var playlist in playlists.Where(p => string.IsNullOrEmpty(p.ThumbnailUrl)))
            {
                playlist.ThumbnailUrl = await _playlistRepository.ResolveThumbnailAsync(playlist);
                _playlistRepository.Update(playlist);
            }
        }

        public async void HandleNewPlaylist(string name)
        {
            var newPlaylist = new UserPlaylist
            {
                Name = name,
                CreatedAt = DateTime.Now,
                Sources = new List<PlaylistSource>(),
                ThumbnailUrl = null,
                IsActive = false,
                Type = PlaylistType.UserDefined
            };
            _playlistRepository.Add(newPlaylist);
            Playlists.Add(newPlaylist);
        }

        public async void HandleNewSource(PlaylistSource source)
        {
            if (SelectedPlaylist != null)
            {
                SelectedPlaylist.Sources.Add(source);
                _playlistRepository.Update(SelectedPlaylist);

                if(SelectedPlaylist.Sources.Count==1)
                {
                    await ResolveThumbnailsAsync(new[] { SelectedPlaylist });
                }

                await _playlistManager.EnqueueAsync(
                    new QueueEntry(source.Tag, source.Url, source.IsPlaylist),
                    CancellationToken.None);
            }
        }

        public async void HandleUpdatePlaylist(UserPlaylist playlist)
        {
            _playlistRepository.Update(playlist);
        }

        public async void HandleDeletePlaylist(UserPlaylist playlist)
        {
            _playlistRepository.Remove(playlist.Id);
            var toRemove = Playlists.FirstOrDefault(p => p.Id == playlist.Id);
            if (toRemove != null)
                Playlists.Remove(toRemove);
        }

        public async void HandleSliderDrag(double dragValue)
        {
            var destSeek = _playlistManager.TotalDuration * dragValue;
            await _playlistManager.SeekAsync(destSeek);
        }

        public async void HandleRemoveFromPlaylist(TrackDisplayItem item)
        {
            HandleRemoveFromQueue(item);
            if(SelectedPlaylist is not null)
            {
                var removeIndex = SelectedPlaylist.Sources.FindIndex(s => !s.IsPlaylist && s.Url == item.Entry.Url);

                if(removeIndex!=-1)
                {
                    SelectedPlaylist.Sources.RemoveAt(removeIndex);

                    if(removeIndex==0)
                    {
                        await _playlistRepository.ClearThumbnailUrlAsync(SelectedPlaylist , CancellationToken.None);
                        await ResolveThumbnailsAsync(new[] { SelectedPlaylist });
                    }
                }
            }
        }

        public async void HandleRemoveFromQueue(TrackDisplayItem item)
        {
            var success = await _playlistManager.TryRemoveQueue(item.OrderNum - 1 , CancellationToken.None);

            if(success)
            {
                AlbumTracks.Remove(item);
                for(int i=item.OrderNum - 1; i < AlbumTracks.Count; i++)
                {
                    AlbumTracks[i].OrderNum--;
                }
            }
        }

        private static string FormatTime(TimeSpan t)
            => t.ToString(t.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");
    }
}
