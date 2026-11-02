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

	private readonly IAudioPlayer _player;
	private readonly IStreamResolver _resolver;
	private readonly ILogger<PlaybackService> _logger;
	private CancellationTokenSource? _loadCts;
	private bool _resolving;

	public PlaybackService(IAudioPlayer player, IStreamResolver resolver, ILogger<PlaybackService> logger)
	{
		_player = player;
		_resolver = resolver;
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

	/// <summary>Raised when the queue's order or contents change (shuffle, new queue).</summary>
	public event EventHandler? QueueChanged;

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
			_player.Load(stream.Url, track);
			_player.Play();
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
