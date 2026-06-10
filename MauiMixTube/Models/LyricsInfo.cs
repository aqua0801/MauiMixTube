using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models
{
    public record LyricsInfo(string Content , double OffsetSec , double DurationSec);

    public class LyricsSet
    {
        public required LyricsInfo[] Lyrics { get; init; }
        public bool HasLyrics => Lyrics.Length > 0;

        public static LyricsSet Empty => new()
        {
            Lyrics = Array.Empty<LyricsInfo>()
        };
    }
}
