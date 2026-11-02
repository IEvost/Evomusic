using Evomusic.Core.Models;
using Evomusic.Core.Playback;

namespace Evomusic.Core.Tests.Playback;

public class PlaybackQueueTests
{
    private static readonly Track[] Tracks = [new("a", "A", null, null), new("b", "B", null, null), new("c", "C", null, null)];

    [Fact]
    public void Set_StartsAtRequestedIndex()
    {
        var queue = new PlaybackQueue();
        queue.Set(Tracks, 1);

        Assert.Equal("b", queue.Current?.VideoId);
    }

    [Fact]
    public void MoveNextAfterEnd_StopsAtEndWithoutRepeat()
    {
        var queue = new PlaybackQueue();
        queue.Set(Tracks, 2);

        Assert.False(queue.MoveNextAfterEnd());
        Assert.Equal("c", queue.Current?.VideoId);
    }

    [Fact]
    public void RepeatAll_WrapsBothWays()
    {
        var queue = new PlaybackQueue { Repeat = RepeatMode.All };
        queue.Set(Tracks, 2);

        Assert.True(queue.MoveNext());
        Assert.Equal("a", queue.Current?.VideoId);
        Assert.True(queue.MovePrevious());
        Assert.Equal("c", queue.Current?.VideoId);
    }

    [Fact]
    public void RepeatOne_StaysOnTrackAfterEndButNextStillAdvances()
    {
        var queue = new PlaybackQueue { Repeat = RepeatMode.One };
        queue.Set(Tracks, 0);

        Assert.True(queue.MoveNextAfterEnd());
        Assert.Equal("a", queue.Current?.VideoId);
        Assert.True(queue.MoveNext());
        Assert.Equal("b", queue.Current?.VideoId);
    }

    [Fact]
    public void Shuffle_KeepsCurrentFirstAndRestoresOriginalOrder()
    {
        var many = Enumerable.Range(0, 20).Select(i => new Track($"v{i}", $"T{i}", null, null)).ToList();
        var queue = new PlaybackQueue(new Random(1));
        queue.Set(many, 5);

        queue.SetShuffle(true);

        Assert.Equal("v5", queue.Current?.VideoId);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Equal(many.Select(t => t.VideoId).Order(), queue.Tracks.Select(t => t.VideoId).Order());
        Assert.NotEqual(many.Select(t => t.VideoId), queue.Tracks.Select(t => t.VideoId));

        queue.MoveNext();
        var playing = queue.Current;
        queue.SetShuffle(false);

        Assert.Equal(many, queue.Tracks);
        Assert.Same(playing, queue.Current);
    }

    [Fact]
    public void Track_ArtworkUrl_RequestsLargerSize()
    {
        var track = new Track("v", "T", "A", "https://lh3.googleusercontent.com/abc=w120-h120-l90-rj");

        Assert.Equal("https://lh3.googleusercontent.com/abc=w544-h544-l90-rj", track.ArtworkUrl(544));
    }

    [Fact]
    public void Track_FromMediaItem_PrefersLinkedArtistThenSubtitle()
    {
        var linked = Track.FromMediaItem(new MediaItem(MediaKind.Song, "Группа крови", "Song • Кино • 4:45", null, "vid", null, Artist: "Кино"));
        var unlinked = Track.FromMediaItem(new MediaItem(MediaKind.Song, "Группа крови", "Кино • 121M plays", null, "vid", null));

        Assert.Equal("Кино", linked?.Artist);
        Assert.Equal("Кино", unlinked?.Artist);
        Assert.Null(Track.FromMediaItem(new MediaItem(MediaKind.Playlist, "Mix", "Playlist", null, null, "VL1")));
    }
}
