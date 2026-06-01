using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MauiMixTube.Helpers
{
    public static class SystemFileExplorer
    {
        public static void Open(string path)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var args = File.Exists(path)
                        ? $"/select,\"{path}\""
                        : $"\"{path}\"";

                    Process.Start("explorer.exe", args);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var args = File.Exists(path)
                        ? $"-R \"{path}\""
                        : $"\"{path}\"";

                    Process.Start("open", args);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var target = File.Exists(path)
                        ? Path.GetDirectoryName(path)!
                        : path;

                    Process.Start("xdg-open", $"\"{target}\"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SystemFileExplorer] Failed to open {path}: {ex.Message}");
            }
        }
    }
}
