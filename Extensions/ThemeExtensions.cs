using MauiMixTube.Models.Settings;


namespace MauiMixTube.Extensions
{
    public static class ThemeExtensions
    {
        public static AppTheme ToAppTheme(this ThemeMode mode) => mode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            ThemeMode.System => AppTheme.Unspecified,
            _ => AppTheme.Unspecified
        };

        public static ThemeMode Next(this ThemeMode mode) => mode switch
        {
            ThemeMode.Light => ThemeMode.Dark,
            ThemeMode.Dark => ThemeMode.System,
            ThemeMode.System => ThemeMode.Light,
            _ => ThemeMode.System
        };

        public static string ToDisplayString(this ThemeMode mode) => mode switch
        {
            ThemeMode.Light => "Light",
            ThemeMode.Dark => "Dark",
            ThemeMode.System => "System",
            _ => "System"
        };

        public static ThemeMode ToThemeMode(this AppTheme theme) => theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            AppTheme.Unspecified => ThemeMode.System,
            _ => ThemeMode.System
        };
    }
}
