using System.Text.Json.Nodes;
using Evomusic.Core.InnerTube.Parsers;
using Evomusic.Core.Models;

namespace Evomusic.Core.Tests.InnerTube;

// Fixtures are real anonymous WEB_REMIX responses captured on 2026-11-02.
// When YouTube changes a layout, re-capture the fixture and fix the parser until these pass.
public class ParserTests
{
    private static JsonNode Fixture(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json")))!;

    [Fact]
    public void AccountParser_ReadsActiveAccountHeader()
    {
        var json = JsonNode.Parse("""
            {"actions":[{"openPopupAction":{"popup":{"multiPageMenuRenderer":{"header":{"activeAccountHeaderRenderer":{
              "accountName":{"runs":[{"text":"Evo"}]},
              "channelHandle":{"runs":[{"text":"@evo"}]},
              "accountPhoto":{"thumbnails":[{"url":"https://small"},{"url":"https://large"}]}
            }}}}}}]}
            """)!;

        var account = AccountParser.Parse(json);

        Assert.NotNull(account);
        Assert.Equal(("Evo", "@evo", "https://large"), (account.Name, account.Handle, account.PhotoUrl));
    }

    [Fact]
    public void Search_HasTopResultAndMergedResultRows()
    {
        var page = PageParser.Parse(Fixture("search"));

        Assert.Null(page.Header);
        var top = page.Sections[0];
        Assert.Equal("Top result", top.Title);
        Assert.Equal(new MediaItem(MediaKind.Song, "Группа крови", "Song • Кино • 4:45", top.Items[0].ThumbnailUrl, "xtxjm7ciwmc", null, "Кино"), top.Items[0]);

        var results = page.Sections[1];
        Assert.True(results.Items.Count >= 20);
        Assert.Contains(results.Items, i => i is { Kind: MediaKind.Album, BrowseId: "MPREb_OLmD8O5IYNS" });
        Assert.Contains(results.Items, i => i is { Kind: MediaKind.Artist, BrowseId: "UCL9NQ06h7I0CRUcGxPWMtkQ" });
    }

    [Fact]
    public void Album_InheritsArtworkAndArtistFromHeader()
    {
        var page = PageParser.Parse(Fixture("album"));

        Assert.Equal(("Группа крови", "Кино", "Album • 1988"), (page.Header?.Title, page.Header?.Subtitle, page.Header?.SecondSubtitle));
        var tracks = page.Sections[0];
        Assert.Equal(SectionLayout.List, tracks.Layout);
        Assert.Equal(11, tracks.Items.Count);
        Assert.All(tracks.Items, t => Assert.True(t.IsPlayable && t.Artist == "Кино" && t.ThumbnailUrl is not null && t.Duration is not null));
        Assert.Contains(page.Sections, s => s is { Layout: SectionLayout.Carousel, Title: "Releases for you" });
    }

    [Fact]
    public void Playlist_HasTracksAndContinuation()
    {
        var page = PageParser.Parse(Fixture("playlist"));

        Assert.Equal("Виктор Цой (все песни)", page.Header?.Title);
        var tracks = page.Sections[0];
        Assert.True(tracks.Items.Count > 50);
        Assert.NotNull(tracks.Continuation);
        Assert.All(tracks.Items, t => Assert.NotNull(t.VideoId));
    }

    [Fact]
    public void Artist_HasTopSongsAndCarousels()
    {
        var page = PageParser.Parse(Fixture("artist"));

        Assert.Equal("Kino", page.Header?.Title);
        Assert.Equal(("Top songs", SectionLayout.List), (page.Sections[0].Title, page.Sections[0].Layout));
        Assert.All(page.Sections[0].Items, t => Assert.True(t.IsPlayable));
        var albums = Assert.Single(page.Sections, s => s.Title == "Albums");
        Assert.All(albums.Items, a => Assert.Equal(MediaKind.Album, a.Kind));
        Assert.Contains(page.Sections, s => s.Title == "Fans might also like" && s.Items.All(i => i.Kind == MediaKind.Artist));
    }

    [Fact]
    public void Watch_HasRadioQueueLyricsAndLikeStatus()
    {
        var info = WatchParser.Parse(Fixture("next"));

        Assert.True(info.UpNext.Count > 10);
        Assert.Equal("xtxjm7ciwmc", info.UpNext[0].VideoId);
        Assert.Equal(("Группа крови", "Кино"), (info.UpNext[0].Title, info.UpNext[0].Artist));
        Assert.All(info.UpNext, i => Assert.True(i.IsPlayable && i.Duration is not null));
        Assert.Equal("MPLYt_OLmD8O5IYNS-1", info.LyricsBrowseId);
        Assert.Equal(LikeStatus.Indifferent, info.LikeStatus);
    }

    [Fact]
    public void Lyrics_ReadsTextAndSource()
    {
        var lyrics = WatchParser.ParseLyrics(Fixture("lyrics"));

        Assert.NotNull(lyrics);
        Assert.StartsWith("Теплое место", lyrics.Text);
        Assert.Equal("Source: LyricFind", lyrics.Source);
        Assert.False(lyrics.IsSynced);
    }

    [Fact]
    public void Continuation_ParsesAppendedItems()
    {
        var json = JsonNode.Parse("""
            {"onResponseReceivedActions":[{"appendContinuationItemsAction":{"continuationItems":[
              {"musicResponsiveListItemRenderer":{"playlistItemData":{"videoId":"v1"},
                "flexColumns":[{"musicResponsiveListItemFlexColumnRenderer":{"text":{"runs":[{"text":"Song"}]}}}]}},
              {"continuationItemRenderer":{"continuationEndpoint":{"continuationCommand":{"token":"next-token"}}}}
            ]}}]}
            """)!;

        var page = PageParser.ParseContinuation(json);

        Assert.Equal("v1", Assert.Single(page.Items).VideoId);
        Assert.Equal("next-token", page.Continuation);
    }
}
