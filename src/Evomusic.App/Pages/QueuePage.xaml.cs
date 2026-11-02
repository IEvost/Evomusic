using Evomusic.App.Services;
using Evomusic.Core.Models;

namespace Evomusic.App.Pages;

/// <summary>One queue row; already-played tracks are dimmed and the current one is bold.</summary>
public sealed record QueueEntry(int Index, Track Track, bool IsCurrent, bool IsPlayed)
{
	public double Opacity => IsPlayed ? 0.45 : 1;

	public FontAttributes TitleAttributes => IsCurrent ? FontAttributes.Bold : FontAttributes.None;
}

public partial class QueuePage : ContentPage
{
	private readonly PlaybackService _playback;

	public QueuePage(PlaybackService playback)
	{
		InitializeComponent();
		_playback = playback;
		BindingContext = this;
	}

	public List<QueueEntry> Entries { get; private set; } = [];

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_playback.PropertyChanged += OnPlaybackChanged;
		_playback.QueueChanged += OnQueueChanged;
		Reload();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_playback.PropertyChanged -= OnPlaybackChanged;
		_playback.QueueChanged -= OnQueueChanged;
	}

	private void OnPlaybackChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(PlaybackService.Current))
			Reload();
	}

	private void OnQueueChanged(object? sender, EventArgs e) => Reload();

	private void Reload()
	{
		var current = _playback.Queue.CurrentIndex;
		Entries = _playback.Queue.Tracks
			.Select((track, index) => new QueueEntry(index, track, index == current, index < current))
			.ToList();
		OnPropertyChanged(nameof(Entries));

		if (current >= 0)
			QueueList.ScrollTo(current, position: ScrollToPosition.Start, animate: false);
	}

	private async void OnEntryTapped(object? sender, TappedEventArgs e)
	{
		if (sender is BindableObject { BindingContext: QueueEntry entry })
			await _playback.PlayQueueIndexAsync(entry.Index);
	}

	private async void OnDoneClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
