using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TodoTracker.Controllers;

/// <summary>
/// 工件下发接口（PILOT-054 · FR-3）：<c>api/todos/artifact-sets</c> 与 <c>api/todos/{id}/artifacts/import</c>。
/// 把项目里 <c>docs/ai/pilot/&lt;task-id&gt;/</c> 的九件套核心文件组装成任务正文，
/// 让人不必再手工复制粘贴给 agent。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/todos")]
public class TodoArtifactsController : ControllerBase
{
    private readonly ITodoService _todos;
    private readonly IArtifactImportService _artifacts;

    public TodoArtifactsController(ITodoService todos, IArtifactImportService artifacts)
    {
        _todos = todos;
        _artifacts = artifacts;
    }

    /// <summary>
    /// 列某项目的工件目录（带每个 <c>NN-*.md</c> 的字节数与"是否核心件"标记）。
    /// 项目没有该目录 ⇒ 空集合 + 说明文案（界面据此走分级空态，不写"暂无数据"了事）。
    /// </summary>
    [HttpGet("artifact-sets")]
    public async Task<ActionResult<ApiResponse<List<ArtifactSetDto>>>> ListSets([FromQuery] int projectId = 0,
        [FromQuery] string? projectPath = null)
    {
        try
        {
            string? root = null;
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                root = projectPath;
            }
            else if (projectId > 0)
            {
                root = (await _todos.GetTodosAsync(new TaskQuery { ProjectId = projectId, PageSize = 1 }))
                    .Items.FirstOrDefault()?.ProjectRoot;
                if (string.IsNullOrWhiteSpace(root))
                    return BadRequest(ApiResponse<List<ArtifactSetDto>>.Error(
                        $"项目 {projectId} 下没有已关联的任务，无法确定项目根；请直接传 projectPath", 400));
            }
            else
            {
                return BadRequest(ApiResponse<List<ArtifactSetDto>>.Error("请给出 projectId 或 projectPath", 400));
            }

            var result = _artifacts.ListSets(root);
            if (!result.Ok)
                return BadRequest(ApiResponse<List<ArtifactSetDto>>.Error(result.Error ?? "工件目录读取失败", 400));

            return Ok(ApiResponse<List<ArtifactSetDto>>.Ok(result.Items, result.Error ?? "获取工件目录成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工件目录失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<ArtifactSetDto>>.Error("获取工件目录失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 把选中工件组装进任务正文。已写正文的任务返回 409，界面据此弹确认后带 <c>overwrite=true</c> 再发一次
    /// （"点一下就把已有内容冲掉"的动作必须二次确认，plugin-development §3.2）。
    /// </summary>
    [HttpPost("{id:int}/artifacts/import")]
    public async Task<ActionResult<ApiResponse<ImportArtifactsResultDto>>> Import(int id, [FromBody] ImportArtifactsRequest request)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<ImportArtifactsResultDto>.Error("id 必须为正整数", 400));
            if (request == null) return BadRequest(ApiResponse<ImportArtifactsResultDto>.Error("请求体不能为空", 400));

            var result = await _artifacts.ImportAsync(id, request);
            if (result.Ok) return Ok(ApiResponse<ImportArtifactsResultDto>.Ok(result, "工件已导入任务正文"));

            return result.Conflict
                ? Conflict(ApiResponse<ImportArtifactsResultDto>.Error(result.Error ?? "正文已存在", 409))
                : BadRequest(ApiResponse<ImportArtifactsResultDto>.Error(result.Error ?? "工件导入失败", 400));
        }
        catch (Exception ex)
        {
            // 读盘异常（占用/权限/非法文件名）如实冒泡 500，不伪装成"参数错"（plugin-development §C）
            XTrace.Log.Error("工件导入失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<ImportArtifactsResultDto>.Error("工件导入失败: " + ex.Message));
        }
    }
}
