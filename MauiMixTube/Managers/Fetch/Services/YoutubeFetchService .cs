using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Helpers;
using MauiMixTube.Messages;
using MauiMixTube.Models;
using MauiMixTube.Models.Fetch;
using MauiMixTube.Models.Settings;
using System.Net;
using System.Web;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;


namespace MauiMixTube.Managers.Fetch.Handlers
{
    public class YoutubeFetchService : FetchService
    {
        private enum UrlPreprocess
        {
            Original , Music 
        }

        private enum FetchEndpoint
        {
            Ytdlp , YtExplode
        }

        private YoutubeDL? _ytdl;
        private YoutubeClient _client;

        private readonly OptionSet _options = new ()
        {
            Format = "bestaudio",
            DumpSingleJson = true,
            NoPlaylist = true
        };

        public YoutubeFetchService(SettingsManager settingsManager , YoutubeDL ytdl)
        {
            _ytdl = ytdl;
            _client = new YoutubeClient();

            WeakReferenceMessenger.Default
                .Register<QualityChangedMessage>(this, (r, m) =>
                {
                    ApplyFetchQuality(m.Value);
                });

            
            ApplyFetchQuality(settingsManager.FetchQuality);
        }

        public override WebTag SupportedTag => WebTag.YouTube;


        public override bool CanHandle(WebTag webTag, string url)
            => webTag == WebTag.YouTube
            || CanDetectFromUrl(url);

        public override bool CanDetectFromUrl(string url)
            => url.Contains("youtube.com")
            || url.Contains("youtu.be");


        private void ApplyFetchQuality(FetchQuality quality)
        {
            _options.Format = quality switch
            {
                FetchQuality.Best => "774/141/bestaudio",
                FetchQuality.High => "bestaudio[abr>=128]/bestaudio",
                FetchQuality.Medium => "bestaudio[abr<=128][abr>=96]/bestaudio[abr<=128]/bestaudio",
                FetchQuality.Low => "worstaudio",
                _ => "bestaudio"
            };
        }

        protected override async Task<AudioInfo?> OnResolveAsync(string url, CancellationToken ct = default)
        {
            var attempt1 = await TryApplyCombinationAsync(url, UrlPreprocess.Music, FetchEndpoint.YtExplode, ct);
            if (attempt1 is not null) return attempt1;

            var attempt2 = await TryApplyCombinationAsync(url, UrlPreprocess.Original, FetchEndpoint.YtExplode, ct);
            if (attempt2 is not null) return attempt2;

            var attempt3 = await TryApplyCombinationAsync(url, UrlPreprocess.Music, FetchEndpoint.Ytdlp, ct);
            if (attempt3 is not null) return attempt3;

            return await TryApplyCombinationAsync(url, UrlPreprocess.Original, FetchEndpoint.Ytdlp, ct);
        }

        private async Task<AudioInfo?> TryApplyCombinationAsync(string url , UrlPreprocess utype , FetchEndpoint endpoint  , CancellationToken ct = default)
        {
            var originalUrl = url;
            try
            {
                if (utype == UrlPreprocess.Music)
                {
                    if (!url.Contains("www.youtube.com"))
                        return null;
                    url = url.Replace("www.youtube.com", "music.youtube.com");
                }

                if (endpoint == FetchEndpoint.Ytdlp)
                {
                    var result = await _ytdl.RunVideoDataFetch(
                        url,
                        overrideOptions: _options
                        );

                    if (!result.Success || result.Data == null)
                        return null;

                    var data = result.Data;

                    return new AudioInfo
                    {
                        Title = data.Title,
                        OriginalUrl = originalUrl,
                        Artist = data.Creator ?? data.Uploader,
                        Duration = TimeSpan.FromSeconds(data.Duration ?? 0f),
                        Fetch = new FetchInfo
                        {
                            SourceUrl = data.Url,
                            ThumbnailUrl = data.Thumbnail
                        },
                        Tag = WebTag.YouTube
                    };
                }
                else
                {
                    var video = await _client.Videos.GetAsync(url, ct);
                    var manifest = await _client.Videos.Streams.GetManifestAsync(url, ct);
                    var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    if (streamInfo == null) return null;

                    return new AudioInfo()
                    {
                        Title = video.Title,
                        OriginalUrl = originalUrl,
                        Artist = video.Author.ChannelTitle,
                        Duration = video.Duration ?? TimeSpan.Zero,
                        Fetch = new FetchInfo
                        {
                            SourceUrl = streamInfo.Url,
                            ThumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url
                        },
                        Tag = WebTag.YouTube
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        protected override async Task<IEnumerable<QueueEntry>> OnExpandPlaylistAsync(string url, CancellationToken ct = default)
        {
            var playlist =  await _client.Playlists
                .GetVideosAsync(url, ct)
                .ToArrayAsync(ct);

            var result = playlist.Select(video => new QueueEntry
            {
                Tag = WebTag.YouTube,
                Url = $"https://www.youtube.com/watch?v={video.Id}",
                IsFromPlaylist = true
            });

            return result;
        }

        public override string NormalizeUrl(string url)
        {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);

            // playlist
            if (query["list"] is { } listId)
                return $"https://www.youtube.com/playlist?list={listId}";

            // track
            if (query["v"] is { } videoId)
                return $"https://www.youtube.com/watch?v={videoId}";

            // youtu.be short link
            if (uri.Host == "youtu.be")
                return $"https://www.youtube.com/watch?v={uri.AbsolutePath.TrimStart('/')}";

            return url;
        }

        public override TimeSpan? SourceUrlExpiry => TimeSpan.FromHours(6);
        public override async Task<string> RefreshSourceUrlAsync(string url, CancellationToken ct = default)
        {
            var manifest = await _client.Videos.Streams.GetManifestAsync(url, ct);
            var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            if (streamInfo is not null)
                return streamInfo.Url;
            return await base.RefreshSourceUrlAsync(url, ct);
        }


    }

    public class YoutubeClientFactory
    {
        private static List<Cookie> ParseCookieFile(string cookieFilePath)
        {
            var lines = File.ReadAllLines(cookieFilePath);

            return (from line in lines
                    where !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#')
                    select line.Split('\t')
                    into parts
                    where parts.Length >= 7
                    select new Cookie
                    {
                        Domain = parts[0],
                        Path = parts[2],
                        Secure = bool.Parse(parts[3]),
                        Name = parts[5],
                        Value = parts[6]
                    }).ToList();
        }

    }
}
