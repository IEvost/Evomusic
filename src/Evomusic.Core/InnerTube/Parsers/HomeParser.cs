using System.Text.Json.Nodes;
using Evomusic.Core.Models;

namespace Evomusic.Core.InnerTube.Parsers;

/// <summary>Parses the <c>browse</c> response for <c>FEmusic_home</c> into shelves.</summary>
public static class HomeParser
{
    public static IReadOnlyList<Shelf> Parse(JsonNode response)
    {
        var sections = response
            .Items("contents", "singleColumnBrowseResultsRenderer", "tabs", 0, "tabRenderer", "content", "sectionListRenderer", "contents");

        var shelves = new List<Shelf>();
        foreach (var section in sections)
        {
            var carousel = section.At("musicCarouselShelfRenderer");
            if (carousel is null)
                continue;

            var title = carousel.At("header", "musicCarouselShelfBasicHeaderRenderer", "title").Text();
            var items = carousel.Items("contents").Select(ParseItem).OfType<ShelfItem>().ToList();
            if (title is not null && items.Count > 0)
                shelves.Add(new Shelf(title, items));
        }

        return shelves;
    }

    private static ShelfItem? ParseItem(JsonNode item)
    {
        if (item.At("musicTwoRowItemRenderer") is { } twoRow)
        {
            var title = twoRow.At("title").Text();
            if (title is null)
                return null;

            return new ShelfItem(
                title,
                twoRow.At("subtitle").Text(),
                twoRow.At("thumbnailRenderer", "musicThumbnailRenderer", "thumbnail").LargestThumbnail(),
                twoRow.Str("navigationEndpoint", "watchEndpoint", "videoId"),
                twoRow.Str("navigationEndpoint", "browseEndpoint", "browseId"));
        }

        if (item.At("musicResponsiveListItemRenderer") is { } listItem)
        {
            var titleText = listItem.At("flexColumns", 0, "musicResponsiveListItemFlexColumnRenderer", "text");
            var title = titleText.Text();
            if (title is null)
                return null;

            return new ShelfItem(
                title,
                listItem.At("flexColumns", 1, "musicResponsiveListItemFlexColumnRenderer", "text").Text(),
                listItem.At("thumbnail", "musicThumbnailRenderer", "thumbnail").LargestThumbnail(),
                listItem.Str("playlistItemData", "videoId") ?? titleText.Str("runs", 0, "navigationEndpoint", "watchEndpoint", "videoId"),
                listItem.Str("navigationEndpoint", "browseEndpoint", "browseId"));
        }

        return null;
    }
}
