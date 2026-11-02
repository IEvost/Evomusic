using Evomusic.App.Pages;
using Evomusic.Core.Models;

namespace Evomusic.App.Services;

/// <summary>What happens when any item is tapped anywhere: play it in context, or open its page.</summary>
public static class Navigator
{
	private static IServiceProvider Services => IPlatformApplication.Current!.Services;

	/// <summary>Dismisses the on-screen keyboard wherever it is open (e.g. still up after typing a search).</summary>
	public static void HideKeyboard()
	{
#if IOS
		UIKit.UIApplication.SharedApplication.SendAction(new ObjCRuntime.Selector("resignFirstResponder"), null, null, null);
#endif
	}

	public static async Task OpenAsync(MediaItem item, IReadOnlyList<MediaItem> context)
	{
		HideKeyboard();
		if (item.IsPlayable)
		{
			var tracks = context.Select(Track.FromMediaItem).OfType<Track>().ToList();
			var index = tracks.FindIndex(t => t.VideoId == item.VideoId);
			await Services.GetRequiredService<PlaybackService>().PlayAsync(tracks, Math.Max(index, 0));
			return;
		}

		if (item.BrowseId is { } browseId)
			await OpenPageAsync(browseId, item.Title, item.Kind == MediaKind.Artist);
	}

	public static Task OpenPageAsync(string browseId, string title, bool isArtist = false) =>
		Shell.Current.Navigation.PushAsync(new DetailPage(Services.GetRequiredService<SessionService>(), browseId, title, isArtist));

	public static async Task PlayAllAsync(IEnumerable<MediaItem> items, bool shuffle)
	{
		var tracks = items.Select(Track.FromMediaItem).OfType<Track>().ToList();
		if (tracks.Count > 0)
			await Services.GetRequiredService<PlaybackService>().PlayAsync(tracks, shuffle ? Random.Shared.Next(tracks.Count) : 0, shuffle);
	}

	public static Task ShowAddToPlaylistAsync(Track track) =>
		Shell.Current.Navigation.PushModalAsync(new AddToPlaylistPage(Services.GetRequiredService<SessionService>(), track));

	public static Task ShowLyricsAsync() =>
		Shell.Current.Navigation.PushModalAsync(new LyricsPage(
			Services.GetRequiredService<PlaybackService>(),
			Services.GetRequiredService<SessionService>()));

	public static PlaybackService Playback => Services.GetRequiredService<PlaybackService>();

	public static Task ShowNowPlayingAsync() =>
		Shell.Current.Navigation.PushModalAsync(new NowPlayingPage(Services.GetRequiredService<PlaybackService>()));
}
