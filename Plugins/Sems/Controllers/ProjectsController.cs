using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// sems 项目控制器：项目列表 / 计数 / 档案编辑 / **插件内部登记** / **移除** / 目录浏览。
/// 数据源为宿主级 <see cref="IProjectRegistry"/> 接缝（L1 契约），但本控制器**不直连接缝**——
/// 一律经 <see cref="IProjectService"/>（sems 业务唯一落点），与对外 MCP 工具共用同一实现。
/// 全部管理端点挂 ApiKeyPolicy 鉴权（count 公开供首页统计）。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize("ApiKeyPolicy")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects)
    {
        _projects = projects;
    }

    /// <summary>项目列表（按最近活动倒序，含每项目命令概要）。</summary>
    [HttpGet]
    public ActionResult<object> GetAll()
    {
        var list = _projects.GetProjects();
        return Ok(new { success = true, total = list.Count, projects = list });
    }

    /// <summary>项目总数（首页统计卡，公开）。</summary>
    [HttpGet("count")]
    [AllowAnonymous]
    public ActionResult<object> GetCount()
    {
        return Ok(new { success = true, total = _projects.Count });
    }

    /// <summary>
    /// 在 sems 内部登记一个项目（来源 manual）：给定已存在的目录绝对路径即可，
    /// 不依赖任何其他插件的动作。Root 已存在时只刷新活跃时间，不覆写用户已编辑的档案。
    /// </summary>
    [HttpPost]
    public ActionResult<object> Register([FromBody] ProjectRegisterRequest request)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "请求体不能为空" });

        var result = _projects.Register(request.Root, request.Name);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        return Ok(new { success = true, project = result.Data });
    }

    /// <summary>档案编辑（Name/Type/Description/Tags）。</summary>
    [HttpPut("{id:int}")]
    public ActionResult<object> Update(int id, [FromBody] ProjectUpdate update)
    {
        var result = _projects.UpdateProject(id, update);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>
    /// 移除项目档案（级联删除其运行命令）。<b>只删档案，不触碰磁盘目录与文件</b>；
    /// 项目仍有存活运行会话时返回 409 且不做任何数据变更。
    /// </summary>
    [HttpDelete("{id:int}")]
    public ActionResult<object> Remove(int id)
    {
        var result = _projects.RemoveProject(id);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>
    /// 目录浏览（供「添加项目」选目录）：省略 path 列举本机驱动器，否则列该目录的直接子目录。
    /// 只读、不列文件、不递归。
    /// </summary>
    [HttpGet("browse")]
    public ActionResult<object> Browse([FromQuery] string? path)
    {
        var result = _projects.Browse(path);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Error });

        var listing = result.Data!;
        return Ok(new
        {
            success = true,
            path = listing.Path,
            parent = listing.Parent,
            directories = listing.Directories
        });
    }
}

/// <summary>项目登记请求（sems 内部添加项目）。</summary>
public class ProjectRegisterRequest
{
    /// <summary>项目根目录绝对路径（必须已存在）。</summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>显示名（可选；留空取目录名）。</summary>
    public string? Name { get; set; }
}
