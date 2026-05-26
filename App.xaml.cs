using MauiMixTube.Audio.Eq;
using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Managers.Fetch;

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

            //MainPage = new LoadingPage(); i dont have loading page yet, so just set main page after settings manager is initialized
            //Todo
            MainPage = new ContentPage();

            _ = InitializeAsync(
                settingsManager,
                fetchManager,
                cacheManager,
                playlistRepository);
        }


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

            MainPage = new AppShell();
        }
    }
}