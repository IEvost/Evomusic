using Evomusic.Core.Auth;

namespace Evomusic.Core.Tests.Auth;

public class CookieHeaderParserTests
{
    [Fact]
    public void Parse_SplitsPairsAndStripsHeaderName()
    {
        var cookies = CookieHeaderParser.Parse("Cookie: SID=abc; SAPISID=x/y=z ; PREF=f6=40000000&tz=Europe.Moscow;");

        Assert.Collection(cookies,
            c => Assert.Equal(("SID", "abc"), (c.Name, c.Value)),
            c => Assert.Equal(("SAPISID", "x/y=z"), (c.Name, c.Value)),
            c => Assert.Equal(("PREF", "f6=40000000&tz=Europe.Moscow"), (c.Name, c.Value)));
        Assert.All(cookies, c => Assert.Equal(".youtube.com", c.Domain));
        Assert.True(CookieHeaderParser.HasAuthCookie(cookies));
    }

    [Fact]
    public void HasAuthCookie_FalseWithoutSapisid()
    {
        Assert.False(CookieHeaderParser.HasAuthCookie(CookieHeaderParser.Parse("SID=abc; HSID=def")));
    }
}
