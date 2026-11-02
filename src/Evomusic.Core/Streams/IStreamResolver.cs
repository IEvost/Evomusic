namespace Evomusic.Core.Streams;

/// <summary>A direct googlevideo URL for one audio-only format.</summary>
public sealed record AudioStream(Uri Url, string Container, string Codec, long BitsPerSecond, long SizeBytes);

/// <summary>Turns a video ID into a playable audio URL. Isolated so the extraction backend can be swapped.</summary>
public interface IStreamResolver
{
    Task<AudioStream> ResolveAsync(string videoId, CancellationToken cancellationToken = default);
}
