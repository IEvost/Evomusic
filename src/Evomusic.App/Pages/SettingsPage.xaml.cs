using Evomusic.App.Services;

namespace Evomusic.App.Pages;

/// <summary>Account and app info. Audio quality, cache and Last.fm settings arrive in later phases.</summary>
public partial class SettingsPage : ContentPage
{
	private readonly SessionService _session;

	public SettingsPage(SessionService session)
	{
		InitializeComponent();
		_session = session;
		_session.SessionChanged += async (_, _) => await LoadAccountAsync();
		BindingContext = this;
	}

	public string AccountName { get; private set; } = "Not signed in";

	public string? AccountHandle { get; private set; }

	public string? PhotoUrl { get; private set; }

	public bool IsSignedIn => _session.IsSignedIn;

	public bool ReportPlays
	{
		get => Preferences.Default.Get(PlaybackService.ReportPlaysPreference, true);
		set => Preferences.Default.Set(PlaybackService.ReportPlaysPreference, value);
	}

	public string VersionText => $"Evomusic {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadAccountAsync();
	}

	private async Task LoadAccountAsync()
	{
		try
		{
			var account = _session.Music is { } music ? await music.GetAccountAsync() : null;
			AccountName = account?.Name ?? (_session.IsSignedIn ? "Signed in" : "Not signed in");
			AccountHandle = account?.Handle;
			PhotoUrl = account?.PhotoUrl;
		}
		catch (Exception ex)
		{
			AccountName = "Signed in";
			AccountHandle = $"Could not load account: {ex.Message}";
		}

		foreach (var property in new[] { nameof(AccountName), nameof(AccountHandle), nameof(PhotoUrl), nameof(IsSignedIn) })
			OnPropertyChanged(property);
	}

	private async void OnSignOutClicked(object? sender, EventArgs e)
	{
		if (await DisplayActionSheetAsync("Sign out of YouTube Music?", "Cancel", "Sign out") == "Sign out")
			await _session.SignOutAsync();
	}
}
