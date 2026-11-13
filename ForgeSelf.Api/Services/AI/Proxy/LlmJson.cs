using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ForgeSelf.Api.Services.AI.Proxy;

internal static class LlmJson
{
    public static readonly JsonSerializerOptions Relaxed = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Write(JsonNode node) => node.ToJsonString(Relaxed);

    public static string? Str(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        return value.TryGetValue<string>(out var text) ? text : null;
    }

    public static bool Bool(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    public static int? Int(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<int>(out var i))
            return i;
        if (value.TryGetValue<long>(out var l))
            return checked((int)l);
        if (value.TryGetValue<double>(out var d))
            return (int)d;
        return null;
    }
}
