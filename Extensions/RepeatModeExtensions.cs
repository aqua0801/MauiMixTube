using MauiMixTube.Models;

namespace MauiMixTube.Extensions
{
    public static class RepeatModeExtensions
    {
        public static RepeatMode Next(this RepeatMode mode)
        {
            return mode switch
            {
                RepeatMode.PlayOnce => RepeatMode.Repeat,
                RepeatMode.Repeat => RepeatMode.RepeatOne,
                RepeatMode.RepeatOne => RepeatMode.PlayOnce,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Unexpected value: {mode}"),
            };
        }
    }

}

