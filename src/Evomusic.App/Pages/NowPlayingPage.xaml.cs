using System.ComponentModel;
using Evomusic.App.Controls;
using Evomusic.App.Services;
using Evomusic.Core.Models;
using Evomusic.Core.Playback;

namespace Evomusic.App.Pages;

/// <summary>Full-screen player sheet: artwork, scrubber, transport, shuffle/repeat and the queue.</summary>
public partial class NowPlayingPage : ContentPage
{
	private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(500);

	private readonly PlaybackService _playback;
	private IDispatcherTimer? _timer;
	private bool _dragging;

	public NowPlayingPage(PlaybackService playback)
	{
		InitializeComponent();
		_playback = playback;
		BindingContext = this;
	}

	public string? TrackTitle => _playback.Current?.Title;

	public string? Artist => _playback.Current?.Artist;

	public string? ArtworkUrl => _playback.Current?.ArtworkUrl(1080) ?? _playback.Current?.ThumbnailUrl;

	public string? Error => _playback.Error;

	public bool HasError => _playback.HasError;

	public bool IsLoadingTrack => _playback.IsBusy;

	public double DurationSeconds => Math.Max(_playback.Duration.TotalSeconds, 1);

	public double PositionSeconds => _playback.Position.TotalSeconds;

	public string PositionText => Format(_playback.Position);

	public string RemainingText => "-" + Format(_playback.Duration - _playback.Position);

	public ImageSource PlayPauseSymbol => Symbol(_playback.IsPlaying ? "pause.fill" : "play.fill", 44, primary: true);

	public ImageSource ShuffleSymbol => Symbol("shuffle", 20, primary: _playback.IsShuffled);

	public ImageSource LikeSymbol => _playback.LikeStatus == LikeStatus.Like
		? new SymbolImageSource { Name = "hand.thumbsup.fill", PointSize = 22, Color = Color.FromArgb("#ac99ea") }
		: Symbol("hand.thumbsup", 22, primary: false);

	public ImageSource RepeatSymbol => Symbol(_playback.Repeat == RepeatMode.One ? "repeat.1" : "repeat", 20, primary: _playback.Repeat != RepeatMode.Off);

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_playback.PropertyChanged += OnPlaybackChanged;
		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TickInterval;
		_timer.Tick += (_, _) => UpdateProgress();
		_timer.Start();
		Refresh();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_playback.PropertyChanged -= OnPlaybackChanged;
		_timer?.Stop();
	}

	private void OnPlaybackChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

	private void Refresh()
	{
		foreach (var property in new[]
		{
			nameof(TrackTitle), nameof(Artist), nameof(ArtworkUrl), nameof(Error), nameof(HasError), nameof(IsLoadingTrack),
			nameof(PlayPauseSymbol), nameof(ShuffleSymbol), nameof(RepeatSymbol), nameof(LikeSymbol),
		})
		{
			OnPropertyChanged(property);
		}

		UpdateProgress();
	}

	private void UpdateProgress()
	{
		if (_dragging)
			return;

		OnPropertyChanged(nameof(DurationSeconds));
		OnPropertyChanged(nameof(PositionSeconds));
		OnPropertyChanged(nameof(PositionText));
		OnPropertyChanged(nameof(RemainingText));
	}

	private void OnDragStarted(object? sender, EventArgs e) => _dragging = true;

	private void OnDragCompleted(object? sender, EventArgs e)
	{
		_dragging = false;
		_playback.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
	}

	private void OnPlayPauseClicked(object? sender, EventArgs e) => _playback.TogglePlayPause();

	private async void OnPreviousClicked(object? sender, EventArgs e) => await _playback.PreviousAsync();

	private async void OnNextClicked(object? sender, EventArgs e) => await _playback.NextAsync();

	private void OnShuffleClicked(object? sender, EventArgs e) => _playback.ToggleShuffle();

	private void OnRepeatClicked(object? sender, EventArgs e) => _playback.CycleRepeat();

	private async void OnLikeClicked(object? sender, EventArgs e) => await _playback.ToggleLikeAsync();

	private async void OnLyricsClicked(object? sender, EventArgs e) => await Navigator.ShowLyricsAsync();

	private async void OnMoreClicked(object? sender, EventArgs e)
	{
		if (_playback.Current is not { } track)
			return;

		const string addToPlaylist = "Add to Playlist…";
		const string startRadio = "Start Radio";
		var choice = await DisplayActionSheetAsync(track.Title, "Cancel", null, addToPlaylist, startRadio);
		if (choice == addToPlaylist)
			await Navigator.ShowAddToPlaylistAsync(track);
		else if (choice == startRadio)
			await _playback.StartRadioFromCurrentAsync();
	}

	private async void OnQueueClicked(object? sender, EventArgs e) =>
		await Navigation.PushModalAsync(new QueuePage(_playback));

	private async void OnCloseClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

	private static string Format(TimeSpan time) =>
		time < TimeSpan.Zero ? "0:00" : time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

	private static SymbolImageSource Symbol(string name, double size, bool primary)
	{
		var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
		var color = primary
			? (dark ? Colors.White : Colors.Black)
			: Color.FromArgb("#919191");
		return new SymbolImageSource { Name = name, PointSize = size, Color = color };
	}
}
