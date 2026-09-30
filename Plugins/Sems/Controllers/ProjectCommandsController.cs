using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// sems 运行命令控制器：命令 CRUD（GET/POST api/projects/{id}/commands、PUT/DELETE api/commands/{id}）
/// 与命令启停（POST api/commands/{id}/run|stop）。
/// 路由同时映射 api/projects/{id}/commands 与 api/commands/{id}，贴合 design §5.4。
/// 业务一律经 <see cref="IProjectService"/>（校验/落库）与 <see cref="IRunnerService"/>（进程启停），
/// 与对外 MCP 工具共用同一实现；本控制器不直连宿主接缝。全部端点挂 ApiKeyPolicy 鉴权。
/// </summary>
[ApiController]
[Route("api/projects/{projectId}/commands")]
[Route("api/commands")]
[Authorize("ApiKeyPolicy")]
public class ProjectCommandsController : ControllerBase
{
    private readonly IProjectService _projects;
    private readonly IRunnerService _runner;

    public ProjectCommandsController(IProjectService projects, IRunnerService runner)
    {
        _projects = projects;
        _runner = runner;
    }

    /// <summary>某项目的运行命令列表（按 Sort 升序）。路由：GET api/projects/{projectId}/commands</summary>
    [HttpGet]
    public ActionResult<object> GetCommands(int projectId)
    {
        var result = _projects.GetCommands(projectId);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        return Ok(new { success = true, commands = result.Data });
    }

    /// <summary>新增运行命令。路由：POST api/projects/{projectId}/commands</summary>
    [HttpPost]
    public ActionResult<object> AddCommand(int projectId, [FromBody] RunCommandInfo command)
    {
        var result = _projects.AddCommand(projectId, command);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        return Ok(new { success = true, id = result.Data });
    }

    /// <summary>编辑运行命令。路由：PUT api/commands/{id}</summary>
    [HttpPut("{id:int}")]
    public ActionResult<object> UpdateCommand(int id, [FromBody] RunCommandUpdate update)
    {
        var result = _projects.UpdateCommand(id, update);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>删除运行命令。路由：DELETE api/commands/{id}</summary>
    [HttpDelete("{id:int}")]
    public ActionResult<object> DeleteCommand(int id)
    {
        var result = _projects.DeleteCommand(id);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>启动命令（在项目根执行 cmd /c &lt;script&gt;）。路由：POST api/commands/{id}/run</summary>
    [HttpPost("{id:int}/run")]
    public ActionResult<object> Run(int id)
    {
        var session = _runner.Launch(id);
        if (session == null)
            return Conflict(new { success = false, message = "命令不存在或已有存活会话（重复启动被拒绝）" });

        return Ok(new { success = true, session });
    }

    /// <summary>停止本面板启动的会话（按 commandId）。路由：POST api/commands/{id}/stop</summary>
    [HttpPost("{id:int}/stop")]
    public ActionResult<object> Stop(int id)
    {
        var ok = _runner.Stop(id);
        if (!ok) return NotFound(new { success = false, message = $"未找到存活的会话：{id}" });
        return Ok(new { success = true });
    }
}
