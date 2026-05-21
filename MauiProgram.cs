using CommunityToolkit.Maui;
using MauiMixTube.Audio;
using MauiMixTube.Extensions;
using MauiMixTube.Managers;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Managers.Fetch.Handlers;
using MauiMixTube.Models.Fetch;
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

            // ── Bilibili (未實作，需要時取消註解) ──────────────────────────────
            //
            // Bilibili 需要自訂 HttpClientHandler（gzip解壓 + Referer）
            // 所以不能讓它自己管，要從外部注入
            //
            // Step 1: 註冊命名 HttpClient
            // builder.Services.AddHttpClient("bilibili")
            //     .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            //     {
            //         AutomaticDecompression =
            //             System.Net.DecompressionMethods.GZip |
            //             System.Net.DecompressionMethods.Deflate
            //     })
            //     .ConfigureHttpClient(client =>
            //     {
            //         client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");
            //     });
            //
            // Step 2: 註冊 FetchService 實作
            // builder.Services.AddSingleton<FetchService, BilibiliFetchService>();
            //
            // BilibiliFetchService 建構子：
            // public BilibiliFetchService(IHttpClientFactory factory)
            // {
            //     _http = factory.CreateClient("bilibili");
            // }
            // ────────────────────────────────────────────────────────────────────

            builder.Services.AddSingleton<FetchManager>();
            builder.Services.AddSingleton<AudioPipeline>();
            builder.Services.AddSingleton<CacheManager>();
            builder.Services.AddSingleton<PlaylistManager>();
            builder.Services.AddSingleton<PlaylistRepository>();
            builder.Services.AddTransient<AddSourcePopup>();

            Routing.RegisterRoute("settings", typeof(SettingsPage));

            return builder.Build();
        }
    }
}
