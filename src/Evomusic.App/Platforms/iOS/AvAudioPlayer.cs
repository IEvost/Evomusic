using AVFoundation;
using CoreMedia;
using Evomusic.Core.Models;
using Foundation;
using MediaPlayer;
using UIKit;

namespace Evomusic.App.Services;

/// <summary>
/// AVPlayer-based output with background audio, Now Playing info (lock screen, Dynamic Island,
/// Control Center) and remote commands (lock screen buttons, AirPods, CarPlay-less headsets).
/// </summary>
public sealed class AvAudioPlayer : IAudioPlayer
{
	private static readonly HttpClient ArtworkHttp = new();

	private readonly AVPlayer _player = new() { AutomaticallyWaitsToMinimizeStalling = true };
	private readonly List<IDisposable> _itemObservers = [];
	private Track? _track;
	private MPMediaItemArtwork? _artwork;
	private bool _wantsToPlay;

	public AvAudioPlayer()
	{
		var session = AVAudioSession.SharedInstance();
		session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.AllowAirPlay | AVAudioSessionCategoryOptions.AllowBluetoothA2DP);

		_player.AddObserver("timeControlStatus", NSKeyValueObservingOptions.New, _ => RaiseStateChanged());
		_player.AddPeriodicTimeObserver(CMTime.FromSeconds(1, 1000), CoreFoundation.DispatchQueue.MainQueue, _ => UpdateNowPlaying());

