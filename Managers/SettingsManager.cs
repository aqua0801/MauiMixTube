using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Extensions;
using MauiMixTube.Messages;
using MauiMixTube.Models.Settings;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiMixTube.Managers
{
    public partial class SettingsManager
    {
        [JsonSourceGenerationOptions(
            PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
            WriteIndented = true,
            GenerationMode = JsonSourceGenerationMode.Default)]
        [JsonSerializable(typeof(AppSettings))]
        public partial class SettingsJsonContext : JsonSerializerContext
        {

        }

        private AppSettings _current;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private CancellationTokenSource _saveCts = new();
        public AppSettings Current => _current ??= new();

        public async Task InitializeAsync()
        {
            var path = Helpers.AppPaths.SettingsFile;

            if (!File.Exists(path))
            {
                Console.WriteLine("[Settings] Settings file does not exist. Attempting to build one.");
                _current = new AppSettings();
                await SaveAsync(_current);
                return;
            }

            using var stream = File.OpenRead(path);
            _current = await JsonSerializer.DeserializeAsync(stream, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            await LocalizationManager.Instance.LoadLanguageAsync(Current.General.Language);
        }

        public Task SaveAsync() => SaveAsync(_current ?? new());
        public async Task SaveAsync(AppSettings settings)
        {
            await _saveLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(
                    settings,
                    SettingsJsonContext.Default.AppSettings);

                await File.WriteAllTextAsync(
                    Helpers.AppPaths.SettingsFile,
                    json);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        private void ScheduleSave()
        {
            _saveCts.Cancel();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;

            _ = Task.Delay(1000, token).ContinueWith(
                async t =>
                {
                    if (t.IsCanceled) return;
                    await SaveAsync(_current);
                },
                TaskScheduler.Default);
        }


        public double Volume
        {
            get => Current.Player.Volume;
            set
            {
                value = Math.Clamp(value, 0, 100);
                if(value != Current.Player.Volume)
                {
                    Current.Player.Volume = value;
                    WeakReferenceMessenger.Default.Send(new VolumeChangedMessage(value));
                    ScheduleSave();
                }
            }
        }

        public bool LoudnessNormEnabled
        {
            get => Current.Player.LoudnessNormEnabled;
            set
            {
                if (value != Current.Player.LoudnessNormEnabled)
                {
                    Current.Player.LoudnessNormEnabled = value;
                    ScheduleSave();
                }
            }
        }

        public int RecentlyPlayedCount
        {
            get => Current.Player.RecentlyPlayedCount;
            set 
            {
                if(value != Current.Player.RecentlyPlayedCount)
                {
                    Current.Player.RecentlyPlayedCount = value;
                    ScheduleSave();
                }
            }
        }

        public bool AutoEqEnabled
        {
            get => Current.Player.AutoEqEnabled;
            set
            {
                if(value != Current.Player.AutoEqEnabled)
                {
                    Current.Player.AutoEqEnabled = value;
                    ScheduleSave();
                }
            }
        }

        public string DeviceName
        {
            get => Current.Player.DeviceName;
            set
            {
                if(value != Current.Player.DeviceName)
                {
                    Current.Player.DeviceName = value;
                    ScheduleSave();
                }
            }
        }


        public ThemeMode Theme
        {
            get => Current.General.Theme;
            set
            {
                if (value != Current.General.Theme)
                {
                    Current.General.Theme = value;
                    Application.Current.UserAppTheme = value.ToAppTheme();
                    ScheduleSave();
                }
            }
        }

        public string Language
        {
            get => _current.General.Language;
            set
            {
                if (value == _current.General.Language) return;
                _current.General.Language = value;
                MainThread.BeginInvokeOnMainThread(async () =>
                    await LocalizationManager.Instance.LoadLanguageAsync(value));
                ScheduleSave();
            }
        }

        public int MaxConcorrentFetches
        {
            get => Current.Fetch.MaxConcurrentFetches;
            set
            {
                if (value != Current.Fetch.MaxConcurrentFetches)
                {
                    Current.Fetch.MaxConcurrentFetches = value;
                    ScheduleSave();
                }
            }
        }

        public int FetchPageSize
        {
            get => Current.Fetch.FetchPageSize;
            set
            {
                if (value != Current.Fetch.FetchPageSize)
                {
                    Current.Fetch.FetchPageSize = value;
                    ScheduleSave();
                }
            }
        }

        public int MaxRetryAttempts
        {
            get => Current.Fetch.MaxRetryAttempts;
            set
            {
                if (value != Current.Fetch.MaxRetryAttempts)
                {
                    Current.Fetch.MaxRetryAttempts = value;
                    ScheduleSave();
                }
            }
        }

        public string UserAgent
        {
            get => Current.Fetch.UserAgent;
            set
            {
                if(value != Current.Fetch.UserAgent)
                {
                    Current.Fetch.UserAgent = value;
                    ScheduleSave();
                    WeakReferenceMessenger.Default.Send(new UserAgentChangedMessage(value));
                }
            }
        }


        public FetchQuality FetchQuality
        {
            get => Current.Fetch.Quality;
            set
            {
                if (value != Current.Fetch.Quality)
                {
                    Current.Fetch.Quality = value;
                    ScheduleSave();
                }
            }
        }

        public int MaxCacheSizeMb
        {
            get => Current.Cache.MaxSizeMb;
            set
            {
                if (value != Current.Cache.MaxSizeMb)
                {
                    Current.Cache.MaxSizeMb = value;
                    ScheduleSave();
                }
            }
        }

    }
}
