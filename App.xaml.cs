using MauiMixTube.Audio.Eq;
using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Views;

namespace MauiMixTube
{
    public partial class App : Application
    {
        public App(
            SettingsManager settingsManager , 
            FetchManager fetchManager ,
            CacheManager cacheManager,
            PlaylistRepository playlistRepository)
        {
            InitializeComponent();

            _ = InitializeAsync(
                settingsManager,
                fetchManager,
                cacheManager,
                playlistRepository);
        }

        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(new LoadingPage());

        private async Task InitializeAsync(
            SettingsManager settingsManager , 
            FetchManager fetchManager ,
            CacheManager cacheManager,
            PlaylistRepository playlistRepository)
        {
            await AppPaths.EnsureDirectories();
            await settingsManager.InitializeAsync();
            await fetchManager.OnStartupAsync();
            await cacheManager.InitializeAsync();
            await playlistRepository.InitializeAsync();
            await AutoEqDatabase.LoadAsync();

            await Task.Delay(1000);

            MainThread.BeginInvokeOnMainThread(() =>
                Windows[0].Page = new AppShell());
        }
    }
}