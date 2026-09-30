using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// sems 运行列表控制器：运行列表查询 / 本机进程检测 / 外部进程停止（路由 api/runs，design §5.4）。
/// 全部管理端点挂 ApiKeyPolicy 鉴权。
/// 进程启停的唯一入口即本控制器 + <see cref="ProjectCommandsController"/> 的 run/stop；
/// 历史上的冗余副本 <c>RunnerController</c>（api/runner/*，前端与测试零引用）已收敛移除
/// （028-project-workspace 已知问题 #1，2026-09-28）。
/// </summary>
[ApiController]
[Route("api/runs")]
[Authorize("ApiKeyPolicy")]
public class RunsController : ControllerBase
{
    private readonly IRunnerService _runner;

    public RunsController(IRunnerService runner)
    {
        _runner = runner;
    }

    /// <summary>运行列表（Launched + 上次检测缓存）。</summary>
    [HttpGet]
    public ActionResult<object> ListRuns()
    {
        var runs = _runner.Current();
        return Ok(new { success = true, total = runs.Count, runs });
    }

    /// <summary>触发本机进程检测并与 Launched 合并返回。</summary>
    [HttpPost("check")]
    public ActionResult<object> Check()
    {
        var runs = _runner.CheckAndMerge();
        return Ok(new { success = true, total = runs.Count, runs });
    }

    /// <summary>停止外部捕获进程（按 PID 杀整树）。</summary>
    [HttpPost("{pid}/stop")]
    public ActionResult<object> StopExternal(int pid)
    {
        var ok = _runner.StopExternal(pid);
        if (!ok) return NotFound(new { success = false, message = $"无法停止进程（不存在或无权限）：{pid}" });
        return Ok(new { success = true });
    }
}
