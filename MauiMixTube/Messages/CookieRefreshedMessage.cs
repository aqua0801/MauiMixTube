using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Messages
{
    public record CookieRefreshedMessage(string CookiePath , string? UserAgent);
}
