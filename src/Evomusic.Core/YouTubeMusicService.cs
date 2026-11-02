using System.Text.Json.Nodes;
using Evomusic.Core.InnerTube;
using Evomusic.Core.InnerTube.Parsers;
using Evomusic.Core.Models;

namespace Evomusic.Core;

/// <summary>Typed YT Music operations on top of <see cref="InnerTubeClient"/>.</summary>
public sealed class YouTubeMusicService(InnerTubeClient client)
{
    public async Task<AccountInfo?> GetAccountAsync(CancellationToken cancellationToken = default) =>
        AccountParser.Parse(await client.PostAsync("account/account_menu", cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<Shelf>> GetHomeAsync(CancellationToken cancellationToken = default) =>
        HomeParser.Parse(await client.PostAsync("browse", new JsonObject { ["browseId"] = "FEmusic_home" }, cancellationToken));
}
