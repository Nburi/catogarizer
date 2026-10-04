using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Catogarizer.Core.Updates;

namespace Catogarizer.App.Services;

/// <summary>
/// Looks for a newer version a few seconds after start and then twice a day (the app lives in the
/// tray for days), entirely in the background. It only remembers what it found: the home and the
/// tray show a quiet note with a link to the download page. Nothing is downloaded or installed.
/// </summary>
public sealed class UpdateNotifier : IDisposable
{
    /// <summary>
    /// Where the website publishes its version.json (website/public/version.json).
    /// Empty = not configured: no check runs and nothing is requested.
    /// </summary>
    public const string VersionUrl = "";

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

    private readonly UpdateChecker? _checker;
    private readonly HttpClient? _http;
    private readonly CancellationTokenSource _stop = new();
    private Version? _dismissed;

    /// <summary>The newer release that was found, or null. Not hidden by <see cref="Dismiss"/>; see <see cref="Visible"/>.</summary>
    public UpdateInfo? Available { get; private set; }

    /// <summary>What the UI should show: an update the user hasn't dismissed this session.</summary>
    public UpdateInfo? Visible => Available is { } update && update.Latest != _dismissed ? update : null;

    /// <summary>Raised on a background thread when <see cref="Visible"/> may have changed.</summary>
    public event Action? Changed;

    public UpdateNotifier()
    {
        if (!Uri.TryCreate(VersionUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps) return;

        var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Catogarizer/{UpdateChecker.Normalize(current)}");
        _checker = new UpdateChecker(_http, url, current);
    }

    /// <summary>Starts the background loop. Returns at once; safe to call once at startup.</summary>
    public void Start()
    {
        if (_checker is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(FirstCheckDelay, _stop.Token);
                while (!_stop.IsCancellationRequested)
                {
                    var found = await _checker.CheckAsync(_stop.Token);
                    // A failed check keeps what an earlier one found; it doesn't make the note vanish.
                    if (found is not null && found != Available)
                    {
                        Available = found;
                        Changed?.Invoke();
                    }
                    await Task.Delay(CheckInterval, _stop.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        });
    }

    /// <summary>Hides the note for this version until the next start; a still newer release shows again.</summary>
    public void Dismiss()
    {
        _dismissed = Available?.Latest;
        Changed?.Invoke();
    }

    /// <summary>Opens the download page in the default browser. A browser that won't open is not worth an error.</summary>
    public void OpenDownloadPage()
    {
        if (Available is not { } update) return;
        try
        {
            Process.Start(new ProcessStartInfo(update.DownloadPage.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        _stop.Cancel(); // not disposed: the loop may still be reading the token while it winds down
        _http?.Dispose();
    }
}
