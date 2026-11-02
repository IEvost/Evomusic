using Evomusic.App.Services;
using Evomusic.Core.Auth;

namespace Evomusic.App.Pages;

/// <summary>Google sign-in inside a WebView; captures the youtube.com session cookies once YT Music loads.</summary>
public partial class LoginPage : ContentPage
{
	// The same sign-in link music.youtube.com uses; it ends on music.youtube.com with youtube.com cookies set.
	private const string SignInUrl =
		"https://accounts.google.com/ServiceLogin?ltmpl=music&service=youtube&passive=true" +
		"&continue=https%3A%2F%2Fwww.youtube.com%2Fsignin%3Faction_handle_signin%3Dtrue%26next%3Dhttps%253A%252F%252Fmusic.youtube.com%252F";

	private readonly SessionService _session;
	private bool _completed;

	public LoginPage(SessionService session)
	{
		InitializeComponent();
		_session = session;
	}

	private void OnWebViewHandlerChanged(object? sender, EventArgs e)
	{
		if (SignInWebView.Handler is null)
			return;

		WebViewCookies.Configure(SignInWebView);
		SignInWebView.Source = SignInUrl;
	}

	private async void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
	{
		if (_completed || !Uri.TryCreate(e.Url, UriKind.Absolute, out var url) || url.Host != "music.youtube.com")
			return;

		try
		{
			var cookies = await WebViewCookies.ReadAsync(SignInWebView);
			if (!CookieHeaderParser.HasAuthCookie(cookies))
			{
				StatusLabel.Text = "YouTube Music opened, but you are not signed in yet. Use \"Sign in\" on the page.";
				return;
			}

			_completed = true;
			await _session.SignInAsync(cookies);
			await Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Could not read the session: {ex.Message}";
		}
	}

	private async void OnPasteCookiesClicked(object? sender, EventArgs e) =>
		await Navigation.PushAsync(new CookiePastePage(_session));

	private async void OnCancelClicked(object? sender, EventArgs e) =>
		await Navigation.PopModalAsync();
}
