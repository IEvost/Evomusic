using Evomusic.Core.Models;

namespace Evomusic.Core.Playback;

public enum RepeatMode
{
    Off,
    All,
    One,
}

/// <summary>Platform-independent play queue: the tracks, the current position and the repeat rule.</summary>
public sealed class PlaybackQueue
{
    private readonly List<Track> _tracks = [];

    public IReadOnlyList<Track> Tracks => _tracks;

    public int CurrentIndex { get; private set; } = -1;

    public Track? Current => CurrentIndex >= 0 && CurrentIndex < _tracks.Count ? _tracks[CurrentIndex] : null;

    public RepeatMode Repeat { get; set; } = RepeatMode.Off;

    public void Set(IEnumerable<Track> tracks, int startIndex)
    {
        _tracks.Clear();
        _tracks.AddRange(tracks);
        CurrentIndex = _tracks.Count == 0 ? -1 : Math.Clamp(startIndex, 0, _tracks.Count - 1);
    }

    /// <summary>Advances after a track ends (honours repeat). Returns false when playback should stop.</summary>
    public bool MoveNextAfterEnd()
    {
        if (Current is null)
            return false;

        if (Repeat == RepeatMode.One)
            return true;

        return MoveNext();
    }

    /// <summary>User pressed "next": always leaves the current track, wrapping only with repeat-all.</summary>
    public bool MoveNext()
    {
        if (CurrentIndex + 1 < _tracks.Count)
        {
            CurrentIndex++;
            return true;
        }

        if (Repeat != RepeatMode.Off && _tracks.Count > 0)
        {
            CurrentIndex = 0;
            return true;
        }

        return false;
    }

    public bool MovePrevious()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
            return true;
        }

        if (Repeat != RepeatMode.Off && _tracks.Count > 0)
        {
            CurrentIndex = _tracks.Count - 1;
            return true;
        }

        return false;
    }
}
