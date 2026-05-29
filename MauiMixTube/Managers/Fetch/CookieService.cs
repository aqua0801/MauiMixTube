using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Helpers;
using MauiMixTube.Messages;
using System.Diagnostics;

namespace MauiMixTube.Managers.Fetch
{

    /// <summary>
    /// Temporary not using , will implement a more robust cookie management in the future 
    /// </summary>
    public sealed class CookieService
    {
        private readonly string _cookiePath = AppPaths.CookieFile;
        private Timer? _refreshTimer;

        private string? _userAgent;
        public string? UserAgent => _userAgent;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await RefreshAsync(ct);

            _refreshTimer = new Timer(
                async _ => await RefreshAsync(CancellationToken.None),
                null,
                TimeSpan.FromHours(2),
                TimeSpan.FromHours(2));
        }

        private async Task RefreshAsync(CancellationToken ct)
        {
            Console.WriteLine("[Cookie] Refreshing...");
            var cookieSuccess = await TryGetCookieAsync(ct);
            var userAgent = await GetUserAgentAsync(ct);

            if(userAgent is not null)
            {
                await File.WriteAllTextAsync(AppPaths.UserAgentFile, _userAgent, ct);
            }

            if(cookieSuccess)
            {
                WeakReferenceMessenger.Default
                    .Send(new CookieRefreshedMessage(_cookiePath, _userAgent));
            }
        }

        private async Task<bool> TryGetCookieAsync(CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = AppPaths.YtDlpBinary,
                Arguments = $"--cookies-from-browser firefox --cookies \"{_cookiePath}\" --skip-download \"\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi)!;
                await process.WaitForExitAsync(ct);

                if (process.ExitCode != 0)
                {
                    var err = await process.StandardError.ReadToEndAsync(ct);
                    Console.WriteLine($"[Cookie] refresh failed: {err}");
                }
                else
                {
                    Console.WriteLine($"[Cookie] refreshed at {DateTime.Now:HH:mm:ss}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cookie] {ex.Message}");
            }
            return false;
        }


        private async Task<string?> GetUserAgentAsync(CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = AppPaths.YtDlpBinary,
                Arguments = "--cookies-from-browser firefox --print user_agent",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi)!;
                var output = await process.StandardOutput.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                return process.ExitCode == 0 ? output.Trim() : null;
            }
            catch(Exception ex) 
            {
                Console.WriteLine($"[UA] {ex}");
                return null; 
            }
        }

    }
}
