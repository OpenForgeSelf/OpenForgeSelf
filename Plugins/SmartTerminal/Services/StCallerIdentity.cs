using Microsoft.AspNetCore.Http;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// 服务端写入 source：客户端不能把 agent 伪造成 ui。
/// 识别头：X-ForgeSelf-Source / X-St-Source / X-Agent-Id。
/// </summary>
public static class StCallerIdentity
{
    public const string Ui = "ui";
    public const string Agent = "agent";

    public static string Resolve(HttpContext? http)
    {
        if (http == null) return Ui;
        var headers = http.Request.Headers;
        if (headers.TryGetValue("X-Agent-Id", out var agentId) && !string.IsNullOrWhiteSpace(agentId))
            return Agent;
        if (TrySource(headers, "X-ForgeSelf-Source") == Agent) return Agent;
        if (TrySource(headers, "X-St-Source") == Agent) return Agent;
        if (TrySource(headers, "X-Lab-Source") == Agent) return Agent;
        return Ui;
    }

    public static string? AgentId(HttpContext? http)
    {
        if (http?.Request.Headers.TryGetValue("X-Agent-Id", out var id) == true)
        {
            var value = id.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        return null;
    }

    private static string? TrySource(IHeaderDictionary headers, string name)
    {
        if (!headers.TryGetValue(name, out var raw)) return null;
        var value = raw.ToString().Trim();
        if (value.Equals(Agent, StringComparison.OrdinalIgnoreCase)) return Agent;
        return null;
    }
}
