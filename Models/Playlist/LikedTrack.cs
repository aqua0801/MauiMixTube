namespace MauiMixTube.Models.Playlist
{
    public record LikedTrack
    {
        public string Url { get; init; } = string.Empty;
        public WebTag Tag { get; init; }
        public string Title { get; init; } = string.Empty; 
        public string Artist { get; init; } = string.Empty;
        public DateTime LikedAt { get; init; } = DateTime.UtcNow;
    }
}
