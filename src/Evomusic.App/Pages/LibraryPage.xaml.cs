using Evomusic.App.ViewModels;
using Evomusic.Core;

namespace Evomusic.App.Pages;

/// <summary>Library landing list, Apple Music style; each entry opens the matching YT Music library page.</summary>
public partial class LibraryPage : ContentPage
{
	public LibraryPage()
	{
		InitializeComponent();
		BindingContext = this;
	}

	public IReadOnlyList<Row> Rows { get; } =
	[
		new LinkRow("Liked Songs", "heart.fill", YouTubeMusicService.LikedSongsId),
		new LinkRow("Playlists", "music.note.list", YouTubeMusicService.LibraryPlaylistsId),
		new LinkRow("Albums", "square.stack", YouTubeMusicService.LibraryAlbumsId),
		new LinkRow("Artists", "music.mic", YouTubeMusicService.LibraryArtistsId),
		new LinkRow("Songs", "music.note", YouTubeMusicService.LibrarySongsId),
		new LinkRow("History", "clock.arrow.circlepath", YouTubeMusicService.HistoryId),
	];
}
