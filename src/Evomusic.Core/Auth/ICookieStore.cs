namespace Evomusic.Core.Auth;

/// <summary>Secure persistence for the Google session cookies (Keychain on iOS).</summary>
public interface ICookieStore
{
    Task<IReadOnlyList<StoredCookie>> LoadAsync();

    Task SaveAsync(IReadOnlyList<StoredCookie> cookies);

    Task ClearAsync();
}
