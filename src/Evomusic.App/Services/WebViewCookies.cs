using Evomusic.Core.Auth;

namespace Evomusic.App.Services;

/// <summary>Reads the Google session cookies out of the sign-in WebView (implemented per platform).</summary>
public static partial class WebViewCookies
{
	/// <summary>Applies platform settings (user agent) once the native WebView exists.</summary>
	public static partial void Configure(WebView webView);

	public static partial Task<IReadOnlyList<StoredCookie>> ReadAsync(WebView webView);

	/// <summary>Removes all web data so the next sign-in starts clean.</summary>
	public static partial Task ClearAsync();
}
