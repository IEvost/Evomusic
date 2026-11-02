using Evomusic.Core.Models;

namespace Evomusic.App.ViewModels;

/// <summary>
/// Pages are rendered as one flat, virtualized list of rows, so a 5,000-track playlist scrolls
/// as smoothly as a Home page and continuation pages can simply be appended.
/// </summary>
public abstract record Row;

/// <summary>Album/playlist/artist header with Play and Shuffle buttons when the page has playable tracks.</summary>
public sealed record HeaderRow(PageHeader Header, bool CanPlay, bool IsArtist) : Row;

public sealed record TitleRow(string Title) : Row;

/// <summary>A track or link row. Rows with the same <see cref="Group"/> form one play queue.</summary>
public sealed record ItemRow(MediaItem Item, int Group, bool ShowArtwork) : Row;

public sealed record CarouselRow(Section Section) : Row;

/// <summary>Library entry linking to a library browse page.</summary>
public sealed record LinkRow(string Title, string Symbol, string BrowseId) : Row;

public static class RowBuilder
{
    public static List<Row> Build(BrowsePage page, bool isArtist = false)
    {
        var rows = new List<Row>();
        var hasTracks = page.Sections.Any(s => s.Layout == SectionLayout.List && s.Items.Any(i => i.IsPlayable));
        if (page.Header is { } header)
            rows.Add(new HeaderRow(header, hasTracks, isArtist));

        // Album tracks share the cover; showing it on every row is noise, like in Apple Music.
        var isAlbum = page.Header is not null && !isArtist && page.Header.SecondSubtitle?.Contains("Album") == true
            || page.Header?.SecondSubtitle?.StartsWith("Single") == true || page.Header?.SecondSubtitle?.StartsWith("EP") == true;

        for (var group = 0; group < page.Sections.Count; group++)
        {
            var section = page.Sections[group];
            if (section.Layout == SectionLayout.Carousel)
            {
                rows.Add(new CarouselRow(section));
                continue;
            }

            if (section.Title is { } title)
                rows.Add(new TitleRow(title));
            rows.AddRange(section.Items.Select(i => new ItemRow(i, group, !isAlbum)));
        }

        return rows;
    }

    /// <summary>The play queue for a tapped row: every playable row of the same group, in page order.</summary>
    public static List<MediaItem> QueueFor(IEnumerable<Row> rows, ItemRow tapped) =>
        rows.OfType<ItemRow>().Where(r => r.Group == tapped.Group).Select(r => r.Item).ToList();
}
