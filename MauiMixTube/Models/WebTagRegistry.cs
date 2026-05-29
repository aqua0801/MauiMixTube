using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MauiMixTube.Models
{
    public static class WebTagRegistry
    {
        private static IReadOnlyList<WebTag>? _cache;

        public static IReadOnlyList<WebTag> GetAll()
        {
            if (_cache is not null) return _cache;

            _cache = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass
                         && t.IsAbstract
                         && t.IsSealed
                         && t.GetCustomAttribute<WebTagProviderAttribute>() is not null)
                .SelectMany(t => t.GetFields(
                    BindingFlags.Public | BindingFlags.Static))
                .Where(f => f.FieldType == typeof(WebTag))
                .Select(f => (WebTag)f.GetValue(null)!)
                .Where(t => t is not null && t != WebTag.None)
                .ToList();

            return _cache;
        }

        public static WebTag GetByName(string name)
            => GetAll().First(t => t.Value == name);
    }
}
