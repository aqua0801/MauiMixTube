
namespace MauiMixTube.Models
{
    public record AudioInfo
    {
        public required string Title { get; init; } = string.Empty;
        public required string OriginalUrl { get; init; } = string.Empty;
        public required string Artist { get; init; } = string.Empty;
        public required TimeSpan Duration { get; init; } = TimeSpan.Zero;
        public required FetchInfo Fetch { get; init; } = new();
        public required WebTag Tag { get; init; } = WebTag.YouTube;
    }
}
