using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models.Playlist
{
    public record PlaylistSource
    {
        public WebTag Tag { get; init; }
        public string Url { get; init; } = string.Empty;
        public bool IsPlaylist { get; init; } 
    }
}
