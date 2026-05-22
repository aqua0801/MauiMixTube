using Android.Widget;
using MauiMixTube.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace MauiMixTube.Helper
{
    public static class MediaValidator
    {
        private static readonly HttpClient _http = new(new HttpClientHandler { UseCookies = false });

        private static readonly Dictionary<string, string> _supportedVideoCodecs = new()
        {
            ["h264"] = "libx264",
            ["hevc"] = "libx265",
            ["av1"] = "libaom-av1",
            ["vp8"] = "libvpx",
            ["vp9"] = "libvpx-vp9",
            ["mpeg2"] = "mpeg2video",
            ["mpeg4"] = "mpeg4",
            ["theora"] = "libtheora",
            ["prores"] = "prores_ks",
            ["dnxhd"] = "dnxhd",
            ["jpeg"] = "mjpeg",
            ["h263"] = "h263",
            ["wmv3"] = "wmv3",
            ["flv1"] = "flv",
            ["mvc"] = "libx264",
        };

        private static readonly Dictionary<string, string> _supportedAudioCodecs = new()
        {
            ["aac"] = "aac",
            ["mp3"] = "libmp3lame",
            ["opus"] = "libopus",
            ["ac3"] = "ac3",
            ["flac"] = "flac",
            ["vorbis"] = "libvorbis",
            ["pcm"] = "pcm_s16le",
            ["alac"] = "alac",
            ["wma"] = "wmav2",
            ["speex"] = "libspeex",
            ["amr-nb"] = "libamr_nb",
            ["amr-wb"] = "libamr_wb",
        };

        public static async Task<bool> IsValidMediaUrlAsync(
            string url, 
            MediaType type, 
            string? ua = null, 
            string referer = "https://www.google.com",
            CancellationToken ct = default)
        {
            try
            {
                if(type == MediaType.VideoWithAudio)
                    return  await IsValidMediaUrlAsync(url, MediaType.AudioOnly, ua, referer, ct) && 
                            await IsValidMediaUrlAsync(url, MediaType.VideoOnly, ua, referer, ct);

                if (await TryValidateAsync(HttpMethod.Head, url, type, ua, referer, rangeHeader: null, ct))
                    return true;

                return await TryValidateAsync(HttpMethod.Get, url, type, ua, referer,
                    rangeHeader: new RangeHeaderValue(0, 1), ct);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> TryValidateAsync(
            HttpMethod method, string url, MediaType type,
            string? ua, string referer, RangeHeaderValue? rangeHeader, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(ua)) request.Headers.UserAgent.ParseAdd(ua);
            request.Headers.Referrer = new Uri(referer);
            if (rangeHeader is not null) request.Headers.Range = rangeHeader;

            using var response = await _http.SendAsync(request,ct);
            if (!response.IsSuccessStatusCode) return false;

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (!IsMediaContentType(contentType)) return false;

            string headerArg = ConvertHttpRequestToFfmpegHeaderArg(request);
            return await IsMediaSupportedEncoding(url, type, headerArg,ct);
        }

        private static bool IsMediaContentType(string? contentType)
            => !string.IsNullOrEmpty(contentType) &&
               (contentType.StartsWith("video/") || contentType.StartsWith("audio/"));

        private static async Task<bool> IsMediaSupportedEncoding(
            string url, 
            MediaType type, 
            string headerArg,
            CancellationToken ct)
        {
            var encoding = await GetMediaEncodingAsync(url, type, headerArg,ct);
            var supported = type == MediaType.VideoOnly ? _supportedVideoCodecs : _supportedAudioCodecs;
            return supported.ContainsKey(encoding);
        }

        private static async Task<string> GetMediaEncodingAsync(
            string url, 
            MediaType type, 
            string headerArg , 
            CancellationToken ct)
        {
            string stream = type == MediaType.VideoOnly ? "v:0" : "a:0";
            string args = $"-v error -select_streams {stream} -show_entries stream=codec_name " +
                            $"-of default=noprint_wrappers=1:nokey=1 {headerArg} \"{url}\"";

            var psi = new ProcessStartInfo
            {
                FileName = AppPaths.FfprobeBinary,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return "failed";

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return string.IsNullOrWhiteSpace(error) ? output.Trim() : "unknown";
        }

        public static async Task<bool> DownloadAndMergeMediaAsync(
            string videoUrl, 
            string audioUrl, 
            string outputPath, 
            string headerArg = "",
            CancellationToken ct = default)
        {
            string args = $"-y {headerArg} -i \"{videoUrl}\" {headerArg} -i \"{audioUrl}\" " +
                          $"-c:v libx264 -crf 23 -preset fast -c:a aac -b:a 192k -f mp4 \"{outputPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = AppPaths.FfmpegBinary,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                using var process = Process.Start(psi);
                if (process is null) return false;
                await process.WaitForExitAsync(ct);
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] ffmpeg merge failed: {ex.Message}");
                return false;
            }
        }

        public static string ConvertHttpRequestToFfmpegHeaderArg(HttpRequestMessage request)
        {
            var sb = new StringBuilder();
            foreach (var h in request.Headers)
                sb.Append($"{h.Key}: {string.Join(", ", h.Value)}\r\n");
            if (request.Content is not null)
                foreach (var h in request.Content.Headers)
                    sb.Append($"{h.Key}: {string.Join(", ", h.Value)}\r\n");
            return $"-headers \"{sb}\"";
        }

        public static string ConvertHttpClientToFfmpegHeaderArg(HttpClient client)
        {
            var sb = new StringBuilder();
            foreach (var h in client.DefaultRequestHeaders)
                sb.Append($"{h.Key}: {string.Join(", ", h.Value)}\r\n");
            return $"-headers \"{sb}\"";
        }
    }
}
