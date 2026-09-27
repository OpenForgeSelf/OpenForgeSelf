using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Controllers;

/// <summary>
/// 自动更新管理面（spec 036 + 2026-09-27 本地目录更新源）。
/// 检查 → 下载（暂存 staged）→ 进度轮询 → 重启并更新（update-agent 换文件后拉起新进程）。
/// 更新源三 provider：stardust / github / local，由 UpdateSettingsService 运行时配置（设置页可改并落盘）。
/// </summary>
[ApiController]
[Route("api/update")]
[Authorize("ApiKeyPolicy")]
public class UpdateController : ControllerBase
{
    private readonly StagedUpdateService _updateService;
    private readonly UpdateSettingsService _settings;

    public UpdateController(
        StagedUpdateService updateService,
        UpdateSettingsService settings)
    {
        _updateService = updateService;
        _settings = settings;
    }

    /// <summary>
    /// 更新总体状态：当前版本、provider 配置、最近检查结果与暂存状态（不回显任何凭据）。
    /// </summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        var state = _updateService.GetState();
        var config = _settings.Current;
        return Ok(new
        {
            success = true,
            data = new
            {
                currentVersion = UpdateChecker.GetCurrentVersion(),
                provider = config.Provider,
                githubRepo = config.GitHubRepo,
                localDir = config.LocalDir,
                channel = config.Channel,
                state,
            },
        });
    }

    /// <summary>
    /// 读取当前更新源配置（设置页展示用；不含 ServerUrl 等可能敏感的完整字段）。
    /// </summary>
    [HttpGet("config")]
    public ActionResult<object> GetConfig()
    {
        var config = _settings.Current;
        return Ok(new
        {
            success = true,
            data = new
            {
                provider = config.Provider,
                serverUrl = config.ServerUrl,
                githubRepo = config.GitHubRepo,
                localDir = config.LocalDir,
                channel = config.Channel,
                checkIntervalMinutes = config.CheckIntervalMinutes,
                checkTimeoutSeconds = config.CheckTimeoutSeconds,
                downloadTimeoutSeconds = config.DownloadTimeoutSeconds,
            },
        });
    }

    /// <summary>更新源配置修改请求体。只更新显式传入的字段（部分更新）。</summary>
    public class UpdateConfigRequest
    {
        public string? Provider { get; set; }
        public string? LocalDir { get; set; }
    }

    /// <summary>
    /// 修改更新源配置（provider / 本地目录），先校验再落盘，立即生效。
    /// 下载/暂存/应用进行中时拒绝修改（避免打断在途更新）。
    /// </summary>
    [HttpPut("config")]
    public ActionResult<object> PutConfig([FromBody] UpdateConfigRequest request)
    {
        if (request == null)
            return BadRequest(new { success = false, error = "请求体不能为空" });

        var state = _updateService.GetState();
        if (state.Status is "downloading" or "verifying" or "extracting" or "applying")
        {
            return Ok(new { success = false, error = $"更新正在进行（{state.Status}），请等待完成后再修改配置" });
        }

        var current = _settings.Current;
        var provider = current.Provider;
        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            provider = request.Provider.Trim().ToLowerInvariant();
            if (provider is not ("stardust" or "github" or "local"))
                return Ok(new { success = false, error = $"不支持的更新源类型: {request.Provider}（支持 stardust / github / local）" });
        }

        var localDir = current.LocalDir;
        if (request.LocalDir != null)
            localDir = request.LocalDir.Trim();

        // 先校验再落盘：local 更新源必须配置存在的本地目录
        if (provider == "local")
        {
            if (string.IsNullOrWhiteSpace(localDir))
                return Ok(new { success = false, error = "本地目录更新源必须填写本地目录" });
            if (!Directory.Exists(localDir))
                return Ok(new { success = false, error = $"本地更新目录不存在: {localDir}" });
        }

        _settings.Update(config =>
        {
            config.Provider = provider;
            config.LocalDir = localDir;
        });

        return Ok(new
        {
            success = true,
            data = new
            {
                provider = current.Provider,
                localDir = current.LocalDir,
                githubRepo = current.GitHubRepo,
                channel = current.Channel,
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
