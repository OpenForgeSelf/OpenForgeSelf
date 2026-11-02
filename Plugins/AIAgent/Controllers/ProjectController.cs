using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
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
    private readonly IContext? _context;

    /// <summary>
    /// 审批服务是宿主契约。插件子容器解析不到它，构造注入会让本控制器激活 500。
    /// </summary>
    public ProjectController(
        IProjectWorkspaceService workspace,
        IProjectSkillScannerService skillScanner,
        IProjectRegistryService registry,
        IContext? context = null)
    {
        _workspace = workspace;
        _skillScanner = skillScanner;
        _registry = registry;
        _context = context;
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

    /// <summary>
    /// 当前项目目录下的技能（.agents/skills + .codebuddy/commands），再并上宿主个人技能库里已启用的项。
    /// 项目扫描本身不改；个人技能来源为 personal，没有项目内路径。
    /// </summary>
    [HttpGet("skills")]
    public ActionResult<List<ProjectSkillItem>> GetSkills()
    {
        var items = _skillScanner.Scan(_workspace.ProjectRoot);
        try
        {
            var catalog = _context?.Get<IPersonalSkillCatalog>();
            if (catalog != null)
            {
                var seen = new HashSet<string>(items.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
                foreach (var skill in catalog.ListEnabled())
                {
                    if (string.IsNullOrWhiteSpace(skill.Id) || !seen.Add(skill.Id))
                        continue;
                    items.Add(new ProjectSkillItem
                    {
                        Id = skill.Id,
                        Name = skill.Name,
                        Description = skill.Description,
                        Source = "personal",
                        Path = string.Empty,
                        IsDirectory = false
                    });
                }
                items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception)
        {
            // 个人库不可用时仍返回项目扫描结果
        }
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

    /// <summary>写入/新建文件（相对路径）。高风险：先走与工具相同的审批，通过后才落盘。</summary>
    [HttpPost("file")]
    public async Task<ActionResult> WriteFile([FromBody] WriteProjectFileRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { success = false, error = "path 必填" });
        }

        var content = request.Content ?? string.Empty;
        if (!FilePathGuard.TryLimitBytes(Encoding.UTF8.GetByteCount(content), "写入内容", out var sizeError))
        {
            return BadRequest(new { success = false, error = sizeError + "，未写入" });
        }

        var argsJson = JsonSerializer.Serialize(new
        {
            path = request.Path,
            byteLength = Encoding.UTF8.GetByteCount(content),
            sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
        });
        var denied = await RequireApprovalAsync("write_file", argsJson, "项目接口直接写入文件 " + request.Path, ct);
        if (denied != null)
        {
            return denied;
        }

        try
        {
            _workspace.WriteFile(request.Path, content);
            return Ok(new { success = true, path = request.Path });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>删除文件（相对路径）。高风险：先走与工具相同的审批，通过后才删除。</summary>
    [HttpDelete("file")]
    public async Task<ActionResult> DeleteFile([FromQuery] string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { success = false, error = "path 必填" });
        }

        var denied = await RequireApprovalAsync(
            "delete_file",
            JsonSerializer.Serialize(new { path }),
            "项目接口直接删除文件 " + path,
            ct);
        if (denied != null)
        {
            return denied;
        }

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

    /// <summary>
    /// 与 MAF 同一套 <see cref="HighRiskToolPolicy"/>：需要审批时调用 <see cref="IApprovalService.AskAsync"/>，
    /// 未通过/超时不执行。审批 HTTP 仍是现有 /api/ai-agent/approvals。
    /// </summary>
    private async Task<ActionResult?> RequireApprovalAsync(string toolName, string argsJson, string reason, CancellationToken ct)
    {
        if (!HighRiskToolPolicy.RequiresApproval(toolName))
        {
            return null;
        }

        var sessionId = ResolveSessionId();
        var approvals = _context?.Get<IApprovalService>();
        if (approvals == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                error = "宿主未提供审批服务，未执行",
                tool = toolName,
                sessionId,
            });
        }

        var approved = await approvals.AskAsync(sessionId, toolName, argsJson, reason, ct).ConfigureAwait(false);
        if (approved)
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            success = false,
            error = "审批未通过或已超时，未执行",
            tool = toolName,
            sessionId,
        });
    }

    private string ResolveSessionId()
    {
        if (Request.Headers.TryGetValue("X-Forge-Session-Id", out var header) && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString().Trim();
        }

        var query = Request.Query["sessionId"].ToString();
        return string.IsNullOrWhiteSpace(query) ? "project-http" : query.Trim();
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