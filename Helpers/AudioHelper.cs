using MauiMixTube.Helper;
using System.Diagnostics;
using System.Text.Json;

namespace MauiMixTube.Helpers
{
    public static class AudioHelper
    {
        public static async Task<TimeSpan?> GetAudioDurationAsync(string url, string headerArgument = "")
        {
            var psi = new ProcessStartInfo
            {
                FileName = AppPaths.FfprobeBinary,
                Arguments = $"-v quiet -print_format json -show_format {headerArgument} -i \"{url}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi);
                using var reader = process.StandardOutput;
                string output = await reader.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (string.IsNullOrWhiteSpace(output)) return null;

                var json = JsonDocument.Parse(output);
                if (json.RootElement.TryGetProperty("format", out var format)
                    && format.TryGetProperty("duration", out var durationElement)
                    && double.TryParse(durationElement.GetString(), out var durationSec))
                {
                    return TimeSpan.FromSeconds(durationSec);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[AudioHelper] Error occurred while getting audio duration: {ex.Message}");
            }

            return null;
        }
    }
}
