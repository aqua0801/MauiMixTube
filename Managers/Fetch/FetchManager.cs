using MauiMixTube.Helpers;
using MauiMixTube.Models;
using MauiMixTube.Models.Fetch;
using MauiMixTube.Models.Playlist;
using YoutubeDLSharp;

namespace MauiMixTube.Managers.Fetch
{
    public sealed class FetchManager
    {
        private readonly CacheManager _cacheManager;
        private readonly List<FetchService> _services = new();
        private readonly YoutubeDL _ytdl;

        public FetchManager(IEnumerable<FetchService> services , CacheManager cacheManager , YoutubeDL ytdl)
        {
            _services = services.ToList();
            _cacheManager = cacheManager;
            _ytdl = ytdl;
        }

        public async Task OnStartupAsync(CancellationToken ct = default)
        {
            try
            {
                Console.WriteLine($"yt-dlp version check : {_ytdl.Version}");
            }
            catch
            {
                Console.WriteLine("yt-dlp is missing , redownloading...");
                await YoutubeDLSharp.Utils.DownloadYtDlp(AppPaths.BinDir);
                _ytdl.YoutubeDLPath = AppPaths.YtDlpBinary;
                _ytdl.FFmpegPath = AppPaths.FfmpegBinary;
            }

            await Task.WhenAll(_services.Select(s => s.OnStartupAsync(ct)));
        }

        public WebTag? DetectTagFromUrl(string url)
            => _services
                .FirstOrDefault(s => s.CanDetectFromUrl(url))
                ?.SupportedTag; 

        public async Task<AudioInfo> ResolveMetadataAsync(QueueEntry entry, CancellationToken ct = default)
        {
            var key = CacheManager.ToKey(entry);

            if (await _cacheManager.GetMetadataAsync(key) is { } cached)
                return cached;

            var tag = entry.Tag;
            var url = entry.Url;

            foreach(var service in _services)
            {
                if(service.CanHandle(tag,url))
                {
                    var info = await service.ResolveAsync(url, ct);
                    if (info is not null)
                    {
                        await _cacheManager.StoreMetadataAsync(key, info, ct);
                        return info;
                    }
                }
            }

            throw new InvalidOperationException($"Unable to resolve audio for URL : {url}");
        }

        public FetchService ResolveService(WebTag tag,string url)
        {
            foreach (var service in _services)
            {
                if (service.CanHandle(tag, url))
                    return service;
            }

            throw new InvalidOperationException($"Unable to resolve fetch service for URL : {url}");
        }

        public async Task<IEnumerable<QueueEntry>> ExpandPlaylistAsync(PlaylistSource source , CancellationToken ct = default)
                => await ExpandPlaylistAsync(new QueueEntry { Tag = source.Tag, Url = source.Url }, ct);

        public async Task<IEnumerable<QueueEntry>> ExpandPlaylistAsync(QueueEntry entry, CancellationToken ct = default)
        {
            var tag = entry.Tag;
            var url = entry.Url;
            foreach (var service in _services)
            {
                if (service.CanHandle(tag, url))
                {
                    var entries = await service.ExpandPlaylistAsync(url, ct);
                    if (entries != null)
                        return entries;
                }
            }
            throw new InvalidOperationException($"Unable to expand playlist for URL: {url}");
        }

    }
}
