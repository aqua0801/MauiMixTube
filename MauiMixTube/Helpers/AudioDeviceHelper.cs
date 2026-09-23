using MauiMixTube.Audio;
using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Helpers
{
    public static class AudioDeviceHelper
    {
        public static string CleanDeviceName(string rawName)
        {
            const string separator = " on ";
            int index = rawName.LastIndexOf(separator, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? rawName[(index + separator.Length)..].Trim() : rawName;
        }

        public static IReadOnlyList<string> GetAvailableDevices()
            => PcmPlayer.GetAvailableDevices()
                .Select(CleanDeviceName)
                .ToList();
    }
}
