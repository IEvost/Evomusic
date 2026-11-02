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

    private const string CpnAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    private static readonly SignatureTimestamp SignatureTimestamps = new(new HttpClient { Timeout = TimeSpan.FromSeconds(20) });

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

    /// <summary>Like status, lyrics page and the radio ("up next") for a track, as the web player requests it.</summary>
    public async Task<WatchInfo> GetWatchInfoAsync(string videoId, CancellationToken cancellationToken = default) =>
        WatchParser.Parse(await client.PostAsync("next", new JsonObject
        {
            ["videoId"] = videoId,
            ["playlistId"] = "RDAMVM" + videoId,
            ["isAudioOnly"] = true,
            ["enablePersistentPlaylistPanel"] = true,
            ["tunerSettingValue"] = "AUTOMIX_SETTING_NORMAL",
        }, cancellationToken));

    public async Task<Lyrics?> GetLyricsAsync(string lyricsBrowseId, CancellationToken cancellationToken = default) =>
        WatchParser.ParseLyrics(await client.PostAsync("browse", new JsonObject { ["browseId"] = lyricsBrowseId }, cancellationToken));

    public Task RateAsync(string videoId, LikeStatus status, CancellationToken cancellationToken = default)
    {
        var endpoint = status switch
        {
            LikeStatus.Like => "like/like",
            LikeStatus.Dislike => "like/dislike",
            _ => "like/removelike",
        };
        return client.PostAsync(endpoint, new JsonObject { ["target"] = new JsonObject { ["videoId"] = videoId } }, cancellationToken);
    }

    /// <summary>The account's playlists (library), as targets for "Add to playlist".</summary>
    public async Task<IReadOnlyList<MediaItem>> GetLibraryPlaylistsAsync(CancellationToken cancellationToken = default)
    {
        var page = await BrowseAsync(LibraryPlaylistsId, cancellationToken);
        return page.Sections
            .SelectMany(s => s.Items)
            .Where(i => i.Kind == MediaKind.Playlist && i.BrowseId is not null && i.BrowseId != LikedSongsId)
            .ToList();
    }

    public async Task AddToPlaylistAsync(string playlistBrowseId, string videoId, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsync("browse/edit_playlist", new JsonObject
        {
            ["playlistId"] = StripVl(playlistBrowseId),
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["action"] = "ACTION_ADD_VIDEO",
                    ["addedVideoId"] = videoId,
                    ["dedupeOption"] = "DEDUPE_OPTION_SKIP",
                },
            },
        }, cancellationToken);

        if (response.Str("status") is { } status && status != "STATUS_SUCCEEDED")
            throw new InnerTubeException($"Could not add to playlist ({status}).");
    }

    /// <summary>Creates a private playlist; returns its browse ID.</summary>
    public async Task<string> CreatePlaylistAsync(string title, IEnumerable<string> videoIds, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsync("playlist/create", new JsonObject
        {
            ["title"] = title,
            ["description"] = "",
            ["privacyStatus"] = "PRIVATE",
            ["videoIds"] = new JsonArray(videoIds.Select(id => (JsonNode)id).ToArray()),
        }, cancellationToken);

        return "VL" + (response.Str("playlistId") ?? throw new InnerTubeException("Playlist was not created."));
    }

    /// <summary>
    /// Records the play in the account's YouTube history, like the web player does, so recommendations
    /// keep matching music.youtube.com. Uses the signed-in player response's tracking URL (same as yt-dlp --mark-watched).
    /// </summary>
    public async Task ReportPlaybackAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["videoId"] = videoId };
        if (await SignatureTimestamps.GetAsync(cancellationToken) is { } sts)
        {
            body["playbackContext"] = new JsonObject
            {
                ["contentPlaybackContext"] = new JsonObject { ["signatureTimestamp"] = sts },
            };
        }

        var player = await client.PostAsync("player", body, cancellationToken);
        if (player.Str("playbackTracking", "videostatsPlaybackUrl", "baseUrl") is not { } baseUrl)
        {
            var reason = player.Str("playabilityStatus", "reason") ?? player.Str("playabilityStatus", "status") ?? "no tracking URL";
            throw new InnerTubeException($"Play not recorded: {reason}");
        }

        // Same parameters ytmusicapi's add_history_item sends.
        var cpn = string.Concat(Enumerable.Range(0, 16).Select(_ => CpnAlphabet[Random.Shared.Next(CpnAlphabet.Length)]));
        await client.PingAsync(new Uri($"{baseUrl}&ver=2&c=WEB_REMIX&cpn={cpn}"), cancellationToken);
    }

    private static string StripVl(string browseId) =>
        browseId.StartsWith("VL", StringComparison.Ordinal) ? browseId[2..] : browseId;
}