		AVAudioSession.Notifications.ObserveInterruption(OnInterruption);
		RegisterRemoteCommands();
	}

	public PlayerState State { get; private set; } = PlayerState.Idle;

	public TimeSpan Position => ToTimeSpan(_player.CurrentTime);

	public TimeSpan Duration => _player.CurrentItem is { } item ? ToTimeSpan(item.Duration) : TimeSpan.Zero;

	public event EventHandler? StateChanged;

	public event EventHandler? Ended;

	public event EventHandler<string>? Failed;

	public event EventHandler? NextRequested;

	public event EventHandler? PreviousRequested;

	public void Load(Uri url, Track track)
	{
		ClearItemObservers();
		_track = track;
		_artwork = null;

		var item = new AVPlayerItem(new NSUrl(url.AbsoluteUri));
		_itemObservers.Add(item.AddObserver("status", NSKeyValueObservingOptions.New, _ => OnItemStatusChanged(item)));
		_itemObservers.Add(AVPlayerItem.Notifications.ObserveDidPlayToEndTime(item, (_, _) => Ended?.Invoke(this, EventArgs.Empty)));
		_itemObservers.Add(AVPlayerItem.Notifications.ObserveItemFailedToPlayToEndTime(item, (_, e) =>
			Fail(e.Error?.LocalizedDescription ?? "Playback stopped unexpectedly.")));

		_player.ReplaceCurrentItemWithPlayerItem(item);
		SetState(PlayerState.Loading);
		UpdateNowPlaying();
		_ = LoadArtworkAsync(track);
	}

	public void Play()
	{
		AVAudioSession.SharedInstance().SetActive(true);
		_wantsToPlay = true;
		_player.Play();
		RaiseStateChanged();
	}

	public void Pause()
	{
		_wantsToPlay = false;
		_player.Pause();
		RaiseStateChanged();
	}

	public void Seek(TimeSpan position)
	{
		_player.Seek(CMTime.FromSeconds(position.TotalSeconds, 1000), _ => UpdateNowPlaying());
	}

	public void Stop()
	{
		Pause();
		ClearItemObservers();
		_player.ReplaceCurrentItemWithPlayerItem(null);
		_track = null;
		MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = new MPNowPlayingInfo();
		SetState(PlayerState.Idle);
	}

	private void OnItemStatusChanged(AVPlayerItem item)
	{
		if (item.Status == AVPlayerItemStatus.Failed)
			Fail(item.Error?.LocalizedDescription ?? "The stream could not be opened.");
		else
			RaiseStateChanged();
	}

	private void Fail(string message)
	{
		_wantsToPlay = false;
		SetState(PlayerState.Failed);
		Failed?.Invoke(this, message);
	}

	private void RaiseStateChanged()
	{
		if (State == PlayerState.Failed)
			return;

		var state = _player.TimeControlStatus switch
		{
			AVPlayerTimeControlStatus.Playing => PlayerState.Playing,
			AVPlayerTimeControlStatus.WaitingToPlayAtSpecifiedRate => PlayerState.Loading,
			_ when _player.CurrentItem is null => PlayerState.Idle,
			_ => _wantsToPlay ? PlayerState.Loading : PlayerState.Paused,
		};
		SetState(state);
	}

	private void SetState(PlayerState state)
	{
		var changed = State != state;
		State = state;
		if (!changed)
			return;

		UpdateNowPlaying();
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnInterruption(object? sender, AVAudioSessionInterruptionEventArgs e)
	{
		// Phone calls, Siri, alarms: the system pauses us; resume only when iOS says so.
		if (e.InterruptionType == AVAudioSessionInterruptionType.Began)
		{
			_player.Pause();
			RaiseStateChanged();
		}
		else if (e.Option == AVAudioSessionInterruptionOptions.ShouldResume && _wantsToPlay)
		{
			Play();
		}
	}

	private void RegisterRemoteCommands()
	{
		var commands = MPRemoteCommandCenter.Shared;
		commands.PlayCommand.AddTarget(_ => Handle(Play));
		commands.PauseCommand.AddTarget(_ => Handle(Pause));
		commands.TogglePlayPauseCommand.AddTarget(_ => Handle(() =>
		{
			if (_wantsToPlay)
				Pause();
			else
				Play();
		}));
		commands.NextTrackCommand.AddTarget(_ => Handle(() => NextRequested?.Invoke(this, EventArgs.Empty)));
		commands.PreviousTrackCommand.AddTarget(_ => Handle(() => PreviousRequested?.Invoke(this, EventArgs.Empty)));
		commands.ChangePlaybackPositionCommand.AddTarget(e =>
		{
			if (e is MPChangePlaybackPositionCommandEvent position)
				Seek(TimeSpan.FromSeconds(position.PositionTime));
			return MPRemoteCommandHandlerStatus.Success;
		});

		static MPRemoteCommandHandlerStatus Handle(Action action)
		{
			action();
			return MPRemoteCommandHandlerStatus.Success;
		}
	}

	private void UpdateNowPlaying()
	{
		if (_track is null)
			return;

		var info = new MPNowPlayingInfo
		{
			Title = _track.Title,
			Artist = _track.Artist,
			ElapsedPlaybackTime = Position.TotalSeconds,
			PlaybackRate = State == PlayerState.Playing ? 1 : 0,
			Artwork = _artwork,
		};
		if (Duration > TimeSpan.Zero)
			info.PlaybackDuration = Duration.TotalSeconds;

		MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = info;
	}

	private async Task LoadArtworkAsync(Track track)
	{
		if (track.ArtworkUrl(544) is not { } url)
			return;

		try
		{
			var bytes = await ArtworkHttp.GetByteArrayAsync(url);
			var image = UIImage.LoadFromData(NSData.FromArray(bytes));
			if (image is null || !ReferenceEquals(track, _track))
				return;

			_artwork = new MPMediaItemArtwork(image.Size, _ => image);
			MainThread.BeginInvokeOnMainThread(UpdateNowPlaying);
		}
		catch (HttpRequestException)
		{
			// Artwork is cosmetic; keep playing without it.
		}
	}

	private void ClearItemObservers()
	{
		foreach (var observer in _itemObservers)
			observer.Dispose();
		_itemObservers.Clear();
	}

	private static TimeSpan ToTimeSpan(CMTime time) =>
		time.IsInvalid || time.IsIndefinite || double.IsNaN(time.Seconds) ? TimeSpan.Zero : TimeSpan.FromSeconds(time.Seconds);
}
