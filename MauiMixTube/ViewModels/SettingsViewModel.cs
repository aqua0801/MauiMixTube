using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Audio;
using MauiMixTube.Audio.Eq;
using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Messages;
using MauiMixTube.Models.Settings;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace MauiMixTube.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settingsManager;
    private readonly CacheManager _cacheManager;
    private bool _isInitialized = false;
    public SettingsViewModel(SettingsManager settingsManager , CacheManager cacheManager)
    {
        _settingsManager = settingsManager;
        _cacheManager = cacheManager;

        SelectedLanguage = settingsManager.Language;
        ThemeModeText = string.Empty;
        CurrentTheme = settingsManager.Theme;
        Volume = settingsManager.Volume;
        SelectedQuality = settingsManager.FetchQuality;
        AutoEqEnabled = settingsManager.AutoEqEnabled;
        SelectedEqDevice = settingsManager.EqDeviceName;
        EqSearchQuery = settingsManager.EqDeviceName;
        EqSearchResults.Clear();
        LoadAudioDevices();
        SelectedAudioDevice = String.IsNullOrEmpty(_settingsManager.AudioDeviceName)? 
            DefaultDeviceLabel : AudioDeviceHelper.CleanDeviceName(_settingsManager.AudioDeviceName);
        RecentlyPlayedCount = settingsManager.RecentlyPlayedCount;

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

        LocalizationManager.Instance.PropertyChanged += (s, e) =>
        {
            OnCurrentThemeChanged(CurrentTheme);
            if(AudioDevices.Count > 0)
                AudioDevices[0] = DefaultDeviceLabel;
        };
        _isInitialized = true;
    }

    public LocalizationManager Localization => LocalizationManager.Instance;

    [ObservableProperty] public partial SettingsSection ActiveSection { get; set; } = SettingsSection.General;

    [RelayCommand] 
    private void SwitchSection(string s) => ActiveSection = Enum.Parse<SettingsSection>(s);

    // General
    public IReadOnlyList<string> AvailableLanguages 
        =>  LocalizationManager.Instance.GetAvailableLanguages();
    public IReadOnlyList<FetchQuality> AvailableQualities
        => (FetchQuality[])Enum.GetValues(typeof(FetchQuality));
    [ObservableProperty] public partial string SelectedLanguage { get; set; }

    [ObservableProperty] public partial ThemeMode CurrentTheme { get; set; }
    [ObservableProperty] public partial string ThemeModeText { get; set; }

    [RelayCommand]
    private void SetTheme(string t)
    {
        CurrentTheme = Enum.Parse<ThemeMode>(t);
        _settingsManager.Theme = CurrentTheme;
    }

    partial void OnSelectedLanguageChanged(string value)
        => _settingsManager.Language = value;

    partial void OnCurrentThemeChanged(ThemeMode value)
    {
        switch(value)
        {
            case ThemeMode.Light:
                ThemeModeText = LocalizationManager.Instance["Settings_Theme_Light"];
                break;
            case ThemeMode.Dark:
                ThemeModeText = LocalizationManager.Instance["Settings_Theme_Dark"];
                break;
            case ThemeMode.System:
                ThemeModeText = LocalizationManager.Instance["Settings_Theme_System"];
                break;
        }
    }

    // Player
    [ObservableProperty] public partial double Volume { get; set; }
    [ObservableProperty] public partial FetchQuality SelectedQuality { get; set; }
    [ObservableProperty] public partial bool LoudnessNormEnabled { get; set; }
    [ObservableProperty] public partial int RecentlyPlayedCount { get; set; }

    [ObservableProperty] public partial bool AutoEqEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEqSearchResults))]
    public partial ObservableCollection<string> EqSearchResults { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEqDevice))]
    public partial string? SelectedEqDevice { get; set; }

    [ObservableProperty] public partial string EqSearchQuery { get; set; } = string.Empty;

    public bool HasEqSearchResults => EqSearchResults.Count > 0;
    public bool HasSelectedEqDevice => !String.IsNullOrEmpty(SelectedEqDevice);
    public ObservableCollection<string> AudioDevices { get; private set; } = new ();
    public string DefaultDeviceLabel => LocalizationManager.Instance["Settings_AudioDevice_Default"];
    [ObservableProperty] public partial string SelectedAudioDevice { get; set; } = string.Empty;

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

    partial void OnEqSearchQueryChanged(string value)
    {
        if (String.IsNullOrEmpty(value))
            return;
        EqSearchResults = new(AutoEqDatabase.Search(value));
    }

    partial void OnAutoEqEnabledChanged(bool value)
    {
        _settingsManager.AutoEqEnabled = value;
    }

    partial void OnSelectedEqDeviceChanged(string? value)
    {
        _settingsManager.EqDeviceName = value??String.Empty;
    }

    partial void OnRecentlyPlayedCountChanged(int value)
    {
        _settingsManager.RecentlyPlayedCount = value;
    }

    [RelayCommand]
    private void SelectEqDevice(string device)
    {
        SelectedEqDevice = device;
        EqSearchQuery = device;
        EqSearchResults.Clear();

        var preset = AutoEqDatabase.Get(device);
        WeakReferenceMessenger.Default.Send(new EqPresetChangedMessage(preset));
    }

    [RelayCommand]
    private void ClearEqDevice()
    {
        SelectedEqDevice = null;
        EqSearchQuery = string.Empty;
    }

    public void LoadAudioDevices()
    {
        AudioDevices.Clear();
        AudioDevices = PcmPlayer.GetAvailableDevices()
            .Select(AudioDeviceHelper.CleanDeviceName)
            .Prepend(DefaultDeviceLabel)
            .ToObservableCollection();
    }

    partial void OnSelectedAudioDeviceChanged(string value)
    {
        if (!_isInitialized)
            return;

        var raw = value == DefaultDeviceLabel
            ? string.Empty
            : PcmPlayer.GetAvailableDevices()
                .FirstOrDefault(d => AudioDeviceHelper.CleanDeviceName(d) == value)
                ?? value;

        _settingsManager.AudioDeviceName = raw;
    }

    [RelayCommand]
    private void DecrementRecentCount()
    {
        RecentlyPlayedCount--;
    }

    [RelayCommand]
    private void IncrementRecentCount()
    {
        RecentlyPlayedCount++;
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

    public bool IsDesktopPlatform =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    [RelayCommand]
    private async Task OpenCacheDirAsync()
    {
        string path = AppPaths.BaseDir;

        SystemFileExplorer.Open(path);
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
        await Launcher.Default.OpenAsync("https://github.com/aqua0801/MauiMixTube/tree/master");
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