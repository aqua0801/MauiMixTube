using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiMixTube.Models.Playlist
{
    public partial class UserPlaylist : ObservableObject
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public List<PlaylistSource> Sources { get; init; } = new();
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = false;

        public PlaylistType Type { get; set; } = PlaylistType.UserDefined;
        public bool IsSystemPlaylist => Type != PlaylistType.UserDefined;

        [ObservableProperty]
        public partial string Name { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? ThumbnailUrl { get; set; }
    }
}
