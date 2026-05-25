using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiMixTube.Helper;
using MauiMixTube.Managers;
using MauiMixTube.Models.Settings;
using System.Collections.ObjectModel;
#if WINDOWS
using MauiMixTube.Helper;
using System.Diagnostics;
#endif

namespace MauiMixTube.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settingsManager;
    private readonly CacheManager _cacheManager;
    public SettingsViewModel(SettingsManager settingsManager , CacheManager cacheManager)
    {
        _settingsManager = settingsManager;
        _cacheManager = cacheManager;

        SelectedLanguage = settingsManager.Language;
        SelectedQuality = _settingsManager.FetchQuality;
        CurrentTheme = settingsManager.Theme;
        Volume = settingsManager.Volume;
        SelectedQuality = settingsManager.FetchQuality;

        MaxCacheSizeMb = settingsManager.MaxCacheSizeMb;
        MaxConcurrentFetches = settingsManager.MaxConcorrentFetches;
        FetchPageSize = settingsManager.FetchPageSize;
        MaxRetryAttempts = settingsManager.MaxRetryAttempts;
        LoudnessNormEnabled = settingsManager.LoudnessNormEnabled;

        AppVersion = AppInfo.VersionString;
        CacheUsageText = _cacheManager.UsageText;
        CacheUsageRatio = _cacheManager.UsageRatio;
        MaxCacheSizeMb = cacheManager.MaxSizeBytes / 1024 / 1024;
        UserAgent = settingsManager.UserAgent;
    }

    public LocalizationManager Localization => LocalizationManager.Instance;

    [ObservableProperty] public partial SettingsSection ActiveSection { get; set; } = SettingsSection.General;

    [RelayCommand] 
    private void SwitchSection(string s) => ActiveSection = Enum.Parse<SettingsSection>(s);

    // General
    public IReadOnlyList<string> AvailableLanguages 
        =>  LocalizationManager.Instance.GetAvailableLanguages();
    public IReadOnlyList<string> AvailableQualities
        => Enum.GetNames(typeof(FetchQuality));
    [ObservableProperty] public partial string SelectedLanguage { get; set; }
    [ObservableProperty] public partial ThemeMode CurrentTheme { get; set; }

    [RelayCommand]
    private void SetTheme(string t)
    {
        CurrentTheme = Enum.Parse<ThemeMode>(t);
        _settingsManager.Theme = CurrentTheme;
    }

    partial void OnSelectedLanguageChanged(string value)
        => _settingsManager.Language = value;

    // Player
    [ObservableProperty] public partial double Volume { get; set; }
    [ObservableProperty] public partial FetchQuality SelectedQuality { get; set; }
    [ObservableProperty] public partial bool LoudnessNormEnabled { get; set; }

    [ObservableProperty] public partial bool AutoEqEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEqSearchResults))]
    public partial ObservableCollection<string> EqSearchResults { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEqDevice))]
    public partial string? SelectedEqDevice { get; set; }

    [ObservableProperty] public partial string EqSearchQuery { get; set; } = string.Empty;

    public bool HasEqSearchResults => EqSearchResults.Count > 0;
    public bool HasSelectedEqDevice => SelectedEqDevice is not null;

    partial void OnVolumeChanged(double value)
    {
        _settingsManager.Volume = value;
    }

    partial void OnSelectedQualityChanged(FetchQuality value)
    {
        _settingsManager.FetchQuality = value;
    }

    partial void OnLoudnessNormEnabledChanged(bool value)
    {
        _settingsManager.LoudnessNormEnabled = value;
    }

    [RelayCommand]
    private void SelectEqDevice(string device)
    {
        SelectedEqDevice = device;
        EqSearchQuery = device;
        EqSearchResults.Clear();             
    }

    [RelayCommand]
    private void ClearEqDevice()
    {
        SelectedEqDevice = null;
        EqSearchQuery = string.Empty;
    }



    // Cache
    [ObservableProperty] public partial long MaxCacheSizeMb { get; set; }
    [ObservableProperty] public partial string CacheUsageText { get; set; }
    [ObservableProperty] public partial double CacheUsageRatio { get; set; }

    partial void OnMaxCacheSizeMbChanged(long value)
    {
        MaxCacheSizeMb = value;
        _settingsManager.MaxCacheSizeMb = (int)value;
        CacheUsageRatio = _cacheManager.UsageRatio;
        CacheUsageText = _cacheManager.UsageText;
    }

    [RelayCommand] 
    private async Task ClearAudioCacheAsync()
    {
        var isConfirm = await Application.Current.Windows[0].Page.DisplayAlertAsync(
            "Confirm",
            "Are you sure you want to clear the audio cache?",
            "Yes",
            "No"
        );

        if(isConfirm)
        {
            _cacheManager.RemoveAllAudioCache();
            CacheUsageRatio = _cacheManager.UsageRatio;
            CacheUsageText = _cacheManager.UsageText;
        }

    }
    [RelayCommand] 
    private async Task ClearMetaCacheAsync()
    {
        var isConfirm = await Application.Current.Windows[0].Page.DisplayAlertAsync(
            "Confirm",
            "Are you sure you want to clear the metadata cache?",
            "Yes",
            "No"
        );

        if(isConfirm)
        {
            _cacheManager.RemoveAllMetadata();
            CacheUsageRatio = _cacheManager.UsageRatio;
            CacheUsageText = _cacheManager.UsageText;
        }
    }

    [RelayCommand]
    private async Task OpenCacheDirAsync()
    {
#if WINDOWS
    Process.Start(new ProcessStartInfo
    {
        FileName        = "explorer.exe",
        Arguments       = $"\"{AppPaths.BaseDir}\"", 
        UseShellExecute = false
    });
#endif
    }

    // Fetch
    [ObservableProperty] public partial string UserAgent { get; set; }
    [ObservableProperty] public partial int MaxConcurrentFetches { get; set; }
    [ObservableProperty] public partial int FetchPageSize { get; set; }
    [ObservableProperty] public partial int MaxRetryAttempts { get; set; }

    [RelayCommand]
    private void RotateUserAgent()
    {
        UserAgent = UserAgentFallback.Next(UserAgent);
    }

    [RelayCommand]
    private void ResetUserAgent()
    {
        UserAgent = UserAgentFallback.Default;
    }

    partial void OnUserAgentChanged(string value)
    {
        _settingsManager.UserAgent = value;
    }

    partial void OnMaxConcurrentFetchesChanged(int value)
    {
        _settingsManager.MaxConcorrentFetches = value;
    }

    partial void OnFetchPageSizeChanged(int value)
    {
        _settingsManager.FetchPageSize = value;
    }

    partial void OnMaxRetryAttemptsChanged(int value)
    {
        _settingsManager.MaxRetryAttempts = value;
    }

    [RelayCommand] 
    private void IncrementConcurrent()
    {
        MaxConcurrentFetches++;
    }
    [RelayCommand] 
    private void DecrementConcurrent()
    {
        if(MaxConcurrentFetches > 1)
            MaxConcurrentFetches--;
    }

    [RelayCommand]
    private void IncrementPageSize()
    {
        FetchPageSize += 5;
    }
    [RelayCommand]
    private void DecrementPageSize()
    {
        if(FetchPageSize > 5)
            FetchPageSize -= 5;
    }

    [RelayCommand]
    private void IncrementRetry()
    {
        MaxRetryAttempts++;
    }
    [RelayCommand]
    private void DecrementRetry()
    {
        if(MaxRetryAttempts > 1)
            MaxRetryAttempts--;
    }

    // About
    [ObservableProperty] public partial string AppVersion { get; set; }

    [RelayCommand]
    private async Task OpenSourceCodeAsync()
    {

    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenLicensesAsync()
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);

        if (Application.Current?.Windows[0] != null)
        {
            var result = await Application.Current.Windows[0].Page.DisplayAlertAsync(
                "Licenses",
                """
                This is just a small project built with love and caffeine. 
                You can use it, tweak it, or share it and it is provided 'as-is' without any warranties. 
                If you find it useful, feel free to give it a ⭐️ on GitHub!
                Btw, check this out ,it's cool I promise !
                """,
                "Sure",
                "No"
            );

            if(result)
            {
                await Launcher.Default.OpenAsync("https://youtube.com/watch?v=dQw4w9WgXcQ");
            }
        }
    }
}