using Evomusic.Core.Models;

namespace Evomusic.Core.Playback;

public enum RepeatMode
{
    Off,
    All,
    One,
}

/// <summary>Platform-independent play queue: the tracks, the current position, shuffle and the repeat rule.</summary>
public sealed class PlaybackQueue(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly List<Track> _tracks = [];
    private List<Track> _originalOrder = [];

    public IReadOnlyList<Track> Tracks => _tracks;

    public int CurrentIndex { get; private set; } = -1;

    public Track? Current => CurrentIndex >= 0 && CurrentIndex < _tracks.Count ? _tracks[CurrentIndex] : null;

    public RepeatMode Repeat { get; set; } = RepeatMode.Off;

    public bool IsShuffled { get; private set; }

    public void Set(IEnumerable<Track> tracks, int startIndex)
    {
        _tracks.Clear();
        _tracks.AddRange(tracks);
        _originalOrder = [.. _tracks];
        CurrentIndex = _tracks.Count == 0 ? -1 : Math.Clamp(startIndex, 0, _tracks.Count - 1);
        if (IsShuffled)
            ShuffleKeepingCurrent();
    }

    /// <summary>Shuffle keeps the current track playing and randomizes everything after it; turning it off restores the original order.</summary>
    public void SetShuffle(bool shuffled)
    {
        if (shuffled == IsShuffled)
            return;

        IsShuffled = shuffled;
        if (shuffled)
        {
            ShuffleKeepingCurrent();
            return;
        }

        var current = Current;
        _tracks.Clear();
        _tracks.AddRange(_originalOrder);
        CurrentIndex = current is null ? -1 : _tracks.IndexOf(current);
    }

    /// <summary>"Play next": inserts right after the current track.</summary>
    public void InsertNext(IEnumerable<Track> tracks)
    {
        var list = tracks.ToList();
        if (Current is null)
        {
            Set(list, 0);
            return;
        }

        _tracks.InsertRange(CurrentIndex + 1, list);
        _originalOrder.InsertRange(_originalOrder.IndexOf(Current) + 1, list);
    }

    /// <summary>"Add to queue" / radio continuation: appends at the end.</summary>
    public void Append(IEnumerable<Track> tracks)
    {
        var list = tracks.ToList();
        if (Current is null)
        {
            Set(list, 0);
            return;
        }

        _tracks.AddRange(list);
        _originalOrder.AddRange(list);
    }

    public bool MoveTo(int index)
    {
        if (index < 0 || index >= _tracks.Count)
            return false;

        CurrentIndex = index;
        return true;
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

    private void ShuffleKeepingCurrent()
    {
        if (Current is not { } current)
            return;

        var rest = _tracks.Where(t => !ReferenceEquals(t, current)).ToArray();
        _random.Shuffle(rest);
        _tracks.Clear();
        _tracks.Add(current);
        _tracks.AddRange(rest);
        CurrentIndex = 0;
    }
}
