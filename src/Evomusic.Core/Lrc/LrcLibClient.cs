using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Evomusic.Core.Models;

namespace Evomusic.Core.Lrc;

/// <summary>Time-synced lyrics from LRCLIB (https://lrclib.net), a free community lyrics database.</summary>
public sealed partial class LrcLibClient(HttpClient http)
{
    public LrcLibClient() : this(CreateHttpClient())
    {
    }

    public async Task<Models.Lyrics?> GetAsync(string artist, string title, TimeSpan? duration, CancellationToken cancellationToken = default)
    {
        var query = $"artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}";
        if (duration is { } d)
            query += $"&duration={(int)Math.Round(d.TotalSeconds)}";

        using var response = await http.GetAsync($"https://lrclib.net/api/get?{query}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var synced = Parse(json?["syncedLyrics"]?.GetValue<string>());
        var plain = json?["plainLyrics"]?.GetValue<string>() ?? string.Join('\n', synced.Select(l => l.Text));
        return string.IsNullOrWhiteSpace(plain) ? null : new Models.Lyrics(plain, synced, "LRCLIB");
    }

    /// <summary>Parses LRC text: <c>[01:23.45] line</c>. Lines without a timestamp are ignored.</summary>
    public static IReadOnlyList<LyricLine> Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
            return [];

        var lines = new List<LyricLine>();
        foreach (var line in lrc.Split('\n'))
        {
            var match = LrcLineRegex().Match(line);
            if (!match.Success)
                continue;

            var time = TimeSpan.FromMinutes(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
                + TimeSpan.FromSeconds(double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
            lines.Add(new LyricLine(time, match.Groups[3].Value.Trim()));
        }

        return lines.OrderBy(l => l.Time).ToList();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // LRCLIB asks clients to identify themselves.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Evomusic/1.0 (+https://github.com/IEvost/Evomusic)");
        return client;
    }

    [GeneratedRegex(@"^\[(\d+):(\d+(?:\.\d+)?)\](.*)$")]
    private static partial Regex LrcLineRegex();
}
