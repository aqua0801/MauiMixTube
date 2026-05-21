
namespace MauiMixTube.Models
{
    public record AudioInfoCache
    {
        public string Title { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public double DurationSec { get; init; }
        public string WebTag { get; set; } = string.Empty;
        public string ThumbnailUrl { get; init; } = string.Empty;
        public string OriginalUrl { get; init; } = string.Empty;
        public string SourceUrl { get; init; } = string.Empty;
        public string CachedFilePath { get; set; } = string.Empty;
        public DateTime FetchedAt { get; init; } = DateTime.UtcNow;

        public static implicit operator AudioInfoCache(AudioInfo info) => new()
        {
            Title = info.Title,
            OriginalUrl = info.OriginalUrl,
            Artist = info.Artist,
            DurationSec = info.Duration.TotalSeconds,
            WebTag = info.Tag,
            ThumbnailUrl = info.Fetch.ThumbnailUrl,
            SourceUrl = info.Fetch.SourceUrl,
            CachedFilePath = info.Fetch.CachedFilePath ?? string.Empty,
            FetchedAt = DateTime.UtcNow
        };

        public static implicit operator AudioInfo(AudioInfoCache cache)
        {
            var info = new AudioInfo()
            {
                Title = cache.Title,
                OriginalUrl = cache.OriginalUrl,
                Artist = cache.Artist,
                Duration = TimeSpan.FromSeconds(cache.DurationSec),
                Tag = WebTagRegistry.GetByName(cache.WebTag),
                Fetch = new FetchInfo 
                { 
                    SourceUrl = cache.SourceUrl ,
                    ThumbnailUrl = cache.ThumbnailUrl ,
                    CachedFilePath = string.IsNullOrEmpty(cache.CachedFilePath)
                                    ? null
                                    : cache.CachedFilePath,
                    FetchedAt = cache.FetchedAt
                },
            };

            return info;
        }
    }
}
