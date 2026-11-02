using System.Net;
using System.Text.Json.Nodes;
using Evomusic.Core.Auth;
using Evomusic.Core.InnerTube;

namespace Evomusic.Core.Tests.InnerTube;

public class InnerTubeClientTests
{
    private const string HomePage = """<script>ytcfg.set({"VISITOR_DATA":"VD","INNERTUBE_CLIENT_VERSION":"1.2.3"});</script>""";

    [Fact]
    public async Task PostAsync_SendsWebClientHeadersCookiesAndContext()
    {
        var handler = new FakeHandler();
        var time = new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1700000000));
        using var client = new InnerTubeClient([new StoredCookie("SAPISID", "abc123/XYZ", ".youtube.com")], handler, time);

        await client.PostAsync("browse", new JsonObject { ["browseId"] = "FEmusic_home" });

        Assert.Equal(2, handler.Requests.Count);
        var (request, body) = handler.Requests[1];
        Assert.Equal("https://music.youtube.com/youtubei/v1/browse?prettyPrint=false", request.RequestUri!.ToString());
        Assert.Equal("SAPISIDHASH 1700000000_289fd730e8d0d6300b332703c750af560713e6b1", request.Headers.GetValues("Authorization").Single());
        Assert.Equal("SAPISID=abc123/XYZ", request.Headers.GetValues("Cookie").Single());
        Assert.Equal("67", request.Headers.GetValues("X-Youtube-Client-Name").Single());
        Assert.Equal("VD", request.Headers.GetValues("X-Goog-Visitor-Id").Single());

        var json = JsonNode.Parse(body!)!;
        Assert.Equal("FEmusic_home", (string?)json["browseId"]);
        Assert.Equal("WEB_REMIX", (string?)json["context"]!["client"]!["clientName"]);
        Assert.Equal("1.2.3", (string?)json["context"]!["client"]!["clientVersion"]);
    }

    [Fact]
    public async Task SetCookie_IsStoredAndRaisesCookiesChanged()
    {
        var handler = new FakeHandler { SetCookie = "__Secure-3PSIDTS=rotated; Domain=.youtube.com; Path=/; Secure" };
        using var client = new InnerTubeClient([new StoredCookie("SAPISID", "s", ".youtube.com")], handler);
        var raised = false;
        client.CookiesChanged += (_, _) => raised = true;

        await client.InitializeAsync();

        Assert.True(raised);
        Assert.Contains(client.ExportCookies(), c => c is { Name: "__Secure-3PSIDTS", Value: "rotated" });
    }

    [Fact]
    public async Task ErrorStatus_ThrowsInnerTubeException()
    {
        var handler = new FakeHandler { Status = HttpStatusCode.Unauthorized };
        using var client = new InnerTubeClient([], handler);

        var error = await Assert.ThrowsAsync<InnerTubeException>(() => client.InitializeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string? SetCookie { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));

            var isPage = request.Method == HttpMethod.Get;
            var response = new HttpResponseMessage(Status) { Content = new StringContent(isPage ? HomePage : "{}") };
            if (SetCookie is not null)
                response.Headers.Add("Set-Cookie", SetCookie);
            return response;
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
