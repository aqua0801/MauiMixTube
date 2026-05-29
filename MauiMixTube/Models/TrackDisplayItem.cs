using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiMixTube.Models
{
    public partial class TrackDisplayItem : ObservableObject
    {
        public QueueEntry Entry { get; init; }
        public int OrderNum { get; set; }
        public string Title { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string? ThumbnailUrl { get; init; }
        public TimeSpan Duration { get; init; }
        public bool IsFromFlattenedPlaylist { get; init; }
        [ObservableProperty] public partial bool IsCurrent { get; set; }
    }
}
