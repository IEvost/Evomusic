using Evomusic.Core.InnerTube;

namespace Evomusic.Core.Tests.InnerTube;

public class YtcfgParserTests
{
    [Fact]
    public void Parse_ExtractsClientIdentity()
    {
        const string html = """
            <script>ytcfg.set({"LOGGED_IN":true,"VISITOR_DATA":"CgtWaXNpdG9y","INNERTUBE_CLIENT_VERSION":"1.20261028.01.00"});</script>
            """;

        var context = YtcfgParser.Parse(html);

        Assert.Equal(new ClientContext("1.20261028.01.00", "CgtWaXNpdG9y", true), context);
    }

    [Fact]
    public void Parse_ThrowsWhenVersionMissing()
    {
        Assert.Throws<InvalidDataException>(() => YtcfgParser.Parse("<html>consent page</html>"));
    }
}
