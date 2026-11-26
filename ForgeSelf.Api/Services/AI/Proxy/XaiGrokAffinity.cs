using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// xAI Grok：粘滞到同一台缓存机。判定用 Endpoint / 提供方名 / 上游模型 id，不看上一轮响应。
/// </summary>
public sealed class XaiGrokAffinity : ILlmUpstreamAffinity
{
    public const string ConvIdHeader = "x-grok-conv-id";
    public const string CacheKeyField = "prompt_cache_key";

    public bool Matches(LlmAffinityContext ctx)
        => IsXaiHost(ctx.Endpoint)
           || IsXaiName(ctx.ProviderName)
           || ctx.UpstreamModelId.StartsWith("grok-", StringComparison.OrdinalIgnoreCase);

    public void Apply(LlmAffinityContext ctx)
    {
        var hasConvId = ctx.TryInbound(ConvIdHeader, out var convId);
        if (hasConvId)
            ctx.OutboundHeaders[ConvIdHeader] = convId;

        if (HasCacheKey(ctx.Body))
            return;

        // xAI：两个都设时 prompt_cache_key 优先。客户端已钉 conv-id 就不要再写 key 把它盖掉。
        if (hasConvId)
            return;

        ctx.Body[CacheKeyField] = HashStaticPrefix(ctx);
    }

    private static bool HasCacheKey(JsonObject body)
        => body[CacheKeyField] is JsonValue value
           && value.TryGetValue<string>(out var key)
           && !string.IsNullOrWhiteSpace(key);

    internal static bool IsXaiHost(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return false;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && !Uri.TryCreate("https://" + endpoint.Trim(), UriKind.Absolute, out uri))
            return endpoint.Contains("x.ai", StringComparison.OrdinalIgnoreCase);

        var host = uri.Host;
        return host.Equals("x.ai", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".x.ai", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsXaiName(string name)
        => name.Equals("grok", StringComparison.OrdinalIgnoreCase)
           || name.Equals("xai", StringComparison.OrdinalIgnoreCase)
           || name.Equals("x-ai", StringComparison.OrdinalIgnoreCase);

    internal static string HashStaticPrefix(LlmAffinityContext ctx)
    {
        var payload = LlmJson.Write(new JsonObject
        {
            ["instructions"] = LlmJson.Str(ctx.Body["instructions"]),
            ["system"] = CollectSystem(ctx),
            ["tools"] = ctx.Body["tools"]?.DeepClone()
        });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static JsonNode? CollectSystem(LlmAffinityContext ctx)
    {
        if (ctx.Body["system"] is { } explicitSystem)
            return explicitSystem.DeepClone();

        if (ctx.Body["messages"] is not JsonArray messages)
            return null;

        var parts = new JsonArray();
        foreach (var item in messages)
        {
            if (item is not JsonObject msg)
                continue;
            if (!string.Equals(LlmJson.Str(msg["role"]), "system", StringComparison.OrdinalIgnoreCase))
                continue;
            parts.Add(msg["content"]?.DeepClone() ?? "");
        }

        return parts.Count == 0 ? null : parts;
    }
}
