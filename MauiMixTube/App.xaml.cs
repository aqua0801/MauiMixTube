using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Audio.Eq;
using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Managers.Media;
using MauiMixTube.Messages;
using MauiMixTube.Views;

namespace MauiMixTube
{
    public partial class App : Application
    {
        public App(
            SettingsManager settingsManager , 
            FetchManager fetchManager ,
            CacheManager cacheManager,
            PlaylistRepository playlistRepository ,
            IMediaControlsService mediaControls)
        {
            InitializeComponent();

            WeakReferenceMessenger.Default.Register<ToastMessage>(this,async (r,m) =>
            {
                var toast = Toast.Make(m.Text, ToastDuration.Short, 14);
                await toast.Show();
            });

            _ = InitializeAsync(
                settingsManager,
                fetchManager,
                cacheManager,
                playlistRepository,
                mediaControls);
        }

        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(new LoadingPage());

        private async Task InitializeAsync(
            SettingsManager settingsManager , 
            FetchManager fetchManager ,
            CacheManager cacheManager,
            PlaylistRepository playlistRepository,
            IMediaControlsService mediaControls)
        {
            SendStatusAndProgress("Ensuring Directories...",0.1);
            await AppPaths.EnsureDirectories();
            SendStatusAndProgress("Initializing Settings...",0.4);
            await settingsManager.InitializeAsync();
            SendStatusAndProgress(LocalizationManager.Instance["Loading_Initializing_Fetch_Module"],0.5);
            await fetchManager.OnStartupAsync();
            SendStatusAndProgress(LocalizationManager.Instance["Loading_Initializing_Cache_Module"],0.6);
            await cacheManager.InitializeAsync();
            SendStatusAndProgress(LocalizationManager.Instance["Loading_Initializing_Playlist_Module"],0.7);
            await playlistRepository.InitializeAsync();
            SendStatusAndProgress(LocalizationManager.Instance["Loading_Initializing_Eq_Module"],0.8);
            await AutoEqDatabase.LoadAsync();
            SendStatusAndProgress(LocalizationManager.Instance["Loading_Initializing_Media_Controls"], 0.9);
            mediaControls.Initialize();

            SendStatusAndProgress(LocalizationManager.Instance["Loading_Starting_Main_Page"],1);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Windows[0].Page.FadeToAsync(0, 400, Easing.CubicIn);

                var appShell = new AppShell();
                appShell.Opacity = 0;
                Windows[0].Page = appShell;

                await appShell.FadeToAsync(1, 300, Easing.CubicOut);
            });
        }

        private void SendStatusAndProgress(string status , double progress)
        {
            WeakReferenceMessenger.Default.Send(new LoadingStatusMessage(status));
            WeakReferenceMessenger.Default.Send(new LoadingProgressMessage(progress));
        }
    }
}