using Microsoft.AspNetCore.Http;
using System.Web;

namespace ForgeSelf.Api.Entities;

/// <summary>客户端类型（聊天记录来源指纹）。枚举便于编程使用，数据库存储其字符串名。</summary>
public enum ClientKind
{
    Unknown = 0,
    App,
    Copilot,
    GoHttp,
    Curl,
    Python,
    Other
}

/// <summary>会话来源。枚举便于编程使用，数据库存储其字符串名。</summary>
public enum SessionSource
{
    App = 0,
    Proxy
}

/// <summary>客户端类型推断辅助。</summary>
public static class ClientKindExtensions
{
    /// <summary>根据 User-Agent 推断客户端类型（代理录制场景）。</summary>
    public static ClientKind FromUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return ClientKind.Unknown;
        if (userAgent.Contains("Copilot", StringComparison.OrdinalIgnoreCase)) return ClientKind.Copilot;
        if (userAgent.Contains("Go-http", StringComparison.OrdinalIgnoreCase)) return ClientKind.GoHttp;
        if (userAgent.Contains("curl", StringComparison.OrdinalIgnoreCase)) return ClientKind.Curl;
        if (userAgent.Contains("python", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("requests", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("aiohttp", StringComparison.OrdinalIgnoreCase)) return ClientKind.Python;
        return ClientKind.Other;
    }
}
