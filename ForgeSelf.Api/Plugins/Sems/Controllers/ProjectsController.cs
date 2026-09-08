using System.Collections.Generic;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// sems 项目控制器（spec028 批2 增强）：
/// - 项目列表查询（GET api/projects，含每项目命令概要）
/// - 档案编辑（PUT api/projects/{id}）
/// 命令相关端点（CRUD + 启停）见 <see cref="ProjectCommandsController"/>。
/// 数据源为宿主级 <see cref="IProjectRegistry"/> 接缝（L1 契约），经 ctx.Get 每次解析。
/// 全部管理端点挂 ApiKeyPolicy 鉴权（count 公开供首页统计）。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize("ApiKeyPolicy")]
public class ProjectsController : ControllerBase
{
    private readonly IContext _ctx;

    public ProjectsController(IContext ctx)
    {
        _ctx = ctx;
    }

    private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>();

    /// <summary>项目列表（按最近活动倒序，含每项目命令概要）。</summary>
    [HttpGet]
    public ActionResult<object> GetAll()
    {
        var list = Registry?.GetAll() ?? new List<ProjectInfo>();
        return Ok(new { success = true, total = list.Count, projects = list });
    }

    /// <summary>项目总数（首页统计卡，公开）。</summary>
    [HttpGet("count")]
    [AllowAnonymous]
    public ActionResult<object> GetCount()
    {
        var list = Registry?.GetAll() ?? new List<ProjectInfo>();
        return Ok(new { success = true, total = list.Count });
    }

    /// <summary>档案编辑（Name/Type/Description/Tags）。</summary>
    [HttpPut("{id}")]
    public ActionResult<object> Update(int id, [FromBody] ProjectUpdate update)
    {
        var registry = Registry;
        if (registry == null) return StatusCode(503, new { success = false, message = "项目注册表不可用" });

        if (!registry.Update(id, update))
            return NotFound(new { success = false, message = $"项目不存在：{id}" });

        return Ok(new { success = true });
    }
}
