using System.Security.Cryptography;
using System.Text;

namespace Evomusic.Core.Auth;

/// <summary>
/// Builds the <c>Authorization: SAPISIDHASH ...</c> header value that the YouTube Music web client
/// sends with every authenticated InnerTube request.
/// </summary>
public static class SapisidHasher
{
    public const string MusicOrigin = "https://music.youtube.com";

    public static string CreateAuthorizationHeader(string sapisid, DateTimeOffset now, string origin = MusicOrigin)
    {
        ArgumentException.ThrowIfNullOrEmpty(sapisid);

        var timestamp = now.ToUnixTimeSeconds();
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes($"{timestamp} {sapisid} {origin}"));
        return $"SAPISIDHASH {timestamp}_{Convert.ToHexStringLower(hash)}";
    }
}
