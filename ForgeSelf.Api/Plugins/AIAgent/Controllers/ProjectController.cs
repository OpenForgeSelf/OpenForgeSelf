using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.AIAgent.Controllers;

/// <summary>
/// 项目工作区控制器：管理「Agent 选择的目录（一个目录视为一个项目）」与其中的文件编辑。
/// 由插件自带界面（web/）在前端直连调用，或作为文件 MCP 工具的后端支撑。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private readonly IProjectWorkspaceService _workspace;
    private readonly IProjectSkillScannerService _skillScanner;
    private readonly IProjectRegistryService _registry;

    public ProjectController(
        IProjectWorkspaceService workspace,
        IProjectSkillScannerService skillScanner,
        IProjectRegistryService registry)
    {
        _workspace = workspace;
        _skillScanner = skillScanner;
        _registry = registry;
    }

    /// <summary>设定项目根目录（GET 由 /directory 提供）。成功后自动登记到 sems 共享项目清单。</summary>
    [HttpPost("directory")]
    public ActionResult SetDirectory([FromBody] SetProjectDirectoryRequest request)
    {
        if (!_workspace.TrySetProjectRoot(request.Path, out var error))
        {
            return BadRequest(new { success = false, error });
        }

        // 一个目录即一个项目：同步登记到宿主共享清单，供 sems 首页展示。
        _registry.Register(_workspace.ProjectRoot!, out _);

        return Ok(new { success = true, root = _workspace.ProjectRoot });
    }

    /// <summary>返回当前项目根目录。</summary>
    [HttpGet("directory")]
    public ActionResult<object> GetDirectory()
    {
        return Ok(new { success = true, set = _workspace.IsProjectSet, root = _workspace.ProjectRoot });
    }

    /// <summary>自动识别当前项目目录下的技能（.agents/skills + .codebuddy/commands）。</summary>
    [HttpGet("skills")]
    public ActionResult<List<ProjectSkillItem>> GetSkills()
    {
        var items = _skillScanner.Scan(_workspace.ProjectRoot);
        return Ok(items);
    }

    /// <summary>列出项目目录下的目录项（相对路径，顶层为空串）。</summary>
    [HttpGet("files")]
    public ActionResult<object> ListFiles([FromQuery] string? path)
    {
        try
        {
            var entries = _workspace.ListEntries(path ?? string.Empty);
            return Ok(new { success = true, root = _workspace.ProjectRoot, files = entries });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>读取文件内容（相对路径）。</summary>
    [HttpGet("file")]
    public ActionResult<object> ReadFile([FromQuery] string path)
    {
        try
        {
            var content = _workspace.ReadFile(path);
            return Ok(new { success = true, path, content });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>写入/新建文件（相对路径）。</summary>
    [HttpPost("file")]
    public ActionResult WriteFile([FromBody] WriteProjectFileRequest request)
    {
        try
        {
            _workspace.WriteFile(request.Path, request.Content ?? string.Empty);
            return Ok(new { success = true, path = request.Path });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>删除文件（相对路径）。</summary>
    [HttpDelete("file")]
    public ActionResult DeleteFile([FromQuery] string path)
    {
        try
        {
            _workspace.DeleteFile(path);
            return Ok(new { success = true, path });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}

public class SetProjectDirectoryRequest
{
    public string Path { get; set; } = string.Empty;
}

public class WriteProjectFileRequest
{
    public string Path { get; set; } = string.Empty;
    public string? Content { get; set; }
}