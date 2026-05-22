using MauiMixTube.Models;
using MauiMixTube.Models.Fetch;
using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Managers.Fetch.Services
{
    public class BilibiliFetchService : FetchService
    {
        public override WebTag SupportedTag => WebTag.Bilibili;

        public override bool CanHandle(WebTag tag, string url)
        {
            if (tag == WebTag.Bilibili)
                return true;
            return url.Contains("bilibili.com") ||
                url.Contains("b23.tv") ||
                url.Contains("biliapi.com") ||
                url.Contains("bilivideo.com");
        }

        protected override Task<AudioInfo?> OnResolveAsync(string url, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
