using System.Text.RegularExpressions;

namespace Evomusic.Core.InnerTube;

/// <summary>Web client identity scraped from the <c>ytcfg</c> of the music.youtube.com page.</summary>
public sealed record ClientContext(string ClientVersion, string? VisitorData, bool LoggedIn);

public static partial class YtcfgParser
{
    public static ClientContext Parse(string html)
    {
        var version = ClientVersionRegex().Match(html);
        if (!version.Success)
            throw new InvalidDataException("INNERTUBE_CLIENT_VERSION not found in the music.youtube.com page.");

        var visitorData = VisitorDataRegex().Match(html);
        var loggedIn = LoggedInRegex().Match(html);

        return new ClientContext(
            version.Groups[1].Value,
            visitorData.Success ? visitorData.Groups[1].Value : null,
            loggedIn.Success && loggedIn.Groups[1].Value == "true");
    }

    [GeneratedRegex("\"INNERTUBE_CLIENT_VERSION\":\"([^\"]+)\"")]
    private static partial Regex ClientVersionRegex();

    [GeneratedRegex("\"VISITOR_DATA\":\"([^\"]+)\"")]
    private static partial Regex VisitorDataRegex();

    [GeneratedRegex("\"LOGGED_IN\":(true|false)")]
    private static partial Regex LoggedInRegex();
}
