using CommunityToolkit.Mvvm.ComponentModel;
using MauiMixTube.Helpers;
using System.Globalization;
using System.Text.Json;

namespace MauiMixTube.Managers
{
    public partial class LocalizationManager : ObservableObject
    {
        public static LocalizationManager Instance { get; } = new();

        private FileSystemWatcher? _watcher;

        private Dictionary<string, string> _strings = new();

        public string this[string key] =>
            _strings.TryGetValue(key, out var value) ? value : key;

        public void WatchForUpdates()
        {
            if (!Directory.Exists(AppPaths.LanguagesDir)) return;

            _watcher = new FileSystemWatcher(AppPaths.LanguagesDir, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _watcher.Changed += async (_, e) =>
            {
                var cultureName = Path.GetFileNameWithoutExtension(e.Name);
                if (cultureName != CultureInfo.CurrentUICulture.Name) return;

                await Task.Delay(200);

                await MainThread.InvokeOnMainThreadAsync(() =>
                    LoadLanguageAsync(cultureName));
            };
        }

        public async Task LoadLanguageAsync(string cultureName)
        {
            if (await TryLoadLanguageAsync(cultureName)) return;
            if (await TryLoadLanguageAsync("en-US")) return;

            _strings = new Dictionary<string, string>();
  
            NotifyLanguageChanged();
        }

        private void NotifyLanguageChanged()
        {
            OnPropertyChanged("Item");
        }

        private async Task<bool> TryLoadLanguageAsync(string cultureName)
        {
            var userPath = Path.Combine(AppPaths.LanguagesDir, $"{cultureName}.json");
            var builtinPath = $"Languages/{cultureName}.json";

            string? json = null;

            if (File.Exists(userPath))
            {
                json = await File.ReadAllTextAsync(userPath);
            }
            else
            {
                try
                {
                    await using var stream = await FileSystem
                        .OpenAppPackageFileAsync(builtinPath);
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                }
                catch { }
            }

            if (json is null)
            {
                return false;
            }

            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            NotifyLanguageChanged();
            return true;
        }

        public IReadOnlyList<string> GetAvailableLanguages()
        {
            if(Directory.Exists(AppPaths.LanguagesDir))
            {
                return Directory.GetFiles(AppPaths.LanguagesDir, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToList();
            }
            return new List<string>();
        }

    }
}
