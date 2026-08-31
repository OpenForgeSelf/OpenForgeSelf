using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// sems 项目控制器：提供项目列表查询（数据源为宿主共享项目清单，由 AIAgent 选定工作目录时写入）。
/// 插件自带界面（web/）在前端直连调用。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects)
    {
        _projects = projects;
    }

    /// <summary>项目列表（按最近活动倒序）。</summary>
    [HttpGet]
    public ActionResult<object> GetAll()
    {
        var list = _projects.GetProjects();
        return Ok(new { success = true, total = list.Count, projects = list });
    }

    /// <summary>项目总数（首页统计卡）。</summary>
    [HttpGet("count")]
    public ActionResult<object> GetCount()
    {
        return Ok(new { success = true, total = _projects.Count });
    }
}