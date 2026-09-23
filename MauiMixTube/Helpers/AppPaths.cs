
namespace MauiMixTube.Helpers
{
    public static class AppPaths
    {
        public static string BaseDir => FileSystem.Current.AppDataDirectory;

        public static string SettingsFile => Path.Combine(BaseDir, "settings.json");
        public static string PlaylistsDir => Path.Combine(BaseDir, "Playlists");
        public static string CookieFile => Path.Combine(BaseDir, "cookies.txt");
        public static string UserAgentFile => Path.Combine(BaseDir, "user_agent.txt");
        public static string AutoEqDir => Path.Combine(BaseDir, "autoeq.txt");
        public static string CacheDir => Path.Combine(BaseDir, "Cache");
        public static string CachedFilesDir => Path.Combine(CacheDir, "Audio");
        public static string MetaCacheDir => Path.Combine(CacheDir, "Metadata");
        public static string CacheIndex => Path.Combine(CacheDir, "index.json");
        public static string BinDir => Path.Combine(BaseDir, "bin");
        public static string PlaylistIndex => Path.Combine(PlaylistsDir, "index.json");
        public static string LikedFile => Path.Combine(PlaylistsDir, "liked.json");
        public static string LanguagesDir => Path.Combine(BaseDir, "Languages");

        public static string YtDlpBinary => Path.Combine(BinDir,
            OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp");
        public static string FfmpegBinary => Path.Combine(BinDir,
            OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");

        public static string FfprobeBinary => Path.Combine(BinDir,
            OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        public static async Task EnsureDirectories()
        {
            Directory.CreateDirectory(BinDir);
            Directory.CreateDirectory(PlaylistsDir);
            Directory.CreateDirectory(CacheDir);
            Directory.CreateDirectory(CachedFilesDir);
            Directory.CreateDirectory(MetaCacheDir);
            Directory.CreateDirectory(LanguagesDir);

            await ExtractBinaryAsync(Path.Combine("Languages", "en-US.json"),Path.Combine(LanguagesDir, "en-US.json"));
            await ExtractBinaryAsync(Path.Combine("Languages", "zh-TW.json"), Path.Combine(LanguagesDir, "zh-TW.json"));
            await ExtractBinaryAsync("ffmpeg.exe", FfmpegBinary);
            await ExtractBinaryAsync("ffprobe.exe", FfprobeBinary);
            await ExtractBinaryAsync("openal32.dll", "openal32.dll");
        }

        private static async Task ExtractBinaryAsync(string assetName, string targetPath)
        {
            await using var asset = await FileSystem.OpenAppPackageFileAsync(assetName);

            if (File.Exists(targetPath))
            {
                await using var existing = File.OpenRead(targetPath);
                if (existing.Length == asset.Length)
                {
                    return;
                }
            }

            await using var dest = File.Create(targetPath);
            await asset.CopyToAsync(dest);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(targetPath,
                    UnixFileMode.UserExecute | UnixFileMode.UserRead |
                    UnixFileMode.GroupExecute | UnixFileMode.GroupRead);
            }
        }
    }
}
