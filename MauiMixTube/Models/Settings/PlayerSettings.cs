using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models.Settings
{
    public class PlayerSettings
    {
        public double Volume { get; set; } = 100.0;
        public int RecentlyPlayedCount { get; set; } = 50;
        public bool LoudnessNormEnabled { get; set; } = true;
        public bool AutoEqEnabled { get; set; } = false;
        public string EqDeviceName { get; set; } = String.Empty;
        public string AudioDeviceName { get; set;  } = String.Empty;
        public bool AlcReopenEnabled { get; set; } = true;
    }

}
