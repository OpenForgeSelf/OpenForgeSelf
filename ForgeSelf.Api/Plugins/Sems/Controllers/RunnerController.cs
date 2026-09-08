using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// sems 运行管理控制器（spec028 批2 T06）：进程启动 / 停止 / 运行列表 / 本机进程检测。
/// 全部管理端点挂 ApiKeyPolicy 鉴权。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize("ApiKeyPolicy")]
public class RunnerController : ControllerBase
{
    private readonly IRunnerService _runner;

    public RunnerController(IRunnerService runner)
    {
        _runner = runner;
    }

    /// <summary>启动命令（在项目根执行 cmd /c &lt;script&gt;）。</summary>
    [HttpPost("commands/{id}/run")]
    public ActionResult<object> Run(int id)
    {
        var session = _runner.Launch(id);
        if (session == null)
            return Conflict(new { success = false, message = "命令不存在或已有存活会话（重复启动被拒绝）" });

        return Ok(new { success = true, session });
    }

    /// <summary>停止本面板启动的会话（按 commandId）。</summary>
    [HttpPost("commands/{id}/stop")]
    public ActionResult<object> Stop(int id)
    {
        var ok = _runner.Stop(id);
        if (!ok) return NotFound(new { success = false, message = $"未找到存活的会话：{id}" });
        return Ok(new { success = true });
    }

    /// <summary>运行列表（Launched + 上次检测缓存）。</summary>
    [HttpGet("runs")]
    public ActionResult<object> ListRuns()
    {
        var runs = _runner.Current();
        return Ok(new { success = true, total = runs.Count, runs });
    }

    /// <summary>触发本机进程检测并与 Launched 合并返回。</summary>
    [HttpPost("runs/check")]
    public ActionResult<object> Check()
    {
        var runs = _runner.CheckAndMerge();
        return Ok(new { success = true, total = runs.Count, runs });
    }

    /// <summary>停止外部捕获进程（按 PID 杀整树）。</summary>
    [HttpPost("runs/{pid}/stop")]
    public ActionResult<object> StopExternal(int pid)
    {
        var ok = _runner.StopExternal(pid);
        if (!ok) return NotFound(new { success = false, message = $"无法停止进程（不存在或无权限）：{pid}" });
        return Ok(new { success = true });
    }
}
