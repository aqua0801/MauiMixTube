using AngleSharp.Common;
using AngleSharp.Media;
using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Helper;
using MauiMixTube.Messages;
using MauiMixTube.Models;
using MauiMixTube.Models.Fetch;
using MauiMixTube.Models.Settings;
using Microsoft.Maui.Controls.Shapes;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;


namespace MauiMixTube.Managers.Fetch.Services
{
    public class BilibiliFetchService : FetchService
    {
        private readonly BilibiliDownloader _bilibiliDownloader;
        private readonly SettingsManager _settingsManager;
        private readonly YoutubeDL _ytdl;

        private OptionSet _options = new ()
        {
            Format = "bestaudio",
            DumpSingleJson = true,
            NoPlaylist = true
        };

        public BilibiliFetchService(SettingsManager settingsManager , BilibiliDownloader bilibiliDownloader , YoutubeDL ytdl)
        {
            _bilibiliDownloader = bilibiliDownloader;
            _settingsManager = settingsManager;
            _ytdl = ytdl;

            WeakReferenceMessenger.Default
                .Register<QualityChangedMessage>(this, (r, m) =>
                {
                    ApplyFetchQuality(m.Value);
                });

            _options.SetCustomOption("--add-headers", $"User-Agent:{_settingsManager.UserAgent}");

            WeakReferenceMessenger.Default.Register<UserAgentChangedMessage>(this, (r, m) =>
            {
                _options.SetCustomOption("--add-headers", $"User-Agent:{m.NewUserAgent}");
            });

            ApplyFetchQuality(settingsManager.FetchQuality);    
        }

        public override async Task OnStartupAsync(CancellationToken ct = default)
        {
            await _bilibiliDownloader.OnStartupAsync(ct);
        }

        private void ApplyFetchQuality(FetchQuality quality)
        {
            _options.Format = quality switch
            {
                FetchQuality.Best => "bestaudio",
                FetchQuality.High => "bestaudio[abr<=320]/bestaudio",
                FetchQuality.Medium => "bestaudio[abr<=128]/bestaudio",
                FetchQuality.Low => "bestaudio[abr<=64]/bestaudio",
                _ => "bestaudio"
            };
        }

        public override WebTag SupportedTag => WebTag.Bilibili;
        public override bool SupportsLyrics => false;

        public override bool CanHandle(WebTag tag, string url)
        {
            if (tag == WebTag.Bilibili)
                return true;
            return url.Contains("bilibili.com") ||
                url.Contains("b23.tv") ||
                url.Contains("biliapi.com") ||
                url.Contains("bilivideo.com");
        }

        protected override async Task<AudioInfo?> OnResolveAsync(string url, CancellationToken ct = default)
        {
            var ua = _settingsManager.UserAgent;
  
            var result = await _ytdl.RunVideoDataFetch(url, overrideOptions: _options);

            if (result.Success && result.Data is not null)
            {
                var data = result.Data;

                return new()
                {
                    Title = data.Title,
                    Artist = data.Artist,
                    OriginalUrl = url,
                    Duration = TimeSpan.FromSeconds(data.Duration ?? 0),
                    Tag = WebTag.Bilibili,
                    Fetch = new()
                    {
                        SourceUrl = data.Url,
                        ThumbnailUrl = data.Thumbnail,
                        FfmpegHeader = $"-headers \"Referer: https://www.bilibili.com\r\nUser-Agent: {ua}\r\n\""
                    }
                };
            }

            return await _bilibiliDownloader.ResolveAudioStreamDataAsync(url, ct);
        }

        public override bool CanDetectFromUrl(string url)
        {
            return CanHandle(WebTag.None,url);
        }

    }
}
