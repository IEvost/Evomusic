namespace Evomusic.Core.Models;

public enum LikeStatus
{
    Indifferent,
    Like,
    Dislike,
}

/// <summary>What the <c>next</c> endpoint knows about a track: the up-next/radio queue, lyrics page and rating.</summary>
public sealed record WatchInfo(IReadOnlyList<MediaItem> UpNext, string? LyricsBrowseId, LikeStatus LikeStatus);

public sealed record LyricLine(TimeSpan Time, string Text);

/// <summary>Lyrics for one track; <see cref="Synced"/> is non-empty when time-coded lines are available.</summary>
public sealed record Lyrics(string Text, IReadOnlyList<LyricLine> Synced, string Source)
{
    public bool IsSynced => Synced.Count > 0;
}
