using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Security;

/// <summary>
/// 自定义 Bearer API 密钥认证 Handler。
/// 从 <c>Authorization: Bearer &lt;token&gt;</c> 头部取令牌，交由 <see cref="ApiKeyService.ResolveByToken"/>
/// 判定（先子密钥、后主密钥回退），本类只做 HTTP 适配与 Claims 构造。
/// </summary>
/// <remarks>
/// 判定逻辑刻意下沉到服务层：Handler 依赖 HTTP 上下文难以单测，而本项目禁用
/// <c>WebApplicationFactory</c> 覆盖连接串，必须让核心判定脱离 HTTP 才能被 xUnit 直接覆盖。
/// 方案名 <see cref="SchemeName"/>、策略名 <c>ApiKeyPolicy</c>、<c>/v1/*</c> 契约均保持不变。
/// </remarks>
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

        // 2. 交服务层判定（子密钥 → 主密钥回退）。服务缺失属部署错误，直接失败
        var apiKeyService = Context.RequestServices.GetService<ApiKeyService>();
        if (apiKeyService == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("API key service not available"));
        }

        ApiKeyIdentity? identity;
        try
        {
            identity = apiKeyService.ResolveByToken(token);
        }
        catch (Exception ex)
        {
            // 异常信息不得回显任何密钥片段
            Logger.LogDebug(ex, "API 密钥认证判定异常");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        if (identity == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        // 3. 认证通过——构造票据（保留原有 Name 声明，增补 KeyId/KeyName/AuthMethod）
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ApiKey"),
            new Claim("KeyId", identity.KeyId.ToString()),
            new Claim("KeyName", identity.KeyName),
            new Claim("AuthMethod", identity.AuthMethod),
        };
        var claimsIdentity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(claimsIdentity);
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
