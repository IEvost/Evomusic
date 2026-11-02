using System.Text.Json.Nodes;
using Evomusic.Core.InnerTube;
using Evomusic.Core.InnerTube.Parsers;
using Evomusic.Core.Models;

namespace Evomusic.Core;

/// <summary>Typed YT Music operations on top of <see cref="InnerTubeClient"/>.</summary>
public sealed class YouTubeMusicService(InnerTubeClient client)
{
    public const string HomeId = "FEmusic_home";
    public const string LikedSongsId = "VLLM";
    public const string LibraryPlaylistsId = "FEmusic_liked_playlists";
    public const string LibraryAlbumsId = "FEmusic_liked_albums";
    public const string LibraryArtistsId = "FEmusic_library_corpus_track_artists";
    public const string LibrarySongsId = "FEmusic_liked_videos";
    public const string HistoryId = "FEmusic_history";

    public async Task<AccountInfo?> GetAccountAsync(CancellationToken cancellationToken = default) =>
        AccountParser.Parse(await client.PostAsync("account/account_menu", cancellationToken: cancellationToken));

    /// <summary>Opens any page by browse ID: Home, album, artist, playlist or a library section.</summary>
    public async Task<BrowsePage> BrowseAsync(string browseId, CancellationToken cancellationToken = default) =>
        PageParser.Parse(await client.PostAsync("browse", new JsonObject { ["browseId"] = browseId }, cancellationToken));

    public async Task<ContinuationPage> ContinueAsync(string continuation, CancellationToken cancellationToken = default) =>
        PageParser.ParseContinuation(await client.PostAsync("browse", new JsonObject { ["continuation"] = continuation }, cancellationToken));

    public async Task<BrowsePage> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        PageParser.Parse(await client.PostAsync("search", new JsonObject { ["query"] = query }, cancellationToken));

    public async Task<IReadOnlyList<string>> GetSearchSuggestionsAsync(string input, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsync("music/get_search_suggestions", new JsonObject { ["input"] = input }, cancellationToken);
        return response.Items("contents", 0, "searchSuggestionsSectionRenderer", "contents")
            .Select(c => c.At("searchSuggestionRenderer", "suggestion").Text())
            .OfType<string>()
            .ToList();
    }
}
