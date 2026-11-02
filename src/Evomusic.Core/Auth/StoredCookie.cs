using System.Net;

namespace Evomusic.Core.Auth;

/// <summary>Serializable copy of a browser cookie, persisted in the Keychain between app launches.</summary>
public sealed record StoredCookie(
    string Name,
    string Value,
    string Domain,
    string Path = "/",
    DateTimeOffset? Expires = null,
    bool Secure = true,
    bool HttpOnly = false)
{
    public Cookie ToNetCookie() => new(Name, Value, Path, Domain)
    {
        Secure = Secure,
        HttpOnly = HttpOnly,
        Expires = Expires?.UtcDateTime ?? DateTime.MinValue,
    };

    public static StoredCookie FromNetCookie(Cookie cookie) => new(
        cookie.Name,
        cookie.Value,
        cookie.Domain,
        cookie.Path,
        cookie.Expires == DateTime.MinValue ? null : new DateTimeOffset(cookie.Expires.ToUniversalTime()),
        cookie.Secure,
        cookie.HttpOnly);
}
