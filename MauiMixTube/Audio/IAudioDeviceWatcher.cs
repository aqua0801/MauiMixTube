using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Audio
{
    public interface IAudioDeviceWatcher
    {
        void StartWatching();
        void StopWatching();
    }
}
