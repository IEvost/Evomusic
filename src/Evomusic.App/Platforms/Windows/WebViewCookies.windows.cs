using Evomusic.Core.Auth;
using Evomusic.Core.InnerTube;
using WebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace Evomusic.App.Services;

// Windows is a development preview only: lets the sign-in flow be tried on the PC.
public static partial class WebViewCookies
{
	public static partial void Configure(WebView webView)
	{
	}

	public static partial async Task<IReadOnlyList<StoredCookie>> ReadAsync(WebView webView)
	{
		if (webView.Handler?.PlatformView is not WebView2 { CoreWebView2: { } core })
			return [];

		var cookies = await core.CookieManager.GetCookiesAsync(InnerTubeClient.BaseUrl);
		return cookies
			.Select(c => new StoredCookie(
				c.Name,
				c.Value,
				c.Domain,
				c.Path,
				c.IsSession ? null : DateTimeOffset.FromUnixTimeSeconds((long)c.Expires),
				c.IsSecure,
				c.IsHttpOnly))
			.ToList();
	}

	public static partial Task ClearAsync() => Task.CompletedTask;
}
