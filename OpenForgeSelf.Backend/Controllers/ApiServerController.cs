using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenForgeSelf.Backend.Models;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Controllers;

/// <summary>
/// API 服务器管理面（管理面端点，无鉴权——独立于 /v1/*）。
/// 提供密钥状态查询、轮换等内部管理能力。
/// </summary>
[ApiController]
[Route("api/api-server")]
public class ApiServerController : ControllerBase
{
    private readonly ApiServerKeyService _keyService;
    private readonly IConfiguration _configuration;

    public ApiServerController(ApiServerKeyService keyService, IConfiguration configuration)
    {
        _keyService = keyService;
        _configuration = configuration;
    }

    /// <summary>
    /// 获取 API 服务器配置状态。
    /// 返回 API 地址、明文密钥、授权标头示例、是否已配置密钥。
    /// </summary>
    [HttpGet("status")]
    [Authorize("ApiKeyPolicy")]
    public ActionResult<object> GetStatus()
    {
        var plainKey = _keyService.GetActiveKeyPlain();
        var hasKey = !string.IsNullOrEmpty(plainKey);

        // API 基址：优先取配置 ApiServer:PublicBaseUrl（支持反向代理/HTTPS），否则根据当前请求构造
        var publicBaseUrl = _configuration["ApiServer:PublicBaseUrl"];
        var apiBaseUrl = string.IsNullOrWhiteSpace(publicBaseUrl)
            ? $"{Request.Scheme}://{Request.Host}/v1"
            : $"{publicBaseUrl.TrimEnd('/')}/v1";

        var authHeader = hasKey ? $"Authorization: Bearer {plainKey}" : "";

        return Ok(new
        {
            success = true,
            data = new
            {
                apiBaseUrl,
                apiKeyMasked = hasKey ? _keyService.GetActiveKeyMasked() : "",
                apiKeyPlain = hasKey ? plainKey : "",
                authHeader,
                hasKey,
            },
        });
    }

    /// <summary>
    /// 获取初始 API 密钥（无认证，仅首次调用有效）。
    /// 通过 ForgeSetting.IsFirstInit 判断是否为首次，是则返回 token 并修改状态，否则 403。
    /// 从 ForgeSetting 读密文后解密返回明文。
    /// </summary>
    [HttpGet("init-token")]
    public ActionResult<object> GetInitToken()
    {
        var setting = Models.ForgeSetting.Current;
        if (!setting.IsFirstInit)
            return StatusCode(403, new { success = false, error = "首次初始化已完成，请使用 status 接口获取密钥" });

        var plainKey = _keyService.GetActiveKeyPlain();
        if (string.IsNullOrEmpty(plainKey))
            return Ok(new { success = false, data = (object?)null });

        // 标记为已初始化
        setting.IsFirstInit = false;
        setting.Save();

        return Ok(new
        {
            success = true,
            data = new
            {
                apiKeyPlain = plainKey,
                authHeader = $"Authorization: Bearer {plainKey}",
            },
        });
    }

    /// <summary>
    /// 重新生成 API 密钥。旧密钥立即失效。
    /// </summary>
    [HttpPost("regenerate")]
    [Authorize("ApiKeyPolicy")]
    public ActionResult<object> Regenerate()
    {
        try
        {
            var result = _keyService.Regenerate();

            // 重新组装响应
            var publicBaseUrl = _configuration["ApiServer:PublicBaseUrl"];
            var apiBaseUrl = string.IsNullOrWhiteSpace(publicBaseUrl)
                ? $"{Request.Scheme}://{Request.Host}/v1"
                : $"{publicBaseUrl.TrimEnd('/')}/v1";

            return Ok(new
            {
                success = true,
                data = new
                {
                    apiBaseUrl,
                    apiKeyMasked = result.MaskedKey,
                    apiKeyPlain = result.PlainKey,
                    authHeader = result.AuthHeader,
                    hasKey = true,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"密钥重新生成失败: {ex.Message}" });
        }
    }
}