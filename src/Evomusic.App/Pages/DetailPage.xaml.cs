using System.Collections.ObjectModel;
using Evomusic.App.Services;
using Evomusic.App.ViewModels;

namespace Evomusic.App.Pages;

/// <summary>Any browsable page: album, playlist, artist or a library section. Long lists load more while scrolling.</summary>
public partial class DetailPage : ContentPage
{
	private readonly SessionService _session;
	private readonly string _browseId;
	private readonly bool _isArtist;
	private string? _continuation;
	private int _continuationGroup;
	private bool _loaded;

	public DetailPage(SessionService session, string browseId, string title, bool isArtist)
	{
		InitializeComponent();
		_session = session;
		_browseId = browseId;
		_isArtist = isArtist;
		Title = title;
		BindingContext = this;
	}

	public ObservableCollection<Row> Rows { get; } = [];

	public bool IsLoading { get; private set; }

	public bool IsLoadingMore { get; private set; }

	public string? ErrorText { get; private set; }

	public bool HasError => ErrorText is not null;

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_loaded || _session.Music is not { } music)
			return;

		_loaded = true;
		SetFlag(nameof(IsLoading), () => IsLoading = true);
		try
		{
			var page = await music.BrowseAsync(_browseId);
			foreach (var row in RowBuilder.Build(page, _isArtist))
				Rows.Add(row);

			// Only the main track list (playlists, liked songs) pages through continuations.
			var paged = page.Sections.Select((s, i) => (Section: s, Group: i)).FirstOrDefault(x => x.Section.Continuation is not null);
			_continuation = paged.Section?.Continuation;
			_continuationGroup = paged.Group;
		}
		catch (Exception ex)
		{
			ErrorText = ex.Message;
			OnPropertyChanged(nameof(ErrorText));
			OnPropertyChanged(nameof(HasError));
		}
		finally
		{
			SetFlag(nameof(IsLoading), () => IsLoading = false);
		}
	}

	private async void OnRemainingItemsThresholdReached(object? sender, EventArgs e)
	{
		if (IsLoadingMore || _continuation is not { } token || _session.Music is not { } music)
			return;

		SetFlag(nameof(IsLoadingMore), () => IsLoadingMore = true);
		try
		{
			var more = await music.ContinueAsync(token);
			foreach (var item in more.Items)
				Rows.Add(new ItemRow(item, _continuationGroup, ShowArtwork: true));
			_continuation = more.Continuation;
		}
		catch (Exception)
		{
			// Keep what is loaded; scrolling to the end again retries.
		}
		finally
		{
			SetFlag(nameof(IsLoadingMore), () => IsLoadingMore = false);
		}
	}

	private void SetFlag(string property, Action set)
	{
		set();
		OnPropertyChanged(property);
	}
}
