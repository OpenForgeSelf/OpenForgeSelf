using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Controllers;

/// <summary>
/// GitHub 账号绑定面（<c>api/github-account</c>，PILOT-083）。
/// 全部端点需 <c>ApiKeyPolicy</c> 鉴权；响应外壳沿用 <c>{ success, data | error }</c>。
/// 令牌永不出现在任何响应体/日志：状态接口只回账号 login 与时间。
/// </summary>
[ApiController]
[Route("api/github-account")]
[Authorize("ApiKeyPolicy")]
public class GitHubAccountController : ControllerBase
{
    private readonly GitHubAccountService _service;

    public GitHubAccountController(GitHubAccountService service)
    {
        _service = service;
    }

    /// <summary>绑定状态（不含令牌）。</summary>
    [HttpGet("status")]
    public ActionResult<object> Status()
    {
        return Ok(new { success = true, data = _service.GetStatus() });
    }

    /// <summary>发起绑定：申请设备码。返回 userCode 与预填后的验证页地址（前端倒计时自动打开）。</summary>
    [HttpPost("bind/start")]
    public async Task<ActionResult<object>> Start(CancellationToken cancellationToken)
    {
        try
        {
            var start = await _service.StartAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                data = new
                {
                    deviceCode = start.DeviceCode,
                    userCode = start.UserCode,
                    verificationUri = start.VerificationUri,
                    verificationUriPrefilled = start.VerificationUriPrefilled,
                    expiresIn = start.ExpiresIn,
                    interval = start.Interval,
                },
            });
        }
        catch (InvalidOperationException ex)
        {
            // 上游原因已由 DescribeStartFailure 透传（≤200 字符）
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>轮询一次绑定结果。Pending/SlowDown 为协议内等待态（HTTP 200，不抛错）。</summary>
    [HttpPost("bind/poll")]
    public async Task<ActionResult<object>> Poll([FromBody] PollRequest request, CancellationToken cancellationToken)
    {
        if (request?.DeviceCode == null || request.DeviceCode.Length == 0)
            return Ok(new { success = false, error = "缺少 deviceCode" });

        var outcome = await _service.PollAsync(new DeviceCodeStartResult { DeviceCode = request.DeviceCode }, cancellationToken);
        return Ok(new
        {
            success = true,
            data = new
            {
                status = outcome.Status.ToString().ToLowerInvariant(),
                accountLogin = outcome.AccountLogin,
                error = outcome.Error,
            },
        });
    }

    /// <summary>解绑：清空账号与令牌。</summary>
    [HttpDelete("bind")]
    public ActionResult<object> Unbind()
    {
        _service.Unbind();
        return Ok(new { success = true, data = _service.GetStatus() });
    }

    /// <summary>绑定态开关：插件 GitHub 拉取是否自动附凭据。</summary>
    [HttpPut("auto-attach")]
    public ActionResult<object> SetAutoAttach([FromBody] AutoAttachRequest request)
    {
        _service.SetAutoAttach(request?.Enabled ?? true);
        return Ok(new { success = true, data = _service.GetStatus() });
    }

    /// <summary>轮询请求体。</summary>
    public sealed class PollRequest
    {
        /// <summary>申请设备码时返回的 device_code。</summary>
        public string DeviceCode { get; set; } = "";
    }

    /// <summary>自动附凭据开关请求体。</summary>
    public sealed class AutoAttachRequest
    {
        /// <summary>是否自动附凭据。</summary>
        public bool Enabled { get; set; } = true;
    }
}
