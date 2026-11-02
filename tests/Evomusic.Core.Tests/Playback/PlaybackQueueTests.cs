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
    public void Track_ArtworkUrl_RequestsLargerSize()
    {
        var track = new Track("v", "T", "A", "https://lh3.googleusercontent.com/abc=w120-h120-l90-rj");

        Assert.Equal("https://lh3.googleusercontent.com/abc=w544-h544-l90-rj", track.ArtworkUrl(544));
    }

    [Fact]
    public void Track_FromShelfItem_TakesArtistFromSubtitle()
    {
        var track = Track.FromShelfItem(new ShelfItem("Группа крови", "Кино • 121M plays", null, "vid", null));

        Assert.Equal(("vid", "Кино"), (track?.VideoId, track?.Artist));
        Assert.Null(Track.FromShelfItem(new ShelfItem("Mix", "Playlist", null, null, "VL1")));
    }
}
