using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models.Settings
{
    public class FetchSettings
    {
        public int MaxConcurrentFetches { get; set; } = 3;
        public int FetchPageSize { get; set; } = 20;
        public int MaxRetryAttempts { get; set; } = 3;
        public FetchQuality Quality { get; set; } = FetchQuality.Best;
    }
}
