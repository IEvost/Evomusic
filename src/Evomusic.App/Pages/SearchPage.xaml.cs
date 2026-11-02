using System.Collections.ObjectModel;
using Evomusic.App.Services;
using Evomusic.App.ViewModels;

namespace Evomusic.App.Pages;

/// <summary>YT Music search with live suggestions.</summary>
public partial class SearchPage : ContentPage
{
	private static readonly TimeSpan SuggestionDelay = TimeSpan.FromMilliseconds(250);

	private readonly SessionService _session;
	private CancellationTokenSource? _suggestionCts;
	private string? _lastSearch;

	public SearchPage(SessionService session)
	{
		InitializeComponent();
		_session = session;
		BindingContext = this;
	}

	public ObservableCollection<Row> Rows { get; } = [];

	public ObservableCollection<string> Suggestions { get; } = [];

	public bool ShowSuggestions { get; private set; }

	public bool ShowResults => !ShowSuggestions;

	public bool IsLoading { get; private set; }

	public string Message { get; private set; } = "Search YouTube Music";

	private async void OnQueryChanged(object? sender, TextChangedEventArgs e)
	{
		_suggestionCts?.Cancel();
		var text = e.NewTextValue?.Trim() ?? "";
		if (text.Length == 0 || text == _lastSearch || _session.Music is not { } music)
		{
			SetSuggestionsVisible(false);
			return;
		}

		var cts = _suggestionCts = new CancellationTokenSource();
		try
		{
			await Task.Delay(SuggestionDelay, cts.Token);
			var suggestions = await music.GetSearchSuggestionsAsync(text, cts.Token);
			if (cts.IsCancellationRequested)
				return;

			Suggestions.Clear();
			foreach (var suggestion in suggestions)
				Suggestions.Add(suggestion);
			SetSuggestionsVisible(Suggestions.Count > 0);
		}
		catch (OperationCanceledException)
		{
			// Superseded by newer typing.
		}
		catch (Exception)
		{
			// Suggestions are optional; searching still works.
		}
	}

	private async void OnSearchPressed(object? sender, EventArgs e)
	{
		QueryBar.Unfocus();
		await SearchAsync(QueryBar.Text);
	}

	private void OnResultsScrolled(object? sender, ItemsViewScrolledEventArgs e)
	{
		if (QueryBar.IsFocused)
			QueryBar.Unfocus();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		QueryBar.Unfocus();
	}

	private async void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is not string suggestion)
			return;

		((CollectionView)sender!).SelectedItem = null;
		_lastSearch = suggestion;
		QueryBar.Text = suggestion;
		QueryBar.Unfocus();
		await SearchAsync(suggestion);
	}

	private async Task SearchAsync(string? query)
	{
		query = query?.Trim();
		_suggestionCts?.Cancel();
		SetSuggestionsVisible(false);
		if (string.IsNullOrEmpty(query))
			return;

		if (_session.Music is not { } music)
		{
			SetMessage("Sign in on the Home tab to search.");
			return;
		}

		_lastSearch = query;
		Rows.Clear();
		SetLoading(true);
		try
		{
			var page = await music.SearchAsync(query);
			foreach (var row in RowBuilder.Build(page))
				Rows.Add(row);
			SetMessage(Rows.Count == 0 ? "No results." : "");
		}
		catch (Exception ex)
		{
			SetMessage($"Search failed: {ex.Message}");
		}
		finally
		{
			SetLoading(false);
		}
	}

	private void SetSuggestionsVisible(bool visible)
	{
		ShowSuggestions = visible;
		OnPropertyChanged(nameof(ShowSuggestions));
		OnPropertyChanged(nameof(ShowResults));
	}

	private void SetLoading(bool loading)
	{
		IsLoading = loading;
		OnPropertyChanged(nameof(IsLoading));
	}

	private void SetMessage(string message)
	{
		Message = message;
		OnPropertyChanged(nameof(Message));
	}
}
