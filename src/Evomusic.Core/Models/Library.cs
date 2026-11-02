namespace Evomusic.Core.Models;

public sealed record AccountInfo(string Name, string? Handle, string? PhotoUrl);

public enum MediaKind
{
    Song,
    Video,
    Album,
    Artist,
    Playlist,
    Other,
}

/// <summary>
/// One entry anywhere in YT Music (song row, album card, artist, playlist...).
/// <see cref="VideoId"/> is set for playable items, <see cref="BrowseId"/> for pages that can be opened.
/// </summary>
public sealed record MediaItem(
    MediaKind Kind,
    string Title,
    string? Subtitle,
    string? ThumbnailUrl,
    string? VideoId,
    string? BrowseId,
    string? Artist = null,
    string? Duration = null)
{
    public bool IsPlayable => VideoId is not null && Kind is MediaKind.Song or MediaKind.Video;
}

public enum SectionLayout
{
    /// <summary>Vertical track list (album/playlist tracks, search results, top songs).</summary>
    List,

    /// <summary>Horizontal row of cards (Home shelves, artist albums).</summary>
    Carousel,
}

public sealed record Section(string? Title, IReadOnlyList<MediaItem> Items, SectionLayout Layout, string? Continuation = null);

public sealed record PageHeader(string Title, string? Subtitle, string? SecondSubtitle, string? Description, string? ThumbnailUrl);

/// <summary>A parsed browse/search response: optional header plus sections in page order.</summary>
public sealed record BrowsePage(PageHeader? Header, IReadOnlyList<Section> Sections);

/// <summary>Items appended by a continuation request (long playlists).</summary>
public sealed record ContinuationPage(IReadOnlyList<MediaItem> Items, string? Continuation);
