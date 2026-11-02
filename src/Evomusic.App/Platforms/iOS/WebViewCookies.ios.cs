using Evomusic.Core.Auth;
using Foundation;
using WebKit;

namespace Evomusic.App.Services;

public static partial class WebViewCookies
{
	// Desktop Safari identity: Google is less likely to reject sign-in, and music.youtube.com serves the full web client.
	private const string SafariUserAgent =
		"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15";

	public static partial void Configure(WebView webView)
	{
		if (webView.Handler?.PlatformView is WKWebView native)
			native.CustomUserAgent = SafariUserAgent;
	}

	public static partial async Task<IReadOnlyList<StoredCookie>> ReadAsync(WebView webView)
	{
		if (webView.Handler?.PlatformView is not WKWebView native)
			return [];

		var cookies = await native.Configuration.WebsiteDataStore.HttpCookieStore.GetAllCookiesAsync();
		return cookies
			.Where(c => c.Domain.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
			.Select(c => new StoredCookie(
				c.Name,
				c.Value,
				c.Domain,
				c.Path,
				c.ExpiresDate is { } expires ? new DateTimeOffset((DateTime)expires) : null,
				c.IsSecure,
				c.IsHttpOnly))
			.ToList();
	}

	public static partial Task ClearAsync() =>
		WKWebsiteDataStore.DefaultDataStore.RemoveDataOfTypesAsync(WKWebsiteDataStore.AllWebsiteDataTypes, NSDate.DistantPast);
}
