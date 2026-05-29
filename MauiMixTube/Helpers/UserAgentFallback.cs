using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Helpers
{
    public static class UserAgentFallback
    {
        public static string GetPlatformUserAgent()
        {
#if ANDROID
            // Android  Chrome Mobil
            return "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Mobile Safari/537.36";

#elif IOS
        // iOS  Safari Mobile [3]
        return "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1";
        
#elif WINDOWS || NETFX_CORE || WINUI
        // Windows 11 Google Chrome 
        return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
        
#elif MACCATALYST || MACOS
        // macOS Apple Safari 
        return "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15";
        
#elif LINUX
        // Linux Mozilla Firefox 
        return "Mozilla/5.0 (X11; Linux x86_64; rv:124.0) Gecko/20100101 Firefox/124.0";
        
#else
        //  Windows Chrome
        return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
#endif
        }


        public static readonly string[] Presets =
        [
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Safari/604.1",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/123.0.0.0 Safari/537.36"
        ];

        public static string Next(string current)
        {
            var index = Array.IndexOf(Presets, current);
            return Presets[(index + 1) % Presets.Length];
        }

        public static string Default => GetPlatformUserAgent();

    }

}
