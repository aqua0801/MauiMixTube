using CommunityToolkit.Maui;
using MauiMixTube.Audio;
using MauiMixTube.Audio.Eq;
using MauiMixTube.Extensions;
using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Managers.Fetch.Handlers;
using MauiMixTube.Managers.Fetch.Services;
using MauiMixTube.Managers.Media;
using MauiMixTube.Models.Fetch;
using MauiMixTube.Models.Settings;
using MauiMixTube.ViewModels;
using MauiMixTube.Views.Popups;
using Microsoft.Extensions.Logging;
using YoutubeDLSharp;

namespace MauiMixTube
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
#if DEBUG
            System.Environment.SetEnvironmentVariable("ALSOFT_LOGLEVEL", "3");
            Environment.SetEnvironmentVariable(
                "ALSOFT_LOGFILE",
                @".\@openal.log");
#endif
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
            builder.Services.AddSingleton<YoutubeDL>(_ => new YoutubeDL
            {
                YoutubeDLPath = AppPaths.YtDlpBinary,
                FFmpegPath = AppPaths.FfmpegBinary,
            });
            builder.Services.AddSingleton<FetchManager>();
            builder.Services.AddSingleton<AudioPipeline>();
            builder.Services.AddSingleton<CacheManager>();
            builder.Services.AddSingleton<PlaylistManager>();
            builder.Services.AddSingleton<AutoEqProcessor>();
            builder.Services.AddSingleton<PlaylistRepository>();
            builder.Services.AddSingleton<BilibiliDownloader>();
            builder.Services.AddTransient<AddSourcePopup>();

            builder.Services.AddSingleton<IMediaControlsService, MediaControlsService>();
            builder.Services.AddSingleton<IAudioDeviceWatcher, AudioDeviceWatcher>();

            Routing.RegisterRoute("settings", typeof(SettingsPage));

            return builder.Build();
        }
    }
}
