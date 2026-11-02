using System.Text.RegularExpressions;

namespace Evomusic.Core.InnerTube;

/// <summary>
/// The web player's signature timestamp (STS). Without it the WEB_REMIX <c>player</c> endpoint answers
/// "The page needs to be reloaded" and returns no playback-tracking URLs. Read from the current player
/// script, located via <c>iframe_api</c> (same approach as yt-dlp); cached for a few hours.
/// </summary>
public sealed partial class SignatureTimestamp(HttpClient http)
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);

    private int? _value;
    private DateTimeOffset _fetchedAt;

    public async Task<int?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_value is not null && DateTimeOffset.UtcNow - _fetchedAt < CacheLifetime)
            return _value;

        var iframeApi = await http.GetStringAsync("https://www.youtube.com/iframe_api", cancellationToken);
        var playerId = PlayerIdRegex().Match(iframeApi);
        if (!playerId.Success)
            return _value;

        var baseJs = await http.GetStringAsync(
            $"https://www.youtube.com/s/player/{playerId.Groups[1].Value}/player_ias.vflset/en_US/base.js", cancellationToken);
        var sts = StsRegex().Match(baseJs);
        if (sts.Success)
        {
            _value = int.Parse(sts.Groups[1].Value);
            _fetchedAt = DateTimeOffset.UtcNow;
        }

        return _value;
    }

    [GeneratedRegex(@"player\\?/([0-9a-fA-F]{8})\\?/")]
    private static partial Regex PlayerIdRegex();

    [GeneratedRegex(@"signatureTimestamp[=:](\d+)")]
    private static partial Regex StsRegex();
}
