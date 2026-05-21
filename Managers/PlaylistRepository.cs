using MauiMixTube.Helper;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Models;
using MauiMixTube.Models.Playlist;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiMixTube.Managers
{
    public partial class PlaylistRepository
    {
        private List<UserPlaylist> _playlists = new();
        private List<LikedTrack> _liked = new();

        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly FetchManager _fetchManager;
        private readonly CacheManager _cacheManager;

        public PlaylistRepository(FetchManager fetchManager , CacheManager cacheManager)
        {
            _fetchManager = fetchManager;
            _cacheManager = cacheManager;
        }

        public async Task InitializeAsync()
        {
            _playlists = await LoadAsync<List<UserPlaylist>>(AppPaths.PlaylistIndex)
                         ?? new();
            _liked = await LoadAsync<List<LikedTrack>>(AppPaths.LikedFile)
                         ?? new();
        }

        public IReadOnlyList<UserPlaylist> GetAll() => _playlists;

        public void Add(UserPlaylist playlist)
        {
            _playlists.Add(playlist);
            _ = SavePlaylistsAsync();
        }

        public void Remove(string id)
        {
            _playlists.RemoveAll(p => p.Id == id);
            _ = SavePlaylistsAsync();
        }

        public void Update(UserPlaylist playlist)
        {
            var index = _playlists.FindIndex(p => p.Id == playlist.Id);
            if (index < 0) return;
            _playlists[index] = playlist;
            _ = SavePlaylistsAsync();
        }

        public bool IsLiked(string normalizedUrl)
            => _liked.Any(t => t.Url == normalizedUrl);

        public async Task ToggleLikeAsync(string normalizedUrl, string title, string artist, WebTag tag)
        {
            if (IsLiked(normalizedUrl))
                _liked.RemoveAll(t => t.Url == normalizedUrl);
            else
                _liked.Add(new LikedTrack
                {
                    Url = normalizedUrl,
                    Tag = tag,
                    Title = title,
                    Artist = artist
                });

            await SaveLikedAsync();
        }

        public IReadOnlyList<LikedTrack> GetLiked() => _liked;

        private async Task<T?> LoadAsync<T>(string path)
        {
            if (!File.Exists(path)) return default;
            try
            {
                await using var stream = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<T>(
                    stream, PlaylistJsonContext.Default.Options);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[PlaylistStore] Failed to load , rebuilding：{ex.Message}");
                return default;
            }
        }

        public async Task ClearThumbnailUrlAsync(
            UserPlaylist playlist ,
            CancellationToken ct = default)
        {
            playlist.ThumbnailUrl = null;
            Update(playlist);
        }


        public async Task<string?> ResolveThumbnailAsync(
            UserPlaylist playlist, CancellationToken ct = default)
        {
            if (playlist.ThumbnailUrl is not null)
                return playlist.ThumbnailUrl;

            if(playlist.Sources.Count < 1)
                return null;

            var firstSource = playlist.Sources.First();

            PlaylistSource? firstSingle = null;

            if (firstSource.IsPlaylist)
            {
                var firstPlaylist = playlist.Sources.FirstOrDefault(s => s.IsPlaylist);
                if (firstPlaylist is null) return null;

                var entries = await _fetchManager.ExpandPlaylistAsync(firstPlaylist, ct);
                var first = entries
                     .Cast<QueueEntry?>()
                    .FirstOrDefault();

                if (first is null) return null;

                firstSingle = new PlaylistSource
                {
                    Tag = first.Value.Tag,
                    Url = first.Value.Url,
                    IsPlaylist = false
                };
            }
            else
                firstSingle = firstSource;

            var info = await _fetchManager.ResolveMetadataAsync(
                new QueueEntry(firstSingle.Tag, firstSingle.Url,false), ct);

            if (info?.Fetch.ThumbnailUrl is null) return null;

            playlist.ThumbnailUrl = info.Fetch.ThumbnailUrl;
            Update(playlist);

            return playlist.ThumbnailUrl;
        }

        private Task SavePlaylistsAsync()
            => SaveAsync(_playlists, AppPaths.PlaylistIndex);

        private Task SaveLikedAsync()
            => SaveAsync(_liked, AppPaths.LikedFile);

        private async Task SaveAsync<T>(T data, string path)
        {
            await _saveLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(data,
                    PlaylistJsonContext.Default.Options);
                await File.WriteAllTextAsync(path, json);
            }
            finally
            {
                _saveLock.Release();
            }
        }


        [JsonSerializable(typeof(List<UserPlaylist>))]
        [JsonSerializable(typeof(List<LikedTrack>))]
        [JsonSourceGenerationOptions(
            PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
            WriteIndented = true)]
        public partial class PlaylistJsonContext : JsonSerializerContext { }

    }
}
