using MauiMixTube.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Managers.Media
{
    public interface IMediaControlsService
    {
        void UpdateNowPlaying(AudioInfo? info);
        void SetPlaybackStatus(bool isPlaying);
        void Initialize();
    }
}
