
namespace MauiMixTube.Models.Fetch
{
    public abstract class FetchService
    {
        private const double ExpiryBufferRatio = 0.9;

        public virtual bool SupportsLyrics => false;
        public abstract WebTag SupportedTag { get; }

        public virtual Task OnStartupAsync(CancellationToken ct = default)
            => Task.CompletedTask;

        public abstract bool CanHandle(WebTag tag, string url);
        public virtual bool CanDetectFromUrl(string url) => false;

        public async Task<AudioInfo?> ResolveAsync(string url, CancellationToken ct = default)
        {
            url = NormalizeUrl(url);
            return await OnResolveAsync(url, ct);
        }

        public virtual TimeSpan? SourceUrlExpiry => null;

        public virtual async Task<string> RefreshSourceUrlAsync(string url, CancellationToken ct = default)
        {
            var info = await ResolveAsync(url, ct);
            if (info is null)
                throw new InvalidOperationException($"Unable to refresh URL: {url}");
            return info.Fetch.SourceUrl;
        }

        public bool IsUrlExpired(DateTime fetchedAt)
        {
            if (SourceUrlExpiry is null) return false;

            var effectiveExpiry = SourceUrlExpiry.Value * ExpiryBufferRatio;
            return DateTime.UtcNow - fetchedAt > effectiveExpiry;
        }

        protected abstract Task<AudioInfo?> OnResolveAsync(string url, CancellationToken ct = default);

        public async Task<IEnumerable<QueueEntry>> ExpandPlaylistAsync(string url, CancellationToken ct = default)
        {
            url = NormalizeUrl(url);
            return await OnExpandPlaylistAsync(url, ct);
        }

        protected virtual Task<IEnumerable<QueueEntry>> OnExpandPlaylistAsync(string url, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<QueueEntry>());

        public virtual string NormalizeUrl(string url) => url;

        public virtual Task<string?> FetchLyricsAsync(string url, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public virtual FetchInfo.FfmpegArgsDelegate GetFfmpegArgs() => (instance, seek) =>
        {
            var cacheOut = instance.CacheWritePath is not null
                ? $"-map 0:a -c:a libopus -b:a 128k -f webm \"{instance.CacheWritePath}\""
                : string.Empty;

            return $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
                   $"{instance.FfmpegHeader} -ss {seek:F3} -i \"{instance.SourceUrl}\" -vn " +
                   $"-map 0:a -f s16le -ar 48000 -ac 2 pipe:1 " +
                   $"{cacheOut}";
        };

        public virtual FetchInfo.FfmpegArgsDelegate GetFfmpegArgsWithLoudnorm() => (instance, seek) =>
        {
            var loudnorm = "loudnorm=I=-14:TP=-1.5:linear=true";
            var cacheOut = instance.CacheWritePath is not null
                ? $"-map \"[cache]\" -c:a libopus -b:a 128k -f webm \"{instance.CacheWritePath}\""
                : string.Empty;

            return $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
                   $"{instance.FfmpegHeader} -ss {seek:F3} -i \"{instance.SourceUrl}\" -vn " +
                   $"-filter_complex \"[0:a]asplit=2[out1][out2];" +
                   $"[out1]{loudnorm}[live];[out2]{loudnorm}[cache]\" " +
                   $"-map \"[live]\" -f s16le -ar 48000 -ac 2 pipe:1 " +
                   $"{cacheOut}";
        };

        public virtual FetchInfo.FfmpegArgsDelegate GetCacheArgs() => (instance, seek) =>
                $"-ss {seek:F3} -i \"{instance.CachedFilePath}\" " +
                $"-vn -f s16le -ar 48000 -ac 2 pipe:1";
    }
}
