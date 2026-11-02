using System.ComponentModel;
using Evomusic.App.Services;

namespace Evomusic.App.Controls;

/// <summary>Compact player bar: artwork, title, play/pause and next. Phase 3 turns it into the tab-bar accessory.</summary>
public partial class MiniPlayerView : ContentView
{
	private PlaybackService? _playback;

	public MiniPlayerView()
	{
		InitializeComponent();
	}

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();
		if (_playback is not null || Handler?.MauiContext?.Services.GetService<PlaybackService>() is not { } playback)
			return;

		_playback = playback;
		_playback.PropertyChanged += OnPlaybackChanged;
		BindingContext = _playback;
		UpdateButtons();
	}

	private void OnPlaybackChanged(object? sender, PropertyChangedEventArgs e) => UpdateButtons();

	private void UpdateButtons()
	{
		if (_playback is null)
			return;

		PlayPauseButton.Text = _playback.IsPlaying ? "⏸︎" : "▶︎";
		PlayPauseButton.IsVisible = !_playback.IsBusy;
	}

	private void OnPlayPauseClicked(object? sender, EventArgs e) => _playback?.TogglePlayPause();

	private async void OnNextClicked(object? sender, EventArgs e)
	{
		if (_playback is not null)
			await _playback.NextAsync();
	}
}
