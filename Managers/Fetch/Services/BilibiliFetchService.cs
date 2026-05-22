using AngleSharp.Media;
using MauiMixTube.Models;
using MauiMixTube.Models.Fetch;
using MauiMixTube.Models.Settings;


namespace MauiMixTube.Managers.Fetch.Services
{
    public class BilibiliFetchService : FetchService
    {
        private readonly BilibiliDownloader _bilibiliDownloader;

        public BilibiliFetchService(BilibiliDownloader bilibiliDownloader)
        {
            _bilibiliDownloader = bilibiliDownloader;
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
            return await _bilibiliDownloader.ResolveAudioStreamDataAsync(url, ct);
        }

    }
}
