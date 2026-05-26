using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models.Settings
{
    public class PlayerSettings
    {
        public double Volume { get; set; } = 100.0;
        public bool LoudnessNormEnabled { get; set; } = true;
        public bool AutoEqEnabled { get; set; } = false;
        public string DeviceName { get; set; } = String.Empty;
    }

}
