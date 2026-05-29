using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models
{
    public record FetchInfo
    {
        public string SourceUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; init; } = string.Empty;
        public string FfmpegHeader { get; set; } = string.Empty;
        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

        public string CacheWritePath { get; set; } = string.Empty;
        public string? CachedFilePath { get; set; }
        public bool IsCached => CachedFilePath is not null;

        public delegate string FfmpegArgsDelegate(FetchInfo instance, double seekSeconds = 0);

        public FfmpegArgsDelegate FfmpegArgs { get; set; } = (instance, seek) =>
            $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
            $"{instance.FfmpegHeader} -ss {seek:F3} -i \"{instance.SourceUrl}\" -vn " +
            $"-map 0:a -f s16le -ar 48000 -ac 2 pipe:1 " +
            $"-map 0:a -c:a libopus -b:a 128k -f webm \"{instance.CacheWritePath}\"";

        public FfmpegArgsDelegate CacheArgs { get; set; } = (instance, seek) =>
            $"-ss {seek:F3} -i \"{instance.CachedFilePath}\" " +
            $"-vn -f s16le -ar 48000 -ac 2 pipe:1";

        public string GetStreamArgs(double seek = 0) => FfmpegArgs(this, seek);
        public string GetCacheArgsOrDefault(double seek = 0) =>
            IsCached ? CacheArgs(this, seek) : GetStreamArgs(seek);
    }
}
