using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Security;

/// <summary>
/// 自定义 Bearer API 密钥认证 Handler。
/// 从 Authorization: Bearer <token> 头部取令牌，与 ForgeSetting 中加密密钥解密后定长比较。
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    /// <summary>
    /// 方案名，供 Program.cs 注册时引用（AddScheme<ApiKeyAuthenticationHandler>("BearerApiKey", ...)）
    /// </summary>
    public const string SchemeName = "BearerApiKey";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. 取 Authorization 头部
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or invalid Authorization header"));
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Bearer token is empty"));
        }

        // 2. 从 ForgeSetting 取密文
        var cipher = ForgeSetting.Current.ApiToken;
        if (string.IsNullOrEmpty(cipher))
        {
            return Task.FromResult(AuthenticateResult.Fail("No API key configured"));
        }

        // 3. 解密后定长比较
        var encryption = Context.RequestServices.GetService<ISecretEncryptionService>();
        if (encryption == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Encryption service not available"));
        }

        string? plainKey = null;
        try
        {
            plainKey = encryption.Decrypt(cipher);
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Failed to decrypt API key"));
        }

        if (string.IsNullOrEmpty(plainKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Decrypted API key is empty"));
        }

        // 定长比较（防止时序侧信道，虽本地场景风险可忽略）
        if (!string.Equals(token, plainKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        // 4. 认证通过——构造票据
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ApiKey"),
            new Claim("AuthMethod", "BearerApiKey"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// 自定义 Bearer API 密钥认证选项
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}
