using System.Text.Json;
using MauiMixTube.Helpers;

namespace MauiMixTube.Audio.Eq
{
    public static class AutoEqDatabase
    {
        private static Dictionary<string, EqDeviceData>? _db;

        public static async Task LoadAsync()
        {
            await using var stream = await FileSystem
                .OpenAppPackageFileAsync("autoeq_database.json");

            _db = await JsonSerializer.DeserializeAsync<Dictionary<string, EqDeviceData>>(
                stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        public static IEnumerable<string> Search(string query)
            => _db?.Keys
                .Select(k => (Name: k, Score: FuzzySearch.HybridScoreSimilarity(query, k)))
                .Where(x => x.Score > 0.3)  
                .OrderByDescending(x => x.Score)
                .Take(10)
                .Select(x => x.Name)
                ?? Enumerable.Empty<string>();

        public static EqPreset? Get(string deviceName)
            => _db?.TryGetValue(deviceName, out var data) == true ? data.Peq : null;
        
    }
}
