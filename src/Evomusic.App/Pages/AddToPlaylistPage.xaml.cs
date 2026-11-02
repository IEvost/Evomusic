using Evomusic.App.Services;
using Evomusic.Core.Models;

namespace Evomusic.App.Pages;

/// <summary>Picks one of the account's playlists (or creates one) for a track.</summary>
public partial class AddToPlaylistPage : ContentPage
{
	private readonly SessionService _session;
	private readonly Track _track;

	public AddToPlaylistPage(SessionService session, Track track)
	{
		InitializeComponent();
		_session = session;
		_track = track;
		BindingContext = this;
	}

	public IReadOnlyList<MediaItem> Playlists { get; private set; } = [];

	public string Message { get; private set; } = "Loading playlists…";

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_session.Music is not { } music)
			return;

		try
		{
			Playlists = await music.GetLibraryPlaylistsAsync();
			Message = Playlists.Count == 0 ? "No playlists yet." : "";
		}
		catch (Exception ex)
		{
			Message = $"Could not load playlists: {ex.Message}";
		}

		OnPropertyChanged(nameof(Playlists));
		OnPropertyChanged(nameof(Message));
	}

	private async void OnPlaylistTapped(object? sender, TappedEventArgs e)
	{
		if (sender is not BindableObject { BindingContext: MediaItem { BrowseId: { } playlistId } playlist } || _session.Music is not { } music)
			return;

		try
		{
			await music.AddToPlaylistAsync(playlistId, _track.VideoId);
			await Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			// Playlists owned by others can't be edited; YouTube rejects those.
			await DisplayAlertAsync("Not added", $"\"{playlist.Title}\": {ex.Message}", "OK");
		}
	}

	private async void OnNewPlaylistTapped(object? sender, TappedEventArgs e)
	{
		if (_session.Music is not { } music)
			return;

		var title = await DisplayPromptAsync("New Playlist", "Name", "Create", "Cancel", initialValue: _track.Title);
		if (string.IsNullOrWhiteSpace(title))
			return;

		try
		{
			await music.CreatePlaylistAsync(title.Trim(), [_track.VideoId]);
			await Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Not created", ex.Message, "OK");
		}
	}

	private async void OnCancelClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
