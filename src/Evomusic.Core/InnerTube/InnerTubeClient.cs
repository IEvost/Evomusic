using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Evomusic.Core.Auth;

namespace Evomusic.Core.InnerTube;

public sealed class InnerTubeException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

/// <summary>
/// Emulates the YouTube Music web client (<c>WEB_REMIX</c>): same endpoints, headers and cookies,
/// so responses match what music.youtube.com shows for the signed-in account.
/// </summary>
public sealed class InnerTubeClient : IDisposable
{
    public const string BaseUrl = "https://music.youtube.com";

    private const string ClientName = "WEB_REMIX";
    private const string ClientNameId = "67";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";

    private static readonly Uri BaseUri = new(BaseUrl);

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public InnerTubeClient(IEnumerable<StoredCookie> cookies, HttpMessageHandler? handler = null, TimeProvider? time = null)
    {
        foreach (var cookie in cookies)
        {
            try
            {
                _cookies.Add(cookie.ToNetCookie());
            }
            catch (CookieException)
            {
                // Malformed cookie from the browser; the session can work without it.
            }
        }

        // Cookies are attached and collected manually so the client behaves the same with any handler.
        _http = new HttpClient(handler ?? new SocketsHttpHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
        });
        _time = time ?? TimeProvider.System;
    }

    public ClientContext? Context { get; private set; }

    /// <summary>Raised when the server rotates cookies; the new set must be persisted.</summary>
    public event EventHandler? CookiesChanged;

    public IReadOnlyList<StoredCookie> ExportCookies() =>
        _cookies.GetAllCookies().Select(StoredCookie.FromNetCookie).ToList();

    public async Task<ClientContext> InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, BaseUri);
        var html = await SendAsync(request, cancellationToken);
        Context = YtcfgParser.Parse(html);
        return Context;
    }

    public async Task<JsonNode> PostAsync(string endpoint, JsonObject? body = null, CancellationToken cancellationToken = default)
    {
        var context = Context ?? await InitializeAsync(cancellationToken);

        body ??= [];
        body["context"] = new JsonObject
        {
            ["client"] = new JsonObject
            {
                ["clientName"] = ClientName,
                ["clientVersion"] = context.ClientVersion,
                ["hl"] = "en",
                ["visitorData"] = context.VisitorData,
            },
            ["user"] = new JsonObject(),
        };

        using var request = CreateRequest(HttpMethod.Post, new Uri(BaseUri, $"/youtubei/v1/{endpoint}?prettyPrint=false"));
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        request.Headers.Add("X-Youtube-Client-Name", ClientNameId);
        request.Headers.Add("X-Youtube-Client-Version", context.ClientVersion);
        if (context.VisitorData is not null)
            request.Headers.Add("X-Goog-Visitor-Id", context.VisitorData);

        var sapisid = FindSapisid();
        if (sapisid is not null)
            request.Headers.TryAddWithoutValidation("Authorization", SapisidHasher.CreateAuthorizationHeader(sapisid, _time.GetUtcNow()));

        var json = await SendAsync(request, cancellationToken);
        return JsonNode.Parse(json) ?? throw new InnerTubeException($"Empty response from '{endpoint}'.");
    }

    public void Dispose() => _http.Dispose();

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("Origin", BaseUrl);
        request.Headers.Add("X-Origin", BaseUrl);
        request.Headers.Referrer = BaseUri;
        request.Headers.Add("X-Goog-AuthUser", "0");

        var cookieHeader = _cookies.GetCookieHeader(uri);
        if (cookieHeader.Length > 0)
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

        return request;
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(request, cancellationToken);
        StoreResponseCookies(response);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var snippet = content.Length > 300 ? content[..300] : content;
            throw new InnerTubeException($"{(int)response.StatusCode} {response.ReasonPhrase} for {request.RequestUri?.AbsolutePath}: {snippet}", response.StatusCode);
        }

        return content;
    }

    private void StoreResponseCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return;

        var changed = false;
        foreach (var value in values)
        {
            try
            {
                _cookies.SetCookies(BaseUri, value);
                changed = true;
            }
            catch (CookieException)
            {
                // Cookie for a domain we don't track; ignore.
            }
        }

        if (changed)
            CookiesChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? FindSapisid()
    {
        var cookies = _cookies.GetCookies(BaseUri);
        return cookies["SAPISID"]?.Value ?? cookies["__Secure-3PAPISID"]?.Value;
    }
}
