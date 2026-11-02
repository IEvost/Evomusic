using Evomusic.App.Services;
using Evomusic.Core.Auth;

namespace Evomusic.App.Pages;

public partial class CookiePastePage : ContentPage
{
	private readonly SessionService _session;

	public CookiePastePage(SessionService session)
	{
		InitializeComponent();
		_session = session;
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		var cookies = CookieHeaderParser.Parse(CookieEditor.Text ?? "");
		if (!CookieHeaderParser.HasAuthCookie(cookies))
		{
			ErrorLabel.Text = "No SAPISID cookie found. Copy the full Cookie header from a signed-in music.youtube.com request.";
			ErrorLabel.IsVisible = true;
			return;
		}

		await _session.SignInAsync(cookies);
		await Navigation.PopModalAsync();
	}
}
