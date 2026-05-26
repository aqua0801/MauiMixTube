using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Models;

namespace MauiMixTube.Audio
{
    public sealed class AudioPipeline
    {
        private readonly CacheManager _cacheManager;
        private readonly FetchManager _fetchManager;
        private readonly SettingsManager _settingsManager;

        public AudioPipeline(CacheManager cacheManager ,FetchManager fetchManager , SettingsManager settingsManager)
        {
            _cacheManager = cacheManager;
            _fetchManager = fetchManager;
            _settingsManager = settingsManager;
        }

        public async Task<ChunkedAudioStream> OpenAsync(AudioInfo info, CancellationToken ct)
        {
            var service = _fetchManager.ResolveService(info.Tag,info.OriginalUrl);

            if (service.IsUrlExpired(info.Fetch.FetchedAt))
            {
                info.Fetch.SourceUrl = await service.RefreshSourceUrlAsync(info.OriginalUrl, ct);
                info.Fetch.FetchedAt = DateTime.UtcNow;
                await _cacheManager.StoreMetadataAsync(info, ct);
            }

            info.Fetch.FfmpegArgs = _settingsManager.LoudnessNormEnabled ? 
                service.GetFfmpegArgsWithLoudnorm() : 
                service.GetFfmpegArgs();
            info.Fetch.CacheArgs = service.GetCacheArgs();

            await _cacheManager.TrySetCachePathAsync(info , ct);

            if (info.Fetch.IsCached)
                return new ChunkedAudioStream(info);

            var key = CacheManager.ToKey(info);
            var finalPath = Path.Combine(AppPaths.CachedFilesDir, $"{key}.webm");

            info.Fetch.CacheWritePath = finalPath;

            var chunked = new ChunkedAudioStream(info);
            _cacheManager.Attach(chunked,finalPath,ct);
          
            return chunked;
        }
    }
}
