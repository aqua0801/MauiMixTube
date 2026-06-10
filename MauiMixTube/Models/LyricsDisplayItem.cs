using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models
{
    public partial class LyricsDisplayItem : ObservableObject
    {
        public string Content { get; set; }
        public double Offset { get; set; }
        public double Duration { get; set; }

        [ObservableProperty] public partial bool IsCurrent { get; set; }
    }
}
