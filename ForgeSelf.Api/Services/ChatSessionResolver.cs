using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ForgeSelf.Api.Entities;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 会话键解析：把不同入口（自定义头 / 上游透传头 / body 字段 / 兜底）统一收敛为一个稳定会话键。
/// 这是「同一会话的多轮请求归并为一个会话」的核心。
/// </summary>
public static class ChatSessionResolver
{
    /// <summary>截断字符串到指定长度（用于标题/预览）。</summary>
    public static string? Truncate(string? s, int n)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= n ? s : s.Substring(0, n));

    /// <summary>
    /// 解析稳定会话键，按优先级取第一个非空：
    /// 1. 头 X-Session-Id / X-Conversation-Id（自定义/标准客户端主动传）
    /// 2. 头 x-interaction-id（Copilot 会话键，已验证可用）
    /// 3. 头 x-conversation-id（Anthropic/Responses 风格兼容）
    /// 4. body 字段 conversationId / sessionId（今后 app 直接打代理时）
    /// 5. 兜底 Guid（仅无会话标识的裸客户端，自成单轮会话）
    /// </summary>
    public static string ResolveConversationKey(HttpRequest request, object? body)
    {
        var h = request.Headers;

        // 1. 自定义/标准头
        var v = h["X-Session-Id"].FirstOrDefault() ?? h["X-Conversation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(v)) return v!;

        // 2. Copilot 透传会话键
        v = h["x-interaction-id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(v)) return v!;

        // 3. Anthropic/Responses 风格
        v = h["x-conversation-id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(v)) return v!;

        // 4. body 字段
        if (body != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
                if (doc.RootElement.TryGetProperty("conversationId", out var c) && c.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(c.GetString()))
                    return c.GetString()!;
                if (doc.RootElement.TryGetProperty("sessionId", out var s) && s.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(s.GetString()))
                    return s.GetString()!;
            }
            catch
            {
                // 忽略解析异常，走兜底
            }
        }

        // 5. 兜底
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>从请求推断客户端类型（UA 指纹）。</summary>
    public static ClientKind DeriveClientKind(HttpRequest request)
        => ClientKindExtensions.FromUserAgent(request.Headers["User-Agent"].FirstOrDefault());

    /// <summary>从请求体消息列表提取首条用户消息文本（用于会话标题/预览）。</summary>
    public static string? ExtractFirstUserMessage(object? body)
    {
        if (body == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
            if (!doc.RootElement.TryGetProperty("messages", out var msgs) || msgs.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var m in msgs.EnumerateArray())
            {
                if (m.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String
                    && role.GetString()?.Equals("user", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (m.TryGetProperty("content", out var content))
                    {
                        if (content.ValueKind == JsonValueKind.String) return content.GetString();
                        if (content.ValueKind == JsonValueKind.Array)
                        {
                            var parts = new List<string>();
                            foreach (var part in content.EnumerateArray())
                            {
                                if (part.TryGetProperty("type", out var t) && t.GetString() == "text"
                                    && part.TryGetProperty("text", out var txt))
                                    parts.Add(txt.GetString() ?? string.Empty);
                            }
                            if (parts.Count > 0) return string.Join("\n", parts);
                        }
                    }
                }
            }
        }
        catch
        {
            // 忽略
        }
        return null;
    }
}
