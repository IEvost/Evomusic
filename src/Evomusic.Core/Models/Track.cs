using System.Text.RegularExpressions;

namespace Evomusic.Core.Models;

public sealed partial record Track(string VideoId, string Title, string? Artist, string? ThumbnailUrl)
{
    /// <summary>
    /// Builds a track from a playable item. Falls back to the first subtitle part
    /// ("Artist • 121M plays") when no run links to an artist page.
    /// </summary>
    public static Track? FromMediaItem(MediaItem item) =>
        item.IsPlayable
            ? new Track(item.VideoId!, item.Title, item.Artist ?? item.Subtitle?.Split(" • ")[0], item.ThumbnailUrl)
            : null;

    /// <summary>
    /// Artwork URL sized for the lock screen / Now Playing. YT Music thumbnails on googleusercontent
    /// carry the size in the URL (<c>=w120-h120</c>), so a larger one can be requested directly.
    /// </summary>
    public string? ArtworkUrl(int size) =>
        ThumbnailUrl is null ? null : ThumbnailSizeRegex().Replace(ThumbnailUrl, $"=w{size}-h{size}");

    [GeneratedRegex(@"=w\d+-h\d+")]
    private static partial Regex ThumbnailSizeRegex();
}
