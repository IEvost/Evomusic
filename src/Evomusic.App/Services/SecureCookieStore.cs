using System.Text.Json;
using Evomusic.Core.Auth;

namespace Evomusic.App.Services;

/// <summary>Keeps the Google session cookies in the iOS Keychain via MAUI SecureStorage.</summary>
public sealed class SecureCookieStore : ICookieStore
{
	private const string Key = "google_session_cookies";

	public async Task<IReadOnlyList<StoredCookie>> LoadAsync()
	{
		var json = await SecureStorage.Default.GetAsync(Key);
		return json is null ? [] : JsonSerializer.Deserialize(json, AuthJsonContext.Default.ListStoredCookie) ?? [];
	}

	public Task SaveAsync(IReadOnlyList<StoredCookie> cookies) =>
		SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(cookies.ToList(), AuthJsonContext.Default.ListStoredCookie));

	public Task ClearAsync()
	{
		SecureStorage.Default.Remove(Key);
		return Task.CompletedTask;
	}
}
