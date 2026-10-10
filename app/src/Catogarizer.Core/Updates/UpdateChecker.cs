using System.Text.Json;
using System.Text.RegularExpressions;

namespace Catogarizer.Core.Updates;

/// <summary>A newer release than the running one. <see cref="DownloadPage"/> is always an https URL.</summary>
public sealed record UpdateInfo(Version Latest, Uri DownloadPage);

/// <summary>
/// Asks the project's own website whether a newer version exists (a static <c>version.json</c>:
/// <c>{ "version": "x.y.z", "url": "https://..." }</c>). Read-only GET, nothing is downloaded or
/// installed. Every failure - offline, timeout, bad status, garbage in the file - ends as
/// "no update" and never throws, so the app just doesn't show a notice.
/// </summary>
public sealed partial class UpdateChecker
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>version.json is a few dozen bytes; anything bigger is not ours.</summary>
    private const int MaxResponseBytes = 16 * 1024;

    private readonly HttpClient _http;
    private readonly Uri _versionUrl;
    private readonly Version _current;
    private readonly TimeSpan _timeout;

    public UpdateChecker(HttpClient http, Uri versionUrl, Version current, TimeSpan? timeout = null)
    {
        _http = http;
        _versionUrl = versionUrl;
        _current = Normalize(current);
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>The update if the site advertises a newer version than the running one, otherwise null.</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_timeout);

            using var response = await _http.GetAsync(_versionUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength > MaxResponseBytes) return null;

            var body = await ReadBoundedAsync(response.Content, timeout.Token);
            return body is null ? null : Parse(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException
                                       or JsonException or InvalidOperationException or UriFormatException)
        {
            return null;
        }
    }

    private UpdateInfo? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
        if (!document.RootElement.TryGetProperty("version", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.String) return null;

        var text = versionElement.GetString()!.Trim();
        if (!SemVer().IsMatch(text) || !Version.TryParse(text, out var latest)) return null;
        if (latest <= _current) return null;

        return new UpdateInfo(latest, DownloadPageFrom(document.RootElement));
    }

    /// <summary>The file's own "url" if it is a valid https link, otherwise the site the file came from.</summary>
    private Uri DownloadPageFrom(JsonElement root)
    {
        if (root.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String
            && Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps)
            return url;

        return new Uri(_versionUrl.GetLeftPart(UriPartial.Authority) + "/");
    }

    private static async Task<string?> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxResponseBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
            total += read;
        return total > MaxResponseBytes ? null : System.Text.Encoding.UTF8.GetString(buffer, 0, total);
    }

    /// <summary>Assembly versions are four-part (1.0.0.0); releases are x.y.z.</summary>
    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex SemVer();
}
