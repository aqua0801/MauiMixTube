using MauiMixTube.Audio;
using MauiMixTube.Helper;
using MauiMixTube.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiMixTube.Managers
{
    public sealed partial class CacheManager
    {
        private ConcurrentDictionary<string, CacheEntry> _index = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly SettingsManager _settingsManager;

        public record CacheEntry(
            string Path,
            long SizeBytes,
            DateTime LastPlayed);

        [JsonSourceGenerationOptions(
            PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
            WriteIndented = true,
            GenerationMode = JsonSourceGenerationMode.Default)]
        [JsonSerializable(typeof(ConcurrentDictionary<string, CacheEntry>))]
        public partial class CacheJsonContext : JsonSerializerContext 
        {

        }

        [JsonSourceGenerationOptions(
            PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
            WriteIndented = true,
            GenerationMode = JsonSourceGenerationMode.Default)]
        [JsonSerializable(typeof(AudioInfoCache))]
        public partial class AudioInfoJsonContext : JsonSerializerContext
        {

        }

        public CacheManager(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        public async Task InitializeAsync()
        {
            if (!File.Exists(AppPaths.CacheIndex))
            {
                _index = new ConcurrentDictionary<string, CacheEntry>();
                await SaveIndexAsync(); 
                return;
            }

            try
            {
                await using (var stream = File.OpenRead(AppPaths.CacheIndex))
                {
                    _index = await JsonSerializer.DeserializeAsync(
                        stream, CacheJsonContext.Default.ConcurrentDictionaryStringCacheEntry)
                        ?? new();
                }

                var indexedPaths = _index.Values.Select(e => e.Path).ToHashSet();
                var fileDirectories = Directory.GetFiles(AppPaths.CachedFilesDir, "*.webm").ToHashSet();
                foreach (var file in fileDirectories)
                {
                    if (!indexedPaths.Contains(file))
                    {
                        File.Delete(file);
                        Console.WriteLine($"[Cache] removing : {Path.GetFileName(file)}");
                    }
                }

                var toRemove = new List<string>();

                foreach (var kvp in _index)
                {
                    var path = kvp.Value.Path;
                    if (!File.Exists(path))
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var item in toRemove)
                {
                    _index.TryRemove(item, out _);
                    Console.WriteLine($"[Cache] removing index for missing file: {item}");
                }

                if (toRemove.Count > 0)
                {
                    await SaveIndexAsync();
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Cache] index corrupted, rebuilding: {ex.Message}");
                _index = new ConcurrentDictionary<string, CacheEntry>();
                await SaveIndexAsync();
            }
        }

        private async Task SaveIndexAsync(CancellationToken ct = default)
        {
            await using var stream = File.Create(AppPaths.CacheIndex);
            await JsonSerializer.SerializeAsync(
                stream, _index, CacheJsonContext.Default.ConcurrentDictionaryStringCacheEntry,ct);
        }

        public async Task StoreMetadataAsync(AudioInfo info , CancellationToken ct = default)
            => await StoreMetadataAsync(ToKey(info), info, ct);

        public async Task StoreMetadataAsync(string key, AudioInfo info , CancellationToken ct = default)
        {
            var path = Path.Combine(AppPaths.MetaCacheDir, $"{key}.json");
            AudioInfoCache cache = info;
            var json = JsonSerializer.Serialize(cache,
                AudioInfoJsonContext.Default.AudioInfoCache);
            await File.WriteAllTextAsync(path, json, ct);
        }

        public void RemoveAllMetadata()
        {
            var files = Directory.GetFiles(AppPaths.MetaCacheDir, "*.json");
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        public void RemoveAllAudioCache()
        {
            foreach(var item in _index)
            {
                if (File.Exists(item.Value.Path))
                {
                    File.Delete(item.Value.Path);
                }
            }
            _index.Clear();
        }

        public async Task<AudioInfo?> GetMetadataAsync(string key)
        {
            var path = Path.Combine(AppPaths.MetaCacheDir, $"{key}.json");
            if (!File.Exists(path)) return null;

            await using var stream = File.OpenRead(path);
            var cache = await JsonSerializer.DeserializeAsync<AudioInfoCache>(
                stream, AudioInfoJsonContext.Default.AudioInfoCache);
            return cache is null ? null : (AudioInfo)cache;
        }

        public async Task RegisterAsync(string key, string path, long size, CancellationToken ct = default)
        {
            var entry = new CacheEntry(path, size, DateTime.UtcNow);
            _index[key] = entry;
            await SaveIndexAsync(ct);
        }

        public async Task TrySetCachePathAsync(AudioInfo audioInfo, CancellationToken ct)
        {
            var path = await TryGetPathAsync(audioInfo,ct);
            audioInfo.Fetch.CachedFilePath = path; 
        }

        public void Attach(ChunkedAudioStream stream , string finalPath , CancellationToken ct)
        {
            stream.OnCacheReady += async info =>
            {
                var size = new FileInfo(finalPath).Length;
                await RegisterAsync(ToKey(info), finalPath, size, ct);
                await PruneAsync(_settingsManager.Current.Cache.MaxSizeMb * 1_000_000);
                await TrySetCachePathAsync(info, ct);
            };
        }

        public async Task<string?> TryGetPathAsync(AudioInfo audioInfo,CancellationToken ct) 
            => await TryGetPathAsync(ToKey(audioInfo.Fetch.SourceUrl),ct);

        public async Task<string?> TryGetPathAsync(string key,CancellationToken ct)
        {
            if (!_index.TryGetValue(key, out var entry)) return null;

            if (!File.Exists(entry.Path))
            {
                _index.TryRemove(key, out _);
                return null;
            }

            _index[key] = entry with { LastPlayed = DateTime.UtcNow };
            _ = SaveIndexAsync(ct); 
            return entry.Path;
        }
        public async Task PruneAsync(long maxBytes)
        {
            await _lock.WaitAsync();

            if (maxBytes < 0)
                return;

            try
            {
                var total = _index.Values.Sum(e => e.SizeBytes);
                if (total <= maxBytes) return;

                var candidates = _index
                    .OrderBy(kvp => kvp.Value.LastPlayed)
                    .ToList();

                foreach (var (key, entry) in candidates)
                {
                    if (total <= maxBytes) break;

                    File.Delete(entry.Path);
                    total -= entry.SizeBytes;
                    _index.TryRemove(key, out _);

                    Console.WriteLine($"[Cache] cleared {Path.GetFileName(entry.Path)}" +
                                      $"({entry.SizeBytes / 1024 / 1024}MB," +
                                      $"last played: {entry.LastPlayed:yyyy-MM-dd})");
                }

                await SaveIndexAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        public static string ToKey(QueueEntry entry)
            => ToKey(entry.Url);

        public static string ToKey(AudioInfo info) 
            => ToKey(info.Fetch.SourceUrl);

        private static string ToKey(string url)
            => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(url)));

        public long TotalSizeBytes
            => _index.Values.Sum(e => e.SizeBytes);
        public long MaxSizeBytes
            => _settingsManager.Current.Cache.MaxSizeMb * 1024L * 1024;
        public double UsageRatio
            => MaxSizeBytes > 0
               ? Math.Clamp((double)TotalSizeBytes / MaxSizeBytes, 0, 1)
               : 0;
        public string UsageText
            => $"{TotalSizeBytes / 1024 / 1024}MB / {_settingsManager.Current.Cache.MaxSizeMb}MB";
    }
}
