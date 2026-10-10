using System.Net;
using System.Text;
using Catogarizer.Core.Updates;

namespace Catogarizer.Core.Tests;

public sealed class UpdateCheckerTests
{
    private static readonly Uri VersionUrl = new("https://catogarizer.test/version.json");

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpMethod? LastMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastMethod = request.Method;
            return respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (UpdateChecker Checker, StubHandler Handler) Checker(string current, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
    {
        var handler = new StubHandler(respond);
        return (new UpdateChecker(new HttpClient(handler), VersionUrl, Version.Parse(current), timeout), handler);
    }

    private static (UpdateChecker Checker, StubHandler Handler) Serving(string current, string body, HttpStatusCode status = HttpStatusCode.OK) =>
        Checker(current, (_, _) => Task.FromResult(Json(body, status)));

    private static string VersionJson(string version) => "{ \"version\": \"" + version + "\" }";

    [Fact]
    public async Task NewerVersion_IsReportedWithTheFilesDownloadUrl()
    {
        var (checker, handler) = Serving("1.0.0", "{ \"version\": \"1.2.0\", \"url\": \"https://catogarizer.test/download\" }");

        var update = await checker.CheckAsync();

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 2, 0), update.Latest);
        Assert.Equal(new Uri("https://catogarizer.test/download"), update.DownloadPage);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.9.9", "1.10.0")]   // compared as numbers, not as text
    [InlineData("1.0.0", "2.0.0")]
    public async Task NewerVersion_IsDetectedNumerically(string current, string latest)
    {
        var (checker, _) = Serving(current, VersionJson(latest));

        Assert.Equal(Version.Parse(latest), (await checker.CheckAsync())?.Latest);
    }

    [Fact]
    public async Task NewerVersion_WithoutUrl_FallsBackToTheSiteItCameFrom()
    {
        var (checker, _) = Serving("1.0.0", VersionJson("1.1.0"));

        Assert.Equal(new Uri("https://catogarizer.test/"), (await checker.CheckAsync())!.DownloadPage);
    }

    [Theory]
    [InlineData("http://catogarizer.test/download")]
    [InlineData("file:///C:/evil.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public async Task NonHttpsUrlInTheFile_IsIgnored(string url)
    {
        var (checker, _) = Serving("1.0.0", "{ \"version\": \"1.1.0\", \"url\": \"" + url + "\" }");

        Assert.Equal(new Uri("https://catogarizer.test/"), (await checker.CheckAsync())!.DownloadPage);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0.0", "1.0.0")]   // assembly versions have four parts
    [InlineData("1.2.0", "1.1.9")]
    [InlineData("2.0.0", "1.99.99")]
    public async Task SameOrOlderVersion_IsNoUpdate(string current, string advertised)
    {
        var (checker, _) = Serving(current, VersionJson(advertised));

        Assert.Null(await checker.CheckAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{ \"version\": 2 }")]
    [InlineData("{ \"version\": \"\" }")]
    [InlineData("{ \"version\": \"latest\" }")]
    [InlineData("{ \"version\": \"2.0\" }")]
    [InlineData("{ \"version\": \"2.0.0-beta\" }")]
    [InlineData("{ \"version\": \"v2.0.0\" }")]
    [InlineData("{ \"version\": \"99999999999.0.0\" }")]
    public async Task MalformedAnswer_IsNoUpdateAndNeverThrows(string body)
    {
        var (checker, _) = Serving("1.0.0", body);

        Assert.Null(await checker.CheckAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ErrorStatus_IsNoUpdate(HttpStatusCode status)
    {
        var (checker, _) = Serving("1.0.0", VersionJson("9.0.0"), status);

        Assert.Null(await checker.CheckAsync());
    }

    [Fact]
    public async Task NetworkFailure_IsNoUpdateAndNeverThrows()
    {
        var (checker, handler) = Checker("1.0.0", (_, _) => throw new HttpRequestException("No such host is known."));

        Assert.Null(await checker.CheckAsync());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SlowServer_IsCutOffByTheTimeout()
    {
        var (checker, _) = Checker("1.0.0", async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Json(VersionJson("9.0.0"));
        }, timeout: TimeSpan.FromMilliseconds(50));

        var started = DateTime.UtcNow;
        Assert.Null(await checker.CheckAsync());
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CallerCancellation_IsNoUpdateAndNeverThrows()
    {
        var (checker, _) = Checker("1.0.0", async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Json(VersionJson("9.0.0"));
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.Null(await checker.CheckAsync(cts.Token));
    }

    [Fact]
    public async Task OversizedAnswer_IsIgnored()
    {
        var padding = new string(' ', 20_000);
        var (checker, _) = Serving("1.0.0", "{ \"version\": \"9.0.0\"" + padding + " }");

        Assert.Null(await checker.CheckAsync());
    }
}
