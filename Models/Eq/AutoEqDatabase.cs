using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MauiMixTube.Models.Eq
{
    public static class AutoEqDatabase
    {
        private static Dictionary<string, EqPreset>? _db;

        public static async Task LoadAsync()
        {
            await using var stream = await FileSystem
                .OpenAppPackageFileAsync("autoeq_database.json");

            _db = await JsonSerializer.DeserializeAsync<Dictionary<string, EqPreset>>(
                stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        public static IEnumerable<string> Search(string query)
            => _db?.Keys
                .Where(k => k.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k)
                .Take(10)
                ?? Enumerable.Empty<string>();

        public static EqPreset? Get(string deviceName)
            => _db?.TryGetValue(deviceName, out var preset) == true ? preset : null;
    }
}
