using System;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Services;

/// <summary>GitHub 账号绑定存取：绑定状态（账号、令牌密文、时间）持久化在 ForgeSetting.config，不落库。</summary>
/// <remarks>
/// 令牌明文只在内存流转；<see cref="SaveTokenAsync"/> 写入前必须经 <see cref="ISecretEncryptionService"/>
/// 加密（与 ProxyPasswordCipher 同一红线）。读取令牌明文的唯一入口 <see cref="GetTokenAsync"/> 仅供
/// 出站调用（插件 GitHub 拉取 / git extraheader / REST Bearer）使用，禁止写日志或回响应。
/// </remarks>
public sealed class GitHubBindingStore
{
    private readonly ISecretEncryptionService _encryption;

    public GitHubBindingStore(ISecretEncryptionService encryption)
    {
        _encryption = encryption;
    }

    /// <summary>绑定状态只读投影（不含任何令牌明文/密文）。</summary>
    public GitHubBindingDto Get()
    {
        var s = ForgeSetting.Current;
        return new GitHubBindingDto
        {
            Bound = !string.IsNullOrWhiteSpace(s.GitHubAccountLogin) && !string.IsNullOrWhiteSpace(s.GitHubTokenCipher),
            AccountLogin = s.GitHubAccountLogin ?? "",
            Scopes = s.GitHubScopes ?? "",
            BoundAt = s.GitHubBoundAt,
            AutoAttachToken = s.GitHubAutoAttachToken,
        };
    }

    /// <summary>保存绑定：令牌加密落盘 + 账号信息回填。任一参数为空即拒绝。</summary>
    public void SaveToken(string accessToken, string accountLogin, string scopes)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("accessToken 为空", nameof(accessToken));
        if (string.IsNullOrWhiteSpace(accountLogin)) throw new ArgumentException("accountLogin 为空", nameof(accountLogin));

        var s = ForgeSetting.Current;
        s.GitHubTokenCipher = _encryption.Encrypt(accessToken);
        s.GitHubAccountLogin = accountLogin.Trim();
        s.GitHubScopes = scopes ?? "";
        s.GitHubBoundAt = DateTime.Now;
        s.Save();
    }

    /// <summary>读取令牌明文（仅出站调用用）；未绑定返回 null。</summary>
    public string? GetTokenAsync()
    {
        var cipher = ForgeSetting.Current.GitHubTokenCipher;
        if (string.IsNullOrWhiteSpace(cipher)) return null;
        try
        {
            return _encryption.Decrypt(cipher);
        }
        catch
        {
            // 密文损坏（换机/手改配置）时按未绑定处理，绝不让出站调用携带坏凭据
            return null;
        }
    }

    /// <summary>解绑：清空账号与令牌字段并落盘。</summary>
    public void Unbind()
    {
        var s = ForgeSetting.Current;
        s.GitHubTokenCipher = "";
        s.GitHubAccountLogin = "";
        s.GitHubScopes = "";
        s.GitHubBoundAt = DateTime.MinValue;
        s.Save();
    }

    /// <summary>绑定态开关（插件 GitHub 拉取是否自动附凭据）。</summary>
    public void SetAutoAttach(bool enabled)
    {
        var s = ForgeSetting.Current;
        s.GitHubAutoAttachToken = enabled;
        s.Save();
    }
}

/// <summary>绑定状态 DTO（对外投影，永不含令牌）。</summary>
public sealed class GitHubBindingDto
{
    /// <summary>是否已绑定。</summary>
    public bool Bound { get; set; }

    /// <summary>绑定账号 login（如 octocat）。</summary>
    public string AccountLogin { get; set; } = "";

    /// <summary>授权范围。</summary>
    public string Scopes { get; set; } = "";

    /// <summary>绑定时间；MinValue = 从未绑定。</summary>
    public DateTime BoundAt { get; set; }

    /// <summary>插件 GitHub 拉取是否自动附凭据。</summary>
    public bool AutoAttachToken { get; set; } = true;
}
