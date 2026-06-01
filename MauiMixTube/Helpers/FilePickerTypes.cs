using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Helpers
{
    public static class FilePickerTypes
    {
        public static readonly FilePickerFileType Playlist = new(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
            { DevicePlatform.WinUI,   new[] { ".json" } },
            { DevicePlatform.Android, new[] { "application/json" } },
            { DevicePlatform.iOS,     new[] { "public.json" } },
            { DevicePlatform.MacCatalyst, new[] { "public.json" } }
            });

    }
}
