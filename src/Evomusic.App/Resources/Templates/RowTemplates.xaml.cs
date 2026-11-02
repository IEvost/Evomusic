using System.Globalization;
using Evomusic.App.Services;
using Evomusic.App.ViewModels;
using Evomusic.Core.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Evomusic.App.Resources.Templates;

/// <summary>Row templates shared by every list screen, with their tap handling in one place.</summary>
public partial class RowTemplates : ResourceDictionary
{
	public RowTemplates()
	{
		InitializeComponent();
	}

	private async void OnItemRowTapped(object? sender, TappedEventArgs e)
	{
		if (sender is not Element { BindingContext: ItemRow row } view || FindRows(view) is not { } rows)
			return;

		await Navigator.OpenAsync(row.Item, RowBuilder.QueueFor(rows, row));
	}

	private async void OnCardTapped(object? sender, TappedEventArgs e)
	{
		if (sender is not Element { BindingContext: MediaItem item } view)
			return;

		var carousel = view.FindAncestor<CollectionView>();
		var context = (carousel?.ItemsSource as IEnumerable<MediaItem>)?.ToList() ?? [item];
		await Navigator.OpenAsync(item, context);
	}

	private async void OnLinkRowTapped(object? sender, TappedEventArgs e)
	{
		if (sender is BindableObject { BindingContext: LinkRow link })
			await Navigator.OpenPageAsync(link.BrowseId, link.Title);
	}

	/// <summary>Press-and-hold on a song: Play Next, Add to Queue, Start Radio, Add to Playlist.</summary>
	private async void OnLongPressed(object? sender, View view)
	{
		var item = view.BindingContext switch
		{
			ItemRow row => row.Item,
			MediaItem media => media,
			_ => null,
		};
		if (item is null || Track.FromMediaItem(item) is not { } track || Shell.Current?.CurrentPage is not { } page)
			return;

		Navigator.HideKeyboard();
		const string playNext = "Play Next";
		const string addToQueue = "Add to Queue";
		const string startRadio = "Start Radio";
		const string addToPlaylist = "Add to Playlist…";
		switch (await page.DisplayActionSheetAsync(track.Title, "Cancel", null, playNext, addToQueue, startRadio, addToPlaylist))
		{
			case playNext:
				Navigator.Playback.PlayNext(track);
				break;
			case addToQueue:
				Navigator.Playback.AddToQueue(track);
				break;
			case startRadio:
				await Navigator.Playback.StartRadioAsync(track);
				break;
			case addToPlaylist:
				await Navigator.ShowAddToPlaylistAsync(track);
				break;
		}
	}

	private async void OnPlayAllClicked(object? sender, EventArgs e) => await PlayAllAsync(sender, shuffle: false);

	private async void OnShuffleAllClicked(object? sender, EventArgs e) => await PlayAllAsync(sender, shuffle: true);

	private static async Task PlayAllAsync(object? sender, bool shuffle)
	{
		if (sender is Element view && FindRows(view) is { } rows)
		{
			// The page's first track list (album/playlist tracks, artist top songs).
			var first = rows.OfType<ItemRow>().FirstOrDefault(r => r.Item.IsPlayable);
			if (first is not null)
				await Navigator.PlayAllAsync(RowBuilder.QueueFor(rows, first), shuffle);
		}
	}

	private static IEnumerable<Row>? FindRows(Element view) =>
		view.FindAncestor<CollectionView>()?.ItemsSource as IEnumerable<Row>;
}

file static class ElementExtensions
{
	public static T? FindAncestor<T>(this Element element) where T : Element
	{
		for (var parent = element.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is T match)
				return match;
		}

		return null;
	}
}

public sealed class RowTemplateSelector : DataTemplateSelector
{
	public DataTemplate? Header { get; set; }

	public DataTemplate? Title { get; set; }

	public DataTemplate? Item { get; set; }

	public DataTemplate? Carousel { get; set; }

	public DataTemplate? Link { get; set; }

	protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => item switch
	{
		HeaderRow => Header!,
		TitleRow => Title!,
		CarouselRow => Carousel!,
		LinkRow => Link!,
		_ => Item!,
	};
}

/// <summary>Artists are shown round, everything else as rounded squares.</summary>
public sealed class ArtworkShapeConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is MediaKind.Artist ? new Ellipse() : new RoundRectangle { CornerRadius = 6 };

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Artist banners are wide; album and playlist covers square.</summary>
public sealed class ArtworkWidthConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is true ? 360.0 : 240.0;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
