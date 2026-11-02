using System.ComponentModel;
using System.Runtime.CompilerServices;
using Evomusic.Core.Models;
using Evomusic.Core.Playback;
using Evomusic.Core.Streams;
using Microsoft.Extensions.Logging;

namespace Evomusic.App.Services;

/// <summary>App-wide playback state: drives the queue, resolves streams and feeds the native player.</summary>
public sealed class PlaybackService : INotifyPropertyChanged
{
	private static readonly TimeSpan RestartThreshold = TimeSpan.FromSeconds(3);

	public const string ReportPlaysPreference = "report_plays";

	private readonly IAudioPlayer _player;
	private readonly IStreamResolver _resolver;
	private readonly SessionService _session;
	private readonly ILogger<PlaybackService> _logger;
	private CancellationTokenSource? _loadCts;
	private bool _resolving;
	private bool _radioLoading;

	public PlaybackService(IAudioPlayer player, IStreamResolver resolver, SessionService session, ILogger<PlaybackService> logger)
	{
		_player = player;
		_resolver = resolver;
		_session = session;
		_logger = logger;

		_player.StateChanged += (_, _) => OnMain(NotifyState);
		_player.Ended += (_, _) => OnMain(async () => await AdvanceAsync(afterEnd: true));
		_player.Failed += (_, message) => OnMain(() => SetError($"Playback failed: {message}"));
		_player.NextRequested += (_, _) => OnMain(async () => await NextAsync());
		_player.PreviousRequested += (_, _) => OnMain(async () => await PreviousAsync());
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public PlaybackQueue Queue { get; } = new();

	public Track? Current => Queue.Current;

	public bool HasTrack => Current is not null;

	public bool IsPlaying => _player.State == PlayerState.Playing;

	public bool IsBusy => _resolving || _player.State == PlayerState.Loading;

	public string? Error { get; private set; }

	public bool HasError => Error is not null;

	public TimeSpan Position => _player.Position;

	public TimeSpan Duration => _player.Duration;

	public bool IsShuffled => Queue.IsShuffled;

	public RepeatMode Repeat => Queue.Repeat;

	/// <summary>Rating of the current track on the account (from the <c>next</c> endpoint).</summary>
	public LikeStatus LikeStatus { get; private set; }

	/// <summary>YT Music lyrics page of the current track, if it has one.</summary>
	public string? LyricsBrowseId { get; private set; }

	/// <summary>Raised when the queue's order or contents change (shuffle, new queue).</summary>
	public event EventHandler? QueueChanged;

	public void PlayNext(Track track)
	{
		var wasEmpty = Current is null;
		Queue.InsertNext([track]);
		QueueChanged?.Invoke(this, EventArgs.Empty);
		if (wasEmpty)
			_ = LoadCurrentAsync();
	}

	public void AddToQueue(Track track)
	{
		var wasEmpty = Current is null;
		Queue.Append([track]);
		QueueChanged?.Invoke(this, EventArgs.Empty);
		if (wasEmpty)
			_ = LoadCurrentAsync();
	}

	/// <summary>Plays the track followed by its YT Music radio (same mix the web player builds).</summary>
	public async Task StartRadioAsync(Track track)
	{
		await PlayAsync([track], 0);
		await AppendRadioAsync(track);
	}

	public async Task ToggleLikeAsync()
	{
		if (Current is not { } track || _session.Music is not { } music)
			return;

		var target = LikeStatus == LikeStatus.Like ? LikeStatus.Indifferent : LikeStatus.Like;
		var previous = LikeStatus;
		SetLikeStatus(target);
		try
		{
			await music.RateAsync(track.VideoId, target);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Rating {VideoId} failed", track.VideoId);
			SetLikeStatus(previous);
			SetError($"Could not update like: {ex.Message}");
		}
	}

	public async Task PlayAsync(IReadOnlyList<Track> tracks, int startIndex, bool shuffle = false)
	{
		Queue.SetShuffle(shuffle);
		Queue.Set(tracks, startIndex);
		NotifyModes();
		await LoadCurrentAsync();
	}

	public async Task PlayQueueIndexAsync(int index)
	{
		if (Queue.MoveTo(index))
			await LoadCurrentAsync();
	}

	public void ToggleShuffle()
	{
		Queue.SetShuffle(!Queue.IsShuffled);
		NotifyModes();
	}

	public void CycleRepeat()
	{
		Queue.Repeat = Queue.Repeat switch
		{
			RepeatMode.Off => RepeatMode.All,
			RepeatMode.All => RepeatMode.One,
			_ => RepeatMode.Off,
		};
		NotifyModes();
	}

	public void Seek(TimeSpan position) => _player.Seek(position);

	/// <summary>Plays the radio of whatever is playing now, replacing the rest of the queue.</summary>
	public async Task StartRadioFromCurrentAsync()
	{
		if (Current is not { } track)
			return;

		Queue.Set([track], 0);
		QueueChanged?.Invoke(this, EventArgs.Empty);
		await AppendRadioAsync(track);
	}

	public void TogglePlayPause()
	{
		if (_player.State == PlayerState.Playing)
			_player.Pause();
		else if (_player.State is PlayerState.Paused)
			_player.Play();
		else if (_player.State is PlayerState.Failed or PlayerState.Idle && Current is not null)
			_ = LoadCurrentAsync();
	}

	public Task NextAsync() => AdvanceAsync(afterEnd: false);

	public async Task PreviousAsync()
	{
		if (_player.Position > RestartThreshold || !Queue.MovePrevious())
		{
			_player.Seek(TimeSpan.Zero);
			return;
		}

		await LoadCurrentAsync();
	}

	private async Task AdvanceAsync(bool afterEnd)
	{
		var moved = afterEnd ? Queue.MoveNextAfterEnd() : Queue.MoveNext();
		if (!moved)
		{
			if (afterEnd)
				_player.Pause();
			return;
		}

		await LoadCurrentAsync();
	}

	private async Task LoadCurrentAsync()
	{
		if (Current is not { } track)
			return;

		_loadCts?.Cancel();
		var cts = _loadCts = new CancellationTokenSource();

		SetError(null);
		_resolving = true;
		NotifyState();
		try
		{
			var stream = await _resolver.ResolveAsync(track.VideoId, cts.Token);
			if (cts.IsCancellationRequested)
				return;

			_logger.LogInformation("Playing {VideoId}: {Container}/{Codec} {Bitrate} bps", track.VideoId, stream.Container, stream.Codec, stream.BitsPerSecond);
			_player.Load(stream.Url, track, stream.Duration);
			_player.Play();
			_ = OnTrackStartedAsync(track, cts.Token);
		}
		catch (OperationCanceledException) when (cts.IsCancellationRequested)
		{
			// A newer track was requested.
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not resolve {VideoId}", track.VideoId);
			SetError($"Could not load \"{track.Title}\": {ex.Message}");
		}
		finally
		{
			if (ReferenceEquals(cts, _loadCts))
			{
				_resolving = false;
				NotifyState();
			}
		}
	}

	/// <summary>
	/// Side work for a started track, all best-effort: rating and lyrics link, history reporting,
	/// and topping up the queue with radio when it is about to run out (like YT Music autoplay).
	/// </summary>
	private async Task OnTrackStartedAsync(Track track, CancellationToken cancellationToken)
	{
		SetLikeStatus(LikeStatus.Indifferent);
		LyricsBrowseId = null;
		Notify(nameof(LyricsBrowseId));
		if (_session.Music is not { } music)
			return;

		try
		{
			var info = await music.GetWatchInfoAsync(track.VideoId, cancellationToken);
			if (cancellationToken.IsCancellationRequested)
				return;

			SetLikeStatus(info.LikeStatus);
			LyricsBrowseId = info.LyricsBrowseId;
			Notify(nameof(LyricsBrowseId));

			var isLast = Queue.CurrentIndex == Queue.Tracks.Count - 1;
			if (isLast && Queue.Repeat == RepeatMode.Off)
				AppendRadio(track, info.UpNext);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Watch info for {VideoId} failed", track.VideoId);
		}

		if (Preferences.Default.Get(ReportPlaysPreference, true))
		{
			try
			{
				await music.ReportPlaybackAsync(track.VideoId, cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogWarning(ex, "Reporting playback of {VideoId} failed", track.VideoId);
			}
		}
	}

	private async Task AppendRadioAsync(Track seed)
	{
		if (_radioLoading || _session.Music is not { } music)
			return;

		_radioLoading = true;
		try
		{
			AppendRadio(seed, (await music.GetWatchInfoAsync(seed.VideoId)).UpNext);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Radio for {VideoId} failed", seed.VideoId);
		}
		finally
		{
			_radioLoading = false;
		}
	}

	private void AppendRadio(Track seed, IEnumerable<MediaItem> upNext)
	{
		var queued = Queue.Tracks.Select(t => t.VideoId).ToHashSet();
		var radio = upNext
			.Select(Track.FromMediaItem)
			.OfType<Track>()
			.Where(t => t.VideoId != seed.VideoId && queued.Add(t.VideoId))
			.ToList();
		if (radio.Count == 0)
			return;

		Queue.Append(radio);
		QueueChanged?.Invoke(this, EventArgs.Empty);
	}

	private void SetLikeStatus(LikeStatus status)
	{
		LikeStatus = status;
		Notify(nameof(LikeStatus));
	}

	private void SetError(string? error)
	{
		Error = error;
		Notify(nameof(Error));
		Notify(nameof(HasError));
	}

	private void NotifyModes()
	{
		Notify(nameof(IsShuffled));
		Notify(nameof(Repeat));
		QueueChanged?.Invoke(this, EventArgs.Empty);
	}

	private void NotifyState()
	{
		Notify(nameof(Current));
		Notify(nameof(HasTrack));
		Notify(nameof(IsPlaying));
		Notify(nameof(IsBusy));
	}

	private void Notify([CallerMemberName] string? property = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

	private static void OnMain(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
