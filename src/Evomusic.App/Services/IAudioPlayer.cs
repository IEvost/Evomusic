using Evomusic.Core.Models;

namespace Evomusic.App.Services;

public enum PlayerState
{
	Idle,
	Loading,
	Playing,
	Paused,
	Failed,
}

/// <summary>Native audio output plus the system media integration (lock screen, Control Center, headset buttons).</summary>
public interface IAudioPlayer
{
	PlayerState State { get; }

	/// <summary>The user's intent (play pressed and not paused), independent of buffering.</summary>
	bool IsPlayRequested { get; }

	TimeSpan Position { get; }

	TimeSpan Duration { get; }

	/// <summary>Raised on any state change; may be raised off the main thread.</summary>
	event EventHandler? StateChanged;

	event EventHandler? Ended;

	event EventHandler<string>? Failed;

	/// <summary>System "next track" command (lock screen, headset).</summary>
	event EventHandler? NextRequested;

	event EventHandler? PreviousRequested;

	/// <summary>Loads a stream. <paramref name="knownDuration"/> overrides the container's (unreliable) duration.</summary>
	void Load(Uri url, Track track, TimeSpan? knownDuration);

	void Play();

	void Pause();

	void Seek(TimeSpan position);

	void Stop();
}
