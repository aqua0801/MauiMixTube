using CommunityToolkit.Maui;
using MauiMixTube.Audio;
using MauiMixTube.Extensions;
using MauiMixTube.Managers;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Managers.Fetch.Handlers;
using MauiMixTube.Managers.Fetch.Services;
using MauiMixTube.Models.Fetch;
using MauiMixTube.Models.Settings;
using MauiMixTube.ViewModels;
using MauiMixTube.Views.Popups;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace MauiMixTube
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons-Regular");
                });

#if DEBUG
            var debugWriter = new DebugTextWriter();

            System.Console.SetOut(debugWriter);
            System.Console.SetError(debugWriter);

            builder.Logging.AddDebug();
#endif
            //page
            builder.Services.AddSingleton<SettingsManager>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<SettingsViewModel>();

            //fetch handler
            builder.Services.AddSingleton<FetchService, YoutubeFetchService>();
            builder.Services.AddSingleton<FetchService, BilibiliFetchService>();
            builder.Services.AddSingleton<FetchManager>();
            builder.Services.AddSingleton<AudioPipeline>();
            builder.Services.AddSingleton<CacheManager>();
            builder.Services.AddSingleton<PlaylistManager>();
            builder.Services.AddSingleton<PlaylistRepository>();
            builder.Services.AddSingleton<BilibiliDownloader>();
            builder.Services.AddTransient<AddSourcePopup>();

            Routing.RegisterRoute("settings", typeof(SettingsPage));

            return builder.Build();
        }
    }
}
