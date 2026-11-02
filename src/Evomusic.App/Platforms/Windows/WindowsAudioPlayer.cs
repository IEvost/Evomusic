using Evomusic.Core.Models;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Evomusic.App.Services;

// Development preview only: plays audio on the PC; no system media integration.
public sealed class WindowsAudioPlayer : IAudioPlayer
{
	private readonly MediaPlayer _player = new();

	public WindowsAudioPlayer()
	{
		_player.MediaEnded += (_, _) => Ended?.Invoke(this, EventArgs.Empty);
		_player.MediaFailed += (_, e) =>
		{
			State = PlayerState.Failed;
			StateChanged?.Invoke(this, EventArgs.Empty);
			Failed?.Invoke(this, e.ErrorMessage);
		};
		_player.PlaybackSession.PlaybackStateChanged += (session, _) =>
		{
			State = session.PlaybackState switch
			{
				MediaPlaybackState.Playing => PlayerState.Playing,
				MediaPlaybackState.Paused => PlayerState.Paused,
				MediaPlaybackState.None => PlayerState.Idle,
				_ => PlayerState.Loading,
			};
			StateChanged?.Invoke(this, EventArgs.Empty);
		};
	}

	public PlayerState State { get; private set; } = PlayerState.Idle;

	public TimeSpan Position => _player.PlaybackSession.Position;

	public TimeSpan Duration => _player.PlaybackSession.NaturalDuration;

	public event EventHandler? StateChanged;

	public event EventHandler? Ended;

	public event EventHandler<string>? Failed;

	public event EventHandler? NextRequested
	{
		add { }
		remove { }
	}

	public event EventHandler? PreviousRequested
	{
		add { }
		remove { }
	}

	public void Load(Uri url, Track track, TimeSpan? knownDuration)
	{
		_player.Source = MediaSource.CreateFromUri(url);
		State = PlayerState.Loading;
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	public void Play() => _player.Play();

	public void Pause() => _player.Pause();

	public void Seek(TimeSpan position) => _player.PlaybackSession.Position = position;

	public void Stop()
	{
		_player.Pause();
		_player.Source = null;
	}
}
