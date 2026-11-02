using System.Text.Json.Nodes;
using Evomusic.Core.InnerTube.Parsers;

namespace Evomusic.Core.Tests.InnerTube;

// Minimal hand-written fixtures mirroring the real response shape.
// Replace with captured real responses once the app can dump them.
public class ParserTests
{
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
    public void HomeParser_ReadsBothItemKinds()
    {
        var json = JsonNode.Parse("""
            {"contents":{"singleColumnBrowseResultsRenderer":{"tabs":[{"tabRenderer":{"content":{"sectionListRenderer":{"contents":[
              {"musicCarouselShelfRenderer":{
                "header":{"musicCarouselShelfBasicHeaderRenderer":{"title":{"runs":[{"text":"Quick picks"}]}}},
                "contents":[{"musicResponsiveListItemRenderer":{
                  "playlistItemData":{"videoId":"vid1"},
                  "thumbnail":{"musicThumbnailRenderer":{"thumbnail":{"thumbnails":[{"url":"https://t1"}]}}},
                  "flexColumns":[
                    {"musicResponsiveListItemFlexColumnRenderer":{"text":{"runs":[{"text":"Song"}]}}},
                    {"musicResponsiveListItemFlexColumnRenderer":{"text":{"runs":[{"text":"Artist"},{"text":" • "},{"text":"Album"}]}}}]}}]}},
              {"musicCarouselShelfRenderer":{
                "header":{"musicCarouselShelfBasicHeaderRenderer":{"title":{"runs":[{"text":"Mixed for you"}]}}},
                "contents":[{"musicTwoRowItemRenderer":{
                  "title":{"runs":[{"text":"My Supermix"}]},
                  "subtitle":{"runs":[{"text":"Playlist"}]},
                  "navigationEndpoint":{"browseEndpoint":{"browseId":"VLRDTMAK5uy"}},
                  "thumbnailRenderer":{"musicThumbnailRenderer":{"thumbnail":{"thumbnails":[{"url":"https://t2"}]}}}}}]}},
              {"musicTastebuilderShelfRenderer":{}}
            ]}}}}]}}}
            """)!;

        var shelves = HomeParser.Parse(json);

        Assert.Equal(["Quick picks", "Mixed for you"], shelves.Select(s => s.Title));
        var song = Assert.Single(shelves[0].Items);
        Assert.Equal(("Song", "Artist • Album", "https://t1", "vid1"), (song.Title, song.Subtitle, song.ThumbnailUrl, song.VideoId));
        var mix = Assert.Single(shelves[1].Items);
        Assert.Equal(("My Supermix", "VLRDTMAK5uy"), (mix.Title, mix.BrowseId));
    }
}
