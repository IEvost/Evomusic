using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Evomusic.Core.Streams;

public sealed class YoutubeExplodeResolver(YoutubeClient youtube) : IStreamResolver
{
    public YoutubeExplodeResolver() : this(new YoutubeClient())
    {
    }

    public async Task<AudioStream> ResolveAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var manifest = await youtube.Videos.Streams.GetManifestAsync(videoId, cancellationToken);

        // AVPlayer can't decode Opus/WebM, so only AAC in MP4 (itag 140 without Premium) is usable.
        var stream = manifest.GetAudioOnlyStreams()
            .Where(s => s.Container == Container.Mp4)
            .OrderByDescending(s => s.Bitrate.BitsPerSecond)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No AAC/MP4 audio stream available for {videoId}.");

        return new AudioStream(
            new Uri(stream.Url),
            stream.Container.Name,
            stream.AudioCodec,
            stream.Bitrate.BitsPerSecond,
            stream.Size.Bytes);
    }
}
