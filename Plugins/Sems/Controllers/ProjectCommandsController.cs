using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// sems 运行命令控制器（spec028 批2 T04/T06）：
/// - 命令 CRUD（GET/POST api/projects/{id}/commands、PUT/DELETE api/commands/{id}）
/// - 命令启停（POST api/commands/{id}/run、POST api/commands/{id}/stop）
/// 路由同时映射 api/projects/{id}/commands 与 api/commands/{id}，贴合 design §5.4。
/// 全部端点挂 ApiKeyPolicy 鉴权。
/// </summary>
[ApiController]
[Route("api/projects/{projectId}/commands")]
[Route("api/commands")]
[Authorize("ApiKeyPolicy")]
public class ProjectCommandsController : ControllerBase
{
    private readonly IContext _ctx;
    private readonly IRunnerService _runner;

    public ProjectCommandsController(IContext ctx, IRunnerService runner)
    {
        _ctx = ctx;
        _runner = runner;
    }

    private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>();

    /// <summary>某项目的运行命令列表（按 Sort 升序）。路由：GET api/projects/{projectId}/commands</summary>
    [HttpGet]
    public ActionResult<object> GetCommands(int projectId)
    {
        var registry = Registry;
        if (registry == null) return StatusCode(503, new { success = false, message = "项目注册表不可用" });

        var project = registry.Get(projectId);
        if (project == null) return NotFound(new { success = false, message = $"项目不存在：{projectId}" });

        return Ok(new { success = true, commands = project.Commands });
    }

    /// <summary>新增运行命令。路由：POST api/projects/{projectId}/commands</summary>
    [HttpPost]
    public ActionResult<object> AddCommand(int projectId, [FromBody] RunCommandInfo command)
    {
        var registry = Registry;
        if (registry == null) return StatusCode(503, new { success = false, message = "项目注册表不可用" });

        if (registry.Get(projectId) == null)
            return NotFound(new { success = false, message = $"项目不存在：{projectId}" });

        if (command == null || string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Script))
            return BadRequest(new { success = false, message = "命令名称与脚本均不能为空" });

        var newId = registry.AddCommand(projectId, command);
        if (newId <= 0)
            return BadRequest(new { success = false, message = "新增命令失败" });

        return Ok(new { success = true, id = newId });
    }

    /// <summary>编辑运行命令。路由：PUT api/commands/{id}</summary>
    [HttpPut("{id}")]
    public ActionResult<object> UpdateCommand(int id, [FromBody] RunCommandUpdate update)
    {
        var registry = Registry;
        if (registry == null) return StatusCode(503, new { success = false, message = "项目注册表不可用" });

        if (!registry.UpdateCommand(id, update))
            return NotFound(new { success = false, message = $"命令不存在：{id}" });

        return Ok(new { success = true });
    }

    /// <summary>删除运行命令。路由：DELETE api/commands/{id}</summary>
    [HttpDelete("{id}")]
    public ActionResult<object> DeleteCommand(int id)
    {
        var registry = Registry;
        if (registry == null) return StatusCode(503, new { success = false, message = "项目注册表不可用" });

        if (!registry.DeleteCommand(id))
            return NotFound(new { success = false, message = $"命令不存在：{id}" });

        return Ok(new { success = true });
    }

    /// <summary>启动命令（在项目根执行 cmd /c &lt;script&gt;）。路由：POST api/commands/{id}/run</summary>
    [HttpPost("{id}/run")]
    public ActionResult<object> Run(int id)
    {
        var session = _runner.Launch(id);
        if (session == null)
            return Conflict(new { success = false, message = "命令不存在或已有存活会话（重复启动被拒绝）" });

        return Ok(new { success = true, session });
    }

    /// <summary>停止本面板启动的会话（按 commandId）。路由：POST api/commands/{id}/stop</summary>
    [HttpPost("{id}/stop")]
    public ActionResult<object> Stop(int id)
    {
        var ok = _runner.Stop(id);
        if (!ok) return NotFound(new { success = false, message = $"未找到存活的会话：{id}" });
        return Ok(new { success = true });
    }
}
