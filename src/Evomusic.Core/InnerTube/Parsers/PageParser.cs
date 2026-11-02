using System.Text.Json.Nodes;
using Evomusic.Core.Models;

namespace Evomusic.Core.InnerTube.Parsers;

/// <summary>
/// Layout-agnostic parser for browse and search responses. Instead of hard-coding each page's path,
/// it walks the response in document order and picks up the known shelf and header renderers, so
/// Home, search, album, playlist, artist and library pages all share one parser and survive YouTube
/// moving shelves between columns.
/// </summary>
public static class PageParser
{
    private static readonly HashSet<string> SkippedKeys =
    [
        "responseContext", "frameworkUpdates", "trackingParams", "microformat", "menu", "menuRenderer",
        "background", "thumbnail", "thumbnailRenderer", "overlay", "playNavigationEndpoint",
    ];

    private static readonly string[] HeaderKeys =
    [
        "musicResponsiveHeaderRenderer", "musicImmersiveHeaderRenderer", "musicVisualHeaderRenderer", "musicDetailHeaderRenderer",
    ];

    public static BrowsePage Parse(JsonNode response)
    {
        PageHeader? header = null;
        var sections = new List<Section>();
        Walk(response, ref header, sections);
        sections = MergeUntitledLists(sections);

        // Album tracks come without artwork or artist; inherit them from the page header.
        if (header is not null)
        {
            sections = sections
                .Select(s => s.Layout == SectionLayout.List ? s with { Items = s.Items.Select(i => Inherit(i, header)).ToList() } : s)
                .ToList();
        }

        return new BrowsePage(header, sections);
    }

    /// <summary>Parses a <c>browse</c> continuation response (next page of a long playlist).</summary>
    public static ContinuationPage ParseContinuation(JsonNode response)
    {
        var items = response.Items("onResponseReceivedActions", 0, "appendContinuationItemsAction", "continuationItems").ToList();
        return new ContinuationPage(
            items.Select(ItemParser.Parse).OfType<MediaItem>().ToList(),
            items.Select(ContinuationToken).OfType<string>().LastOrDefault());
    }

    private static void Walk(JsonNode? node, ref PageHeader? header, List<Section> sections)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (value is null || SkippedKeys.Contains(key))
                        continue;

                    if (TryParseSection(key, value) is { } section)
                    {
                        if (section.Items.Count > 0)
                            sections.Add(section);
                        continue;
                    }

                    if (header is null && HeaderKeys.Contains(key))
                    {
                        header = ParseHeader(value);
                        continue;
                    }

                    Walk(value, ref header, sections);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                    Walk(item, ref header, sections);
                break;
        }
    }

    private static Section? TryParseSection(string key, JsonNode shelf) => key switch
    {
        "musicCarouselShelfRenderer" => new Section(
            shelf.At("header", "musicCarouselShelfBasicHeaderRenderer", "title").Text(),
            Items(shelf.Items("contents")),
            SectionLayout.Carousel),
        "musicShelfRenderer" => new Section(
            shelf.At("title").Text(),
            Items(shelf.Items("contents")),
            SectionLayout.List,
            Continuation(shelf)),
        "musicPlaylistShelfRenderer" => new Section(
            null,
            Items(shelf.Items("contents")),
            SectionLayout.List,
            Continuation(shelf)),
        "gridRenderer" => new Section(
            shelf.At("header", "gridHeaderRenderer", "title").Text(),
            Items(shelf.Items("items")),
            SectionLayout.List),
        "musicCardShelfRenderer" => new Section(
            "Top result",
            new[] { ItemParser.ParseCard(shelf) }.Concat(Items(shelf.Items("contents"))).OfType<MediaItem>().ToList(),
            SectionLayout.List),
        // Search results (2026 layout) are bare rows, one per item section. Sections wrapping a shelf
        // have no direct items and are walked into instead.
        "itemSectionRenderer" when Items(shelf.Items("contents")) is { Count: > 0 } rows => new Section(null, rows, SectionLayout.List),
        _ => null,
    };

    /// <summary>Joins adjacent untitled lists (search rows arrive one item section each).</summary>
    private static List<Section> MergeUntitledLists(List<Section> sections)
    {
        var merged = new List<Section>();
        foreach (var section in sections)
        {
            if (merged.Count > 0 && merged[^1] is { Title: null, Layout: SectionLayout.List, Continuation: null } previous
                && section is { Title: null, Layout: SectionLayout.List })
            {
                merged[^1] = section with { Items = [.. previous.Items, .. section.Items] };
                continue;
            }

            merged.Add(section);
        }

        return merged;
    }

    private static List<MediaItem> Items(IEnumerable<JsonNode> renderers) =>
        renderers.Select(ItemParser.Parse).OfType<MediaItem>().ToList();

    private static string? Continuation(JsonNode shelf) =>
        shelf.Items("contents").Select(ContinuationToken).OfType<string>().LastOrDefault()
        ?? shelf.Str("continuations", 0, "nextContinuationData", "continuation");

    private static string? ContinuationToken(JsonNode item) =>
        item.Str("continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");

    private static PageHeader? ParseHeader(JsonNode header)
    {
        var title = header.At("title").Text();
        if (title is null)
            return null;

        var strapline = header.At("straplineTextOne").Text();
        var subtitle = header.At("subtitle").Text();
        return new PageHeader(
            title,
            strapline ?? subtitle,
            strapline is null ? header.At("secondSubtitle").Text() : subtitle,
            header.At("description", "musicDescriptionShelfRenderer", "description").Text() ?? header.At("description").Text(),
            header.At("thumbnail", "musicThumbnailRenderer", "thumbnail").LargestThumbnail()
                ?? header.At("foregroundThumbnail", "musicThumbnailRenderer", "thumbnail").LargestThumbnail()
                ?? header.At("thumbnail", "croppedSquareThumbnailRenderer", "thumbnail").LargestThumbnail());
    }

    private static MediaItem Inherit(MediaItem item, PageHeader header) => item with
    {
        ThumbnailUrl = item.ThumbnailUrl ?? header.ThumbnailUrl,
        Artist = item.Artist ?? (item.IsPlayable ? header.Subtitle : null),
    };
}
