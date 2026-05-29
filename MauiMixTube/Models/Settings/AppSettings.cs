using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models.Settings
{
    public class AppSettings
    {
        public GeneralSettings General { get; init; } = new();
        public PlayerSettings Player { get; init; } = new();
        public FetchSettings Fetch { get; init; } = new();
        public CacheSettings Cache { get; init; } = new();
    }
}
