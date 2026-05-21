using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models
{
    public readonly record struct QueueEntry(WebTag Tag, string Url , bool IsFromPlaylist)
    {
        public static readonly QueueEntry None = new QueueEntry(WebTag.None, string.Empty, false);
    }
}
