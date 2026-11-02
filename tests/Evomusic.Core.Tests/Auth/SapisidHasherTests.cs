using Evomusic.Core.Auth;

namespace Evomusic.Core.Tests.Auth;

public class SapisidHasherTests
{
    [Fact]
    public void CreateAuthorizationHeader_MatchesKnownVector()
    {
        var header = SapisidHasher.CreateAuthorizationHeader("abc123/XYZ", DateTimeOffset.FromUnixTimeSeconds(1700000000));

        Assert.Equal("SAPISIDHASH 1700000000_289fd730e8d0d6300b332703c750af560713e6b1", header);
    }

    [Fact]
    public void CreateAuthorizationHeader_RejectsEmptySapisid()
    {
        Assert.ThrowsAny<ArgumentException>(() => SapisidHasher.CreateAuthorizationHeader("", DateTimeOffset.UtcNow));
    }
}
