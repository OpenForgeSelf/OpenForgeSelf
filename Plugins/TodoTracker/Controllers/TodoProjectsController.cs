using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TodoTracker.Controllers;

/// <summary>
/// 项目关联接口 <c>api/todos/projects</c>（PILOT-054 · FR-2）。
///
/// 项目档案的真相在宿主 <see cref="IProjectRegistry"/>（<c>ForgeSelf.Api/Services/HostProjectRegistry.cs</c>），
/// 这里只做「归一 + 匹配 + 必要时登记 + 任务计数」，<b>不提供删除宿主项目的端点</b> ——
/// 待办插件没有权力删掉别人（AIAgent/sems）登记的档案。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/todos/projects")]
public class TodoProjectsController : ControllerBase
{
    private readonly ITodoProjectService _projects;

    public TodoProjectsController(ITodoProjectService projects) => _projects = projects;

    /// <summary>项目清单（宿主档案 + 每项目任务数/未完成数），按最近活动倒序。</summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<TodoProjectDto>>> List()
    {
        try
        {
            var items = _projects.ListProjects().ToList();
            return Ok(ApiResponse<List<TodoProjectDto>>.Ok(items, "获取项目清单成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取项目清单失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<TodoProjectDto>>.Error("获取项目清单失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 归一并匹配一条路径（不写任务）。同一目录的 <c>/d/proj</c>、<c>D:\proj</c>、<c>D:/proj/</c>、
    /// <c>/mnt/d/proj</c> 必须给出<b>同一个</b> projectId；未命中且 <c>register=true</c> 才登记新档案。
    /// </summary>
    [HttpPost("resolve")]
    public ActionResult<ApiResponse<ResolveProjectResult>> Resolve([FromBody] ResolveProjectRequest request)
    {
        try
        {
            var raw = request?.Path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                return BadRequest(ApiResponse<ResolveProjectResult>.Error("path 不能为空", 400));

            var result = _projects.Resolve(raw, registerIfMissing: true);
            // 归一/目录问题算客户端错误；接缝缺席算依赖不可用（503）—— 两种成因必须分得开，
            // 否则用户会把"agent-hub 没装/宿主接缝未就绪"当成"我路径写错了"反复试。
            if (!result.Ok)
            {
                var reason = result.Error ?? "路径无法解析";
                XTrace.Log.Warn("项目路径解析失败：{0} → {1}", raw, reason);
                return result.RegistryAvailable
                    ? BadRequest(ApiResponse<ResolveProjectResult>.Error(reason, 400))
                    : StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<ResolveProjectResult>.Error(reason, 503));
            }

            return Ok(ApiResponse<ResolveProjectResult>.Ok(result, result.Registered ? "已登记并匹配到项目" : "匹配到已有项目"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("解析项目路径失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ResolveProjectResult>.Error("解析项目路径失败: " + ex.Message));
        }
    }

    /// <summary>单个项目（含任务计数）。不存在 404。</summary>
    [HttpGet("{id:int}")]
    public ActionResult<ApiResponse<TodoProjectDto>> Get(int id)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<TodoProjectDto>.Error("id 必须为正整数", 400));

            var project = _projects.Get(id);
            return project == null
                ? NotFound(ApiResponse<TodoProjectDto>.Error($"项目 {id} 不存在", 404))
                : Ok(ApiResponse<TodoProjectDto>.Ok(project, "获取项目成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取项目失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoProjectDto>.Error("获取项目失败: " + ex.Message));
        }
    }
}
