using System.Text.Json.Nodes;

namespace Evomusic.Core.InnerTube.Parsers;

/// <summary>Null-tolerant helpers for walking InnerTube's deeply nested renderer JSON.</summary>
internal static class JsonNav
{
    /// <summary>Follows a path of object keys and array indexes; returns null as soon as a step is missing.</summary>
    public static JsonNode? At(this JsonNode? node, params object[] path)
    {
        foreach (var step in path)
        {
            node = step switch
            {
                string key when node is JsonObject obj => obj[key],
                int index when node is JsonArray array && index >= 0 && index < array.Count => array[index],
                int index when node is JsonArray array && index < 0 && array.Count + index >= 0 => array[array.Count + index],
                _ => null,
            };

            if (node is null)
                return null;
        }

        return node;
    }

    public static string? Str(this JsonNode? node, params object[] path) =>
        node.At(path) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public static IEnumerable<JsonNode> Items(this JsonNode? node, params object[] path) =>
        node.At(path) is JsonArray array ? array.OfType<JsonNode>() : [];

    /// <summary>Concatenates a <c>{ "runs": [{ "text": ... }] }</c> or <c>{ "simpleText": ... }</c> text object.</summary>
    public static string? Text(this JsonNode? textObject)
    {
        if (textObject.Str("simpleText") is { } simple)
            return simple;

        var runs = textObject.Items("runs").Select(run => run.Str("text")).OfType<string>().ToList();
        return runs.Count > 0 ? string.Concat(runs) : null;
    }

    /// <summary>Largest thumbnail URL from a <c>{ "thumbnails": [...] }</c> object (InnerTube orders them ascending).</summary>
    public static string? LargestThumbnail(this JsonNode? thumbnailObject) =>
        thumbnailObject.Str("thumbnails", -1, "url");
}
