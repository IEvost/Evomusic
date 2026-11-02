using System.Text.Json.Nodes;
using Evomusic.Core.Models;

namespace Evomusic.Core.InnerTube.Parsers;

/// <summary>Parses the <c>next</c> response (queue panel, lyrics tab, like button) and the lyrics browse page.</summary>
public static class WatchParser
{
    public static WatchInfo Parse(JsonNode response)
    {
        var tabs = response.Items(
            "contents", "singleColumnMusicWatchNextResultsRenderer", "tabbedRenderer", "watchNextTabbedResultsRenderer", "tabs").ToList();

        var panel = tabs.FirstOrDefault()
            .Items("tabRenderer", "content", "musicQueueRenderer", "content", "playlistPanelRenderer", "contents")
            .Select(item => item.At("playlistPanelVideoRenderer")
                ?? item.At("playlistPanelVideoWrapperRenderer", "primaryRenderer", "playlistPanelVideoRenderer"))
            .OfType<JsonNode>()
            .Select(ParsePanelVideo)
            .OfType<MediaItem>()
            .ToList();

        var lyricsId = tabs
            .Select(t => t.Str("tabRenderer", "endpoint", "browseEndpoint", "browseId"))
            .FirstOrDefault(id => id?.StartsWith("MPLY", StringComparison.Ordinal) == true);

        var likeStatus = response.Str("playerOverlays", "playerOverlayRenderer", "actions", 0, "likeButtonRenderer", "likeStatus") switch
        {
            "LIKE" => LikeStatus.Like,
            "DISLIKE" => LikeStatus.Dislike,
            _ => LikeStatus.Indifferent,
        };

        return new WatchInfo(panel, lyricsId, likeStatus);
    }

    public static Lyrics? ParseLyrics(JsonNode response)
    {
        var shelf = response.At("contents", "sectionListRenderer", "contents", 0, "musicDescriptionShelfRenderer");
        var text = shelf.At("description").Text();
        return string.IsNullOrWhiteSpace(text) ? null : new Lyrics(text, [], shelf.At("footer").Text() ?? "YouTube Music");
    }

    private static MediaItem? ParsePanelVideo(JsonNode video)
    {
        var title = video.At("title").Text();
        var videoId = video.Str("videoId") ?? video.Str("navigationEndpoint", "watchEndpoint", "videoId");
        if (title is null || videoId is null)
            return null;

        var byline = video.At("longBylineText") ?? video.At("shortBylineText");
        return new MediaItem(
            MediaKind.Song,
            title,
            byline.Text(),
            video.At("thumbnail").LargestThumbnail(),
            videoId,
            null,
            ItemParser.ArtistFromRuns(byline.Items("runs")) ?? video.At("shortBylineText").Text(),
            video.At("lengthText").Text());
    }
}
