using MauiMixTube.Managers.Media;
using MauiMixTube.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Managers.Media;
{
    public class MediaControlsService : IMediaControlsService
    {
        public void Initialize() { }
        public void UpdateNowPlaying(AudioInfo? info) { }
        public void SetPlaybackStatus(bool isPlaying) { }
    }
}
