namespace Evomusic.Core.Streams;

/// <summary>
/// A direct googlevideo URL for one audio-only format. <see cref="Duration"/> comes from YouTube's metadata:
/// AVPlayer misreads the length of YouTube's fragmented MP4 files (about double), so the player must not rely on it.
/// </summary>
public sealed record AudioStream(Uri Url, string Container, string Codec, long BitsPerSecond, long SizeBytes, TimeSpan? Duration = null);

/// <summary>Turns a video ID into a playable audio URL. Isolated so the extraction backend can be swapped.</summary>
public interface IStreamResolver
{
    Task<AudioStream> ResolveAsync(string videoId, CancellationToken cancellationToken = default);
}
