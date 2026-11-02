using System.Text.Json.Serialization;

namespace Evomusic.Core.Auth;

/// <summary>Source-generated serializer for cookie persistence (trimming-safe on iOS).</summary>
[JsonSerializable(typeof(List<StoredCookie>))]
public sealed partial class AuthJsonContext : JsonSerializerContext;
