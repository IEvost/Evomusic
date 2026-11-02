namespace Evomusic.Core.Models;

public sealed record AccountInfo(string Name, string? Handle, string? PhotoUrl);

/// <summary>A horizontal row on the Home page ("Quick picks", "Listen again", mixes, ...).</summary>
public sealed record Shelf(string Title, IReadOnlyList<ShelfItem> Items);

/// <summary>
/// One card in a shelf. <see cref="VideoId"/> is set for playable songs,
/// <see cref="BrowseId"/> for albums, playlists and artists.
/// </summary>
public sealed record ShelfItem(string Title, string? Subtitle, string? ThumbnailUrl, string? VideoId, string? BrowseId);
