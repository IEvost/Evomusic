using System.Text.Json.Nodes;
using Evomusic.Core.Streams;

namespace Evomusic.Core.Tests.Streams;

public class PlayerResponseParserTests
{
    [Fact]
    public void Parse_PicksHighestBitrateAacWithDirectUrl()
    {
        var json = JsonNode.Parse("""
            {"playabilityStatus":{"status":"OK"},"streamingData":{"adaptiveFormats":[
              {"itag":251,"mimeType":"audio/webm; codecs=\"opus\"","bitrate":160000,"url":"https://rr1.googlevideo.com/opus"},
              {"itag":139,"mimeType":"audio/mp4; codecs=\"mp4a.40.5\"","bitrate":48000,"url":"https://rr1.googlevideo.com/low","contentLength":"1000"},
              {"itag":140,"mimeType":"audio/mp4; codecs=\"mp4a.40.2\"","bitrate":130000,"url":"https://rr1.googlevideo.com/aac","contentLength":"3500000","approxDurationMs":"284002"},
              {"itag":141,"mimeType":"audio/mp4; codecs=\"mp4a.40.2\"","bitrate":260000,"signatureCipher":"s=..."},
              {"itag":137,"mimeType":"video/mp4; codecs=\"avc1\"","bitrate":4000000,"url":"https://rr1.googlevideo.com/video"}
            ]}}
            """)!;

        var stream = PlayerResponseParser.Parse(json, out var reason);

        Assert.Null(reason);
        Assert.NotNull(stream);
        Assert.Equal("https://rr1.googlevideo.com/aac", stream.Url.ToString());
        Assert.Equal(("mp4a.40.2", 130000L, 3500000L), (stream.Codec, stream.BitsPerSecond, stream.SizeBytes));
        Assert.Equal(TimeSpan.FromMilliseconds(284002), stream.Duration);
    }

    [Fact]
    public void Parse_ReturnsReasonWhenUnplayable()
    {
        var json = JsonNode.Parse("""{"playabilityStatus":{"status":"LOGIN_REQUIRED","reason":"Sign in to confirm your age"}}""")!;

        Assert.Null(PlayerResponseParser.Parse(json, out var reason));
        Assert.Equal("Sign in to confirm your age", reason);
    }
}
