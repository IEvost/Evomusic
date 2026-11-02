namespace Evomusic.Core.Auth;

/// <summary>
/// Parses a <c>Cookie:</c> header copied from desktop browser DevTools on music.youtube.com
/// (fallback sign-in when Google blocks the in-app WebView).
/// </summary>
public static class CookieHeaderParser
{
    public static IReadOnlyList<StoredCookie> Parse(string header, string domain = ".youtube.com")
    {
        var text = header.Trim();
        if (text.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
            text = text["Cookie:".Length..];

        var cookies = new List<StoredCookie>();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            cookies.Add(new StoredCookie(part[..separator], part[(separator + 1)..], domain));
        }

        return cookies;
    }

    /// <summary>True when the set contains the cookie needed to sign InnerTube requests.</summary>
    public static bool HasAuthCookie(IEnumerable<StoredCookie> cookies) =>
        cookies.Any(c => c.Name is "SAPISID" or "__Secure-3PAPISID");
}
