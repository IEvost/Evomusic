using System.Text.RegularExpressions;

namespace Evomusic.Core.Models;

public sealed partial record Track(string VideoId, string Title, string? Artist, string? ThumbnailUrl)
{
    /// <summary>Builds a track from a Home card; the subtitle looks like "Artist • 121M plays".</summary>
    public static Track? FromShelfItem(ShelfItem item) =>
        item.VideoId is null
            ? null
            : new Track(item.VideoId, item.Title, item.Subtitle?.Split(" • ")[0], item.ThumbnailUrl);

    /// <summary>
    /// Artwork URL sized for the lock screen / Now Playing. YT Music thumbnails on googleusercontent
    /// carry the size in the URL (<c>=w120-h120</c>), so a larger one can be requested directly.
    /// </summary>
    public string? ArtworkUrl(int size) =>
        ThumbnailUrl is null ? null : ThumbnailSizeRegex().Replace(ThumbnailUrl, $"=w{size}-h{size}");

    [GeneratedRegex(@"=w\d+-h\d+")]
    private static partial Regex ThumbnailSizeRegex();
}
