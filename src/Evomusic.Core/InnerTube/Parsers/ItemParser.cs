using System.Text.Json.Nodes;
using Evomusic.Core.Models;

namespace Evomusic.Core.InnerTube.Parsers;

/// <summary>Parses the item renderers shared by all YT Music pages into <see cref="MediaItem"/>.</summary>
internal static class ItemParser
{
    public static MediaItem? Parse(JsonNode node) =>
        node.At("musicResponsiveListItemRenderer") is { } listItem ? ParseListItem(listItem)
        : node.At("musicTwoRowItemRenderer") is { } twoRow ? ParseTwoRow(twoRow)
        : null;

    /// <summary>The "Top result" card at the top of search results.</summary>
    public static MediaItem? ParseCard(JsonNode card)
    {
        var title = card.At("title").Text();
        if (title is null)
            return null;

        var titleRun = card.At("title", "runs", 0);
        var (kind, videoId, browseId) = Classify(card.At("onTap") ?? titleRun.At("navigationEndpoint"), card);
        return new MediaItem(
            kind,
            title,
            card.At("subtitle").Text(),
            card.At("thumbnail", "musicThumbnailRenderer", "thumbnail").LargestThumbnail(),
            videoId,
            browseId,
            ArtistFromRuns(card.Items("subtitle", "runs")));
    }

    private static MediaItem? ParseListItem(JsonNode item)
    {
        var columns = item.Items("flexColumns")
            .Select(c => c.At("musicResponsiveListItemFlexColumnRenderer", "text"))
            .ToList();
        var titleText = columns.FirstOrDefault();
        var title = titleText.Text();
        if (title is null)
            return null;

        var subtitle = string.Join(" • ", columns.Skip(1).Select(c => c.Text()).Where(t => !string.IsNullOrWhiteSpace(t)));
        var endpoint = item.At("navigationEndpoint") ?? titleText.At("runs", 0, "navigationEndpoint");
        var (kind, videoId, browseId) = Classify(endpoint, item);

        return new MediaItem(
            kind,
            title,
            subtitle.Length > 0 ? subtitle : null,
            item.At("thumbnail", "musicThumbnailRenderer", "thumbnail").LargestThumbnail(),
            videoId,
            browseId,
            ArtistFromRuns(columns.Skip(1).SelectMany(c => c.Items("runs"))),
            item.At("fixedColumns", 0, "musicResponsiveListItemFixedColumnRenderer", "text").Text());
    }

    private static MediaItem? ParseTwoRow(JsonNode item)
    {
        var title = item.At("title").Text();
        if (title is null)
            return null;

        var (kind, videoId, browseId) = Classify(item.At("navigationEndpoint") ?? item.At("title", "runs", 0, "navigationEndpoint"), item);
        return new MediaItem(
            kind,
            title,
            item.At("subtitle").Text(),
            item.At("thumbnailRenderer", "musicThumbnailRenderer", "thumbnail").LargestThumbnail(),
            videoId,
            browseId,
            ArtistFromRuns(item.Items("subtitle", "runs")));
    }

    /// <summary>
    /// A browse endpoint (album/artist/playlist page) wins over a play action, because album and
    /// playlist cards also carry a "play" overlay. Otherwise the item is a song or video.
    /// </summary>
    private static (MediaKind Kind, string? VideoId, string? BrowseId) Classify(JsonNode? endpoint, JsonNode item)
    {
        if (endpoint.At("browseEndpoint") is { } browse)
        {
            var pageType = browse.Str("browseEndpointContextSupportedConfigs", "browseEndpointContextMusicConfig", "pageType");
            var kind = pageType switch
            {
                "MUSIC_PAGE_TYPE_ALBUM" or "MUSIC_PAGE_TYPE_AUDIOBOOK" => MediaKind.Album,
                "MUSIC_PAGE_TYPE_ARTIST" or "MUSIC_PAGE_TYPE_USER_CHANNEL" or "MUSIC_PAGE_TYPE_LIBRARY_ARTIST" => MediaKind.Artist,
                "MUSIC_PAGE_TYPE_PLAYLIST" => MediaKind.Playlist,
                _ => MediaKind.Other,
            };
            return (kind, null, browse.Str("browseId"));
        }

        var watch = endpoint.At("watchEndpoint")
            ?? item.At("overlay", "musicItemThumbnailOverlayRenderer", "content", "musicPlayButtonRenderer", "playNavigationEndpoint", "watchEndpoint");
        var videoId = item.Str("playlistItemData", "videoId") ?? watch.Str("videoId");
        if (videoId is null)
            return (MediaKind.Other, null, null);

        var videoType = watch.Str("watchEndpointSupportedConfigs", "watchEndpointMusicSupportedConfigs", "watchEndpointMusicConfig", "musicVideoType");
        var isSong = videoType is null or "MUSIC_VIDEO_TYPE_ATV";
        return (isSong ? MediaKind.Song : MediaKind.Video, videoId, null);
    }

    /// <summary>Artist names are the subtitle runs that link to artist pages.</summary>
    private static string? ArtistFromRuns(IEnumerable<JsonNode> runs)
    {
        var artists = runs
            .Where(r => r.Str("navigationEndpoint", "browseEndpoint", "browseEndpointContextSupportedConfigs", "browseEndpointContextMusicConfig", "pageType")
                is "MUSIC_PAGE_TYPE_ARTIST" or "MUSIC_PAGE_TYPE_USER_CHANNEL")
            .Select(r => r.Str("text"))
            .OfType<string>()
            .Distinct()
            .ToList();
        return artists.Count > 0 ? string.Join(", ", artists) : null;
    }
}
