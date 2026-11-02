using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Evomusic.Core.Streams;

/// <summary>
/// Resolves audio URLs with an InnerTube <c>player</c> request impersonating clients that still get direct,
/// non-ciphered stream URLs without Proof-of-Origin tokens. Mirrors YoutubeExplode 6.6.3, which can't be
/// referenced directly: its assembly contains duplicate type definitions that break the iOS linker (MT2231).
/// </summary>
public sealed partial class InnerTubeStreamResolver(HttpClient http, Func<CancellationToken, Task<string?>> visitorDataProvider) : IStreamResolver
{
    private const string PlayerUrl = "https://www.youtube.com/youtubei/v1/player?prettyPrint=false";

    // VISIONOS returns direct streams without PO tokens or deciphering (yt-dlp's default JS-less client).
    private static readonly PlayerClient VisionOs = new(
        new JsonObject
        {
            ["clientName"] = "VISIONOS",
            ["clientVersion"] = "1.02",
            ["deviceMake"] = "Apple",
            ["deviceModel"] = "RealityDevice17,1",
            ["osName"] = "visionOS",
            ["osVersion"] = "26.5.23O471",
        },
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15");

    // Fallback for content VISIONOS refuses (e.g. "made for kids").
    private static readonly PlayerClient Android = new(
        new JsonObject
        {
            ["clientName"] = "ANDROID",
            ["clientVersion"] = "21.26.364",
            ["androidSdkVersion"] = 30,
            ["osName"] = "Android",
            ["osVersion"] = "11",
        },
        "com.google.android.youtube/21.26.364 (Linux; U; Android 11) gzip");

    public InnerTubeStreamResolver(Func<CancellationToken, Task<string?>> visitorDataProvider)
        : this(new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All }), visitorDataProvider)
    {
    }

    private string? _fallbackVisitorData;

    public async Task<AudioStream> ResolveAsync(string videoId, CancellationToken cancellationToken = default)
    {
        // Without visitor data YouTube answers with a "confirm you're not a bot" LOGIN_REQUIRED.
        var visitorData = await visitorDataProvider(cancellationToken)
            ?? (_fallbackVisitorData ??= await FetchVisitorDataAsync(cancellationToken));

        string? reason = null;
        foreach (var client in (PlayerClient[])[VisionOs, Android])
        {
            var response = await SendPlayerRequestAsync(client, videoId, visitorData, cancellationToken);
            if (PlayerResponseParser.Parse(response, out reason) is { } stream)
                return stream;
        }

        throw new InvalidOperationException($"Not playable: {reason}");
    }

    private async Task<JsonNode> SendPlayerRequestAsync(PlayerClient client, string videoId, string? visitorData, CancellationToken cancellationToken)
    {
        var clientContext = client.Context.DeepClone().AsObject();
        clientContext["visitorData"] = visitorData;
        clientContext["hl"] = "en";
        clientContext["gl"] = "US";
        clientContext["utcOffsetMinutes"] = 0;

        var body = new JsonObject
        {
            ["videoId"] = videoId,
            ["contentCheckOk"] = true,
            ["racyCheckOk"] = true,
            ["context"] = new JsonObject { ["client"] = clientContext },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, PlayerUrl)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("User-Agent", client.UserAgent);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))
            ?? throw new InvalidOperationException("Empty player response.");
    }

    /// <summary>Anonymous visitor ID from the service-worker bootstrap data (same source YoutubeExplode uses).</summary>
    private async Task<string?> FetchVisitorDataAsync(CancellationToken cancellationToken)
    {
        var data = await http.GetStringAsync("https://www.youtube.com/sw.js_data", cancellationToken);
        var match = VisitorDataRegex().Match(data);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("\"(Cg[A-Za-z0-9_%-]{20,})\"")]
    private static partial Regex VisitorDataRegex();

    private sealed record PlayerClient(JsonObject Context, string UserAgent);
}
