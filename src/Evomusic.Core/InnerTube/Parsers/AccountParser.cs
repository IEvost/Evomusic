using System.Text.Json.Nodes;
using Evomusic.Core.Models;

namespace Evomusic.Core.InnerTube.Parsers;

/// <summary>Parses the <c>account/account_menu</c> response.</summary>
public static class AccountParser
{
    public static AccountInfo? Parse(JsonNode response)
    {
        var header = response.At("actions", 0, "openPopupAction", "popup", "multiPageMenuRenderer", "header", "activeAccountHeaderRenderer");
        var name = header.At("accountName").Text();
        if (name is null)
            return null;

        return new AccountInfo(
            name,
            header.At("channelHandle").Text(),
            header.At("accountPhoto").LargestThumbnail());
    }
}
