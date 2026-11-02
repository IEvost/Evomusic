using System.Text.Json.Nodes;
using Evomusic.Core.InnerTube.Parsers;

namespace Evomusic.Core.Streams;

/// <summary>Picks the playable audio stream out of an InnerTube <c>player</c> response.</summary>
public static class PlayerResponseParser
{
    /// <summary>Returns the best AAC/MP4 audio stream, or null with the reason YouTube gave.</summary>
    public static AudioStream? Parse(JsonNode response, out string? unplayableReason)
    {
        var status = response.Str("playabilityStatus", "status");
        if (status != "OK")
        {
            unplayableReason = response.Str("playabilityStatus", "reason") ?? status ?? "No playability status.";
            return null;
        }

        // AVPlayer can't decode Opus/WebM, so only AAC in MP4 (itag 140 without Premium) is usable.
        // Formats without a direct URL are SABR/ciphered and can't be fetched directly.
        var best = response.Items("streamingData", "adaptiveFormats")
            .Where(f => f.Str("mimeType")?.StartsWith("audio/mp4", StringComparison.Ordinal) == true)
            .Where(f => !string.IsNullOrEmpty(f.Str("url")))
            .MaxBy(f => f.At("bitrate")?.GetValue<long>() ?? 0);

        if (best is null)
        {
            unplayableReason = "No directly playable AAC audio stream.";
            return null;
        }

        unplayableReason = null;
        var mimeType = best.Str("mimeType")!;
        return new AudioStream(
            new Uri(best.Str("url")!),
            "mp4",
            ExtractCodec(mimeType),
            best.At("bitrate")?.GetValue<long>() ?? 0,
            long.TryParse(best.Str("contentLength"), out var size) ? size : 0);
    }

    // audio/mp4; codecs="mp4a.40.2" -> mp4a.40.2
    private static string ExtractCodec(string mimeType)
    {
        var start = mimeType.IndexOf("codecs=\"", StringComparison.Ordinal);
        if (start < 0)
            return "unknown";

        start += "codecs=\"".Length;
        var end = mimeType.IndexOf('"', start);
        return end > start ? mimeType[start..end] : "unknown";
    }
}
