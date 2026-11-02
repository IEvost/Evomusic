using Evomusic.App.Services;
using Evomusic.Core.Lrc;
using Evomusic.Core.Models;

namespace Evomusic.App.Pages;

public sealed record LyricRow(int Index, TimeSpan Time, string Text, bool IsCurrent)
{
	public double Opacity => IsCurrent ? 1 : 0.35;
}

/// <summary>
/// Lyrics for the playing track: time-synced from LRCLIB when available (current line follows playback),
/// otherwise the plain YT Music lyrics (LyricFind).
/// </summary>
public partial class LyricsPage : ContentPage
{
	private static readonly LrcLibClient LrcLib = new();

	private readonly PlaybackService _playback;
	private readonly SessionService _session;
	private readonly Track? _track;
	private IReadOnlyList<LyricLine> _synced = [];
	private IDispatcherTimer? _timer;
	private int _currentLine = -1;

	public LyricsPage(PlaybackService playback, SessionService session)
	{
		InitializeComponent();
		_playback = playback;
		_session = session;
		_track = playback.Current;
		BindingContext = this;
	}

	public string? TrackTitle => _track?.Title;

	public string? TrackArtist => _track?.Artist;

	public List<LyricRow> Lines { get; private set; } = [];

	public bool IsSynced => _synced.Count > 0;

	public bool IsPlain => !IsSynced;

	public string PlainText { get; private set; } = "Loading lyrics…";

	public string SourceText { get; private set; } = "";

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		var lyrics = await LoadAsync();
		_synced = lyrics?.Synced ?? [];
		PlainText = lyrics?.Text ?? "No lyrics available for this song.";
		SourceText = lyrics is null ? "" : $"Lyrics: {lyrics.Source}";
		Lines = _synced.Select((l, i) => new LyricRow(i, l.Time, l.Text.Length == 0 ? "♪" : l.Text, false)).ToList();
		foreach (var property in new[] { nameof(Lines), nameof(IsSynced), nameof(IsPlain), nameof(PlainText), nameof(SourceText) })
			OnPropertyChanged(property);

		if (IsSynced)
		{
			_timer = Dispatcher.CreateTimer();
			_timer.Interval = TimeSpan.FromMilliseconds(250);
			_timer.Tick += (_, _) => FollowPlayback();
			_timer.Start();
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_timer?.Stop();
	}

	private async Task<Lyrics?> LoadAsync()
	{
		if (_track is null)
			return null;

		// Synced lyrics first; LRCLIB is a community service and may be down, so failures fall through.
		if (_track.Artist is { } artist)
		{
			try
			{
				var synced = await LrcLib.GetAsync(artist, _track.Title, _playback.Duration > TimeSpan.Zero ? _playback.Duration : null);
				if (synced is not null)
					return synced;
			}
			catch (Exception)
			{
				// Fall back to YT Music lyrics.
			}
		}

		if (_playback.LyricsBrowseId is { } lyricsId && _session.Music is { } music)
		{
			try
			{
				return await music.GetLyricsAsync(lyricsId);
			}
			catch (Exception)
			{
				// Shown as "no lyrics".
			}
		}

		return null;
	}

	private void FollowPlayback()
	{
		if (!ReferenceEquals(_playback.Current, _track))
			return;

		var position = _playback.Position;
		var index = -1;
		for (var i = 0; i < _synced.Count && _synced[i].Time <= position; i++)
			index = i;

		if (index == _currentLine)
			return;

		_currentLine = index;
		Lines = Lines.Select(l => l with { IsCurrent = l.Index == index }).ToList();
		OnPropertyChanged(nameof(Lines));
		if (index >= 0)
			LinesView.ScrollTo(index, position: ScrollToPosition.Center, animate: true);
	}

	private void OnLineTapped(object? sender, TappedEventArgs e)
	{
		if (sender is BindableObject { BindingContext: LyricRow row })
			_playback.Seek(row.Time);
	}

	private async void OnDoneClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
