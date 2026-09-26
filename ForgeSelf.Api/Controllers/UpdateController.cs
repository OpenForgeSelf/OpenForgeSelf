using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Controllers;

/// <summary>
/// 自动更新管理面（spec 036）。
/// 检查 → 下载（暂存 staged）→ 进度轮询 → 重启并更新（update-agent 换文件后拉起新进程）。
/// 支持 stardust 与 GitHub Releases 双 provider（由 Update:Provider 配置切换）。
/// </summary>
[ApiController]
[Route("api/update")]
[Authorize("ApiKeyPolicy")]
public class UpdateController : ControllerBase
{
    private readonly StagedUpdateService _updateService;
    private readonly UpdateConfig _updateConfig;

    public UpdateController(
        StagedUpdateService updateService,
        IOptions<UpdateConfig> updateConfig)
    {
        _updateService = updateService;
        _updateConfig = updateConfig.Value;
    }

    /// <summary>
    /// 更新总体状态：当前版本、provider 配置（不含 token）、最近检查结果与暂存状态。
    /// </summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        var state = _updateService.GetState();
        return Ok(new
        {
            success = true,
            data = new
            {
                currentVersion = UpdateChecker.GetCurrentVersion(),
                provider = _updateConfig.Provider,
                githubRepo = _updateConfig.GitHubRepo,
                channel = _updateConfig.Channel,
                // token 是否已配置（仅回布尔，不回显内容）
                githubTokenConfigured = !string.IsNullOrEmpty(_updateConfig.GitHubToken)
                    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FORGESELF_UPDATE_TOKEN")),
                state,
            },
        });
    }

    /// <summary>检查更新（同步等待结果，超时由配置 CheckTimeoutSeconds 控制）。</summary>
    [HttpPost("check")]
    public async Task<ActionResult<object>> Check()
    {
        var result = await _updateService.CheckAsync();
        return Ok(new { success = result.IsSuccess, data = result });
    }

    /// <summary>启动后台下载+暂存任务（幂等：已在下载/已就绪时直接返回当前状态）。</summary>
    [HttpPost("download")]
    public ActionResult<object> Download()
    {
        var state = _updateService.GetState();
        if (state.Check == null || !state.Check.HasUpdate)
        {
            return Ok(new { success = false, data = state, error = "没有可用更新，请先检查更新" });
        }

        _updateService.StartDownload();
        return Ok(new { success = true, data = _updateService.GetState() });
    }

    /// <summary>轮询下载/暂存进度。</summary>
    [HttpGet("progress")]
    public ActionResult<object> Progress()
    {
        return Ok(new { success = true, data = _updateService.GetState() });
    }

    /// <summary>
    /// 应用已暂存的更新：拉起更新代理后宿主自动退出并被代理重启。
    /// 仅在 ready 状态可用；返回后进程即将终止（前端应轮询恢复）。
    /// </summary>
    [HttpPost("apply")]
    public ActionResult<object> Apply()
    {
        var state = _updateService.GetState();
        if (state.Status != "ready")
        {
            return Ok(new { success = false, data = state, error = "更新尚未就绪（请先下载完成）" });
        }

        var started = _updateService.ApplyStaged();
        return Ok(new
        {
            success = started,
            data = _updateService.GetState(),
            error = started ? null : "启动更新代理失败",
        });
    }
}
