using System;

namespace ForgeSelf.Api.Services;

/// <summary>GitHub OAuth App（设备流）配置。来源 = appsettings.json「GitHubOAuth」节。</summary>
/// <remarks>
/// 内置 client_id（PILOT-083 / O2 决策 068 相关：client_id 非机密，公开值可入库）；
/// token 属用户凭据，经 <see cref="ISecretEncryptionService"/> 加密后落 ForgeSetting，绝不入本节。
/// </remarks>
public sealed class GitHubOAuthConfig
{
    /// <summary>OAuth App 的 ClientId（设备流识别用，非机密）。</summary>
    public string ClientId { get; init; } = "";

    /// <summary>申请的授权范围（空格分隔，默认 repo workflow）。</summary>
    public string Scopes { get; init; } = "repo workflow";

    /// <summary>设备码申请端点（RFC 8628 §3.1）。</summary>
    public string DeviceCodeUrl { get; init; } = "https://github.com/login/device/code";

    /// <summary>令牌轮询端点（RFC 8628 §3.4/§3.5）。</summary>
    public string TokenUrl { get; init; } = "https://github.com/login/oauth/access_token";

    /// <summary>REST API 根（用于绑定后拉取账号 login）。</summary>
    public string ApiBase { get; init; } = "https://api.github.com";

    /// <summary>配置缺失（ClientId 为空）时是否可用；守卫测试锚定默认值。</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>从 IConfiguration「GitHubOAuth」节绑定；节缺失时得到空 ClientId（未配置态）。</summary>
    public static GitHubOAuthConfig FromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var section = configuration.GetSection("GitHubOAuth");
        return new GitHubOAuthConfig
        {
            ClientId = section["ClientId"] ?? "",
            Scopes = string.IsNullOrWhiteSpace(section["Scopes"]) ? "repo workflow" : section["Scopes"]!,
            DeviceCodeUrl = string.IsNullOrWhiteSpace(section["DeviceCodeUrl"]) ? "https://github.com/login/device/code" : section["DeviceCodeUrl"]!,
            TokenUrl = string.IsNullOrWhiteSpace(section["TokenUrl"]) ? "https://github.com/login/oauth/access_token" : section["TokenUrl"]!,
            ApiBase = string.IsNullOrWhiteSpace(section["ApiBase"]) ? "https://api.github.com" : section["ApiBase"]!,
        };
    }
}
