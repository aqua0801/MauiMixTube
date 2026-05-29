using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Messages;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MauiMixTube.Models.Settings
{
    public class BilibiliDownloader
    {
        private record BilibiliVideoMetadata(
            string Title,
            string Author,
            string ThumbnailUrl,
            int DurationSec,
            string Header = "",
            string VideoUrl = "",
            string AudioUrl = ""
        );

        private const string DefaultReferer = "https://www.bilibili.com";
        private readonly SettingsManager _settingsManager;
        private readonly HttpClient _http;

        public BilibiliDownloader(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                         System.Net.DecompressionMethods.Deflate,
                UseCookies = true,
                CookieContainer = new()
            };
            _http = new HttpClient(handler);
            _http.DefaultRequestHeaders.Referrer = new Uri(DefaultReferer);

            SetUserAgent(settingsManager.UserAgent);

            WeakReferenceMessenger.Default.Register<UserAgentChangedMessage>(this, (r, m) =>
            {
                SetUserAgent(m.NewUserAgent);
            });
        }

        public async Task OnStartupAsync(CancellationToken ct = default)
        {
            await WarmUpAsync();
        }
        private async Task WarmUpAsync()
        {
            await _http.GetAsync("https://www.bilibili.com");
        }

        private void SetUserAgent(string ua)
        {
            var old = _http.DefaultRequestHeaders.UserAgent.ToString();
            _http.DefaultRequestHeaders.UserAgent.Clear();
            if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(ua))
                _http.DefaultRequestHeaders.UserAgent.TryParseAdd(old);
        }

        private async Task<BilibiliVideoMetadata?> GetMetadataAsync(string url, MediaType type , CancellationToken ct)
        {
            string ua = _settingsManager.UserAgent;
            var html = await FetchHtmlAsync(url);
            var meta = ResolveMetadataFromHtml(html);
            var header = MediaValidator.ConvertHttpClientToFfmpegHeaderArg(_http);

            if (meta is null)
                return null;

            var dash = ExtractDashFromHtml(html);

            var (videoStream, audioStream) = dash;

            string audioUrl = "unknown" , videoUrl = "unknown";

            if(type == MediaType.AudioOnly || type == MediaType.VideoWithAudio)
                audioUrl = await ResolveFirstValidUrlAsync(audioStream, MediaType.AudioOnly, ua, ct);

            if(type == MediaType.VideoOnly || type == MediaType.VideoWithAudio)
                videoUrl = await ResolveFirstValidUrlAsync(videoStream, MediaType.VideoOnly, ua, ct);

            if(type==MediaType.VideoWithAudio)
                meta = meta with { VideoUrl = videoUrl, AudioUrl = audioUrl, Header = header };
            else if (type==MediaType.AudioOnly)
                meta = meta with { AudioUrl = audioUrl, Header = header };
            else if (type==MediaType.VideoOnly)
                meta = meta with { VideoUrl = videoUrl, Header = header };

            return meta;
        }

        private async Task<string> FetchHtmlAsync(string url)
        {
            var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
            return await response.Content.ReadAsStringAsync();
        }

        private static (JsonElement video, JsonElement audio) ExtractDashFromHtml(string html)
        {
            var match = Regex.Match(html, @"__playinfo__=(.*?)</script><script>");

            if (!match.Success)
            {
                return (default, default); 
            }

            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            var root = doc.RootElement;

            JsonElement video = default;
            JsonElement audio = default;

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("dash", out var dash))
            {
                dash.TryGetProperty("video", out video);
                dash.TryGetProperty("audio", out audio);
            }

            return (video.Clone(), audio.Clone());
        }

        private static readonly List<Func<JsonElement, string?>> _urlParsers = new()
        {
            t => t.TryGetProperty("base_url", out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null,

            t => t.TryGetProperty("baseUrl", out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null,

            t => t.TryGetProperty("backup_url", out var el) && el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0
                 ? el[0].ToString() : null,

            t => t.TryGetProperty("backupUrl", out var el) && el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0
                 ? el[0].ToString() : null,
        };

        private static async Task<string> ResolveFirstValidUrlAsync(JsonElement stream, MediaType type , string ua , CancellationToken ct)
        {
            foreach (var item in stream.EnumerateArray())
            {
                foreach (var parser in _urlParsers)
                {
                    try
                    {
                        string candidate = parser(item);
                        if (await MediaValidator.IsValidMediaUrlAsync(candidate, type, ua, DefaultReferer, ct))
                            return candidate;
                    }
                    catch { /* empty , move on */ }
                }
            }
            return "-1";
        }

        private static BilibiliVideoMetadata? ResolveMetadataFromHtml(string html)
        {
            var match = Regex.Match(html, @"__INITIAL_STATE__=(.*?);\(function\(\)");
            if (!match.Success) return null;

            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            var root = doc.RootElement;

            if (!root.TryGetProperty("videoData", out var videoData)) return null;

            string title = videoData.TryGetProperty("title", out var t) ? t.GetString() ?? "unknown" : "unknown";
            string author = videoData.TryGetProperty("owner", out var owner) &&
                            owner.TryGetProperty("name", out var n)
                                ? n.GetString() ?? "unknown"
                                : "unknown";
            int durationSec = videoData.TryGetProperty("duration", out var dur) ? dur.GetInt32() : 0;
            string thumbnailUrl = videoData.TryGetProperty("pic", out var p) ? p.GetString() ?? "unknown" : "unknown";

            return new BilibiliVideoMetadata(title, author , thumbnailUrl , durationSec);
        }

        public async Task<AudioInfo?> ResolveAudioStreamDataAsync(string url, CancellationToken ct)
        {
            for (int i = 0; i < _settingsManager.MaxRetryAttempts; i++)
            {
                try
                {
                    var meta = await GetMetadataAsync(url, MediaType.AudioOnly, ct);
                    if (meta is null)
                        continue;

                    return new()
                    {
                        Title = meta.Title,
                        Artist = meta.Author,
                        Duration = TimeSpan.FromSeconds(meta.DurationSec),
                        OriginalUrl = url,
                        Tag = WebTag.Bilibili,
                        Fetch = new()
                        {
                            SourceUrl = meta.AudioUrl,
                            FfmpegHeader = meta.Header,
                            FetchedAt = DateTime.UtcNow,
                            ThumbnailUrl = meta.ThumbnailUrl
                        }
                    };
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"[BilibiliDownloader] Error occurred while resolving audio stream data for URL {url}: {ex.Message}");
                }
            }

            return null;
        }

    }
}
