using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models
{
    public partial record WebTag(string Value)
    {
        public static implicit operator string(WebTag tag) => tag.Value;
        public override string ToString() => Value;
        public static readonly WebTag None = new WebTag("None");
    }

    [WebTagProvider]
    public static class WebTags
    {
        public static readonly WebTag YouTube = new("YouTube");
        public static readonly WebTag Bilibili = new("Bilibili");
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class WebTagProviderAttribute : Attribute { }
}
