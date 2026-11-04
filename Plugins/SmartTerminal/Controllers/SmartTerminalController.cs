using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Controllers;

/// <summary>
/// 智能终端管理面。全部端点会启进程 / 改会话状态 ⇒ 类级 ApiKeyPolicy（plugin-development 铁律 17）。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/smart-terminal")]
public class SmartTerminalController : ControllerBase
{
    private readonly ITerminalSessionService _sessions;
    private readonly IPermissionClassifier _classifier;

    public SmartTerminalController(ITerminalSessionService sessions, IPermissionClassifier classifier)
    {
        _sessions = sessions;
        _classifier = classifier;
    }

    [HttpGet("shells")]
    public ActionResult<ApiResponse<IReadOnlyList<ShellProfileDto>>> ListShells()
        => Ok(ApiResponse<IReadOnlyList<ShellProfileDto>>.Ok(_sessions.ListShells(), "可用外壳"));

    [HttpGet("permission-mode")]
    public ActionResult<ApiResponse<object>> GetPermissionMode()
        => Ok(ApiResponse<object>.Ok(new
        {
            mode = _sessions.GetGlobalPermissionMode().ToString(),
            sessionDefault = PermissionMode.AskWrite.ToString()
        }, "当前全局权限模式"));

    [HttpPut("permission-mode")]
    public ActionResult<ApiResponse<object>> SetPermissionMode([FromBody] SetPermissionModeRequest request)
    {
        _sessions.SetGlobalPermissionMode(request.Mode);
        return Ok(ApiResponse<object>.Ok(new { mode = request.Mode.ToString() }, "已更新全局权限模式"));
    }

    [HttpPost("classify")]
    public ActionResult<ApiResponse<PermissionClassifyResult>> Classify([FromBody] RunCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Command))
            return BadRequest(ApiResponse<PermissionClassifyResult>.Error("command 不能为空", 400));
        var result = _classifier.Classify(request.Command);
        return Ok(ApiResponse<PermissionClassifyResult>.Ok(result, "分类完成（未执行）"));
    }

    [HttpGet("sessions")]
    public ActionResult<ApiResponse<IReadOnlyList<TerminalSessionDto>>> ListSessions()
        => Ok(ApiResponse<IReadOnlyList<TerminalSessionDto>>.Ok(_sessions.ListSessions(), "会话列表"));

    [HttpPost("sessions")]
    public ActionResult<ApiResponse<TerminalSessionDto>> CreateSession([FromBody] CreateSessionRequest? request)
    {
        try
        {
            var dto = _sessions.CreateSession(request ?? new CreateSessionRequest());
            return Ok(ApiResponse<TerminalSessionDto>.Ok(dto, "会话已创建"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SmartTerminal] 创建会话失败: {0}", ex.Message);
            return BadRequest(ApiResponse<TerminalSessionDto>.Error(ex.Message, 400));
        }
    }


    [HttpPost("launch")]
    public ActionResult<ApiResponse<LaunchTerminalResponse>> Launch([FromBody] LaunchTerminalRequest? request)
    {
        try
        {
            var result = _sessions.Launch(request ?? new LaunchTerminalRequest());
            return Ok(ApiResponse<LaunchTerminalResponse>.Ok(result, result.Message));
        }
        catch (InteractivePasswordRequiredException ex)
        {
            // S5：无密 interactive 不建会话；返回可识别 JSON 供联调/误调兜底
            XTrace.Log.Info("[SmartTerminal] launch 需要交互密码: {0}", ex.Message);
            var profileHint = !string.IsNullOrWhiteSpace(request?.Profile)
                ? request!.Profile
                : request?.Type;
            var payload = new LaunchTerminalResponse
            {
                NeedsInteractivePassword = true,
                Message = ex.Message,
                LaunchType = string.IsNullOrWhiteSpace(profileHint) ? "ssh" : profileHint!,
                Profile = profileHint,
                PasswordProvided = false
            };
            return BadRequest(new ApiResponse<LaunchTerminalResponse>
            {
                Success = false,
                Code = 400,
                Message = SecretRedactor.Redact(ex.Message),
                Data = payload
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SmartTerminal] launch 失败: {0}", SecretRedactor.Redact(ex.Message));
            return BadRequest(ApiResponse<LaunchTerminalResponse>.Error(SecretRedactor.Redact(ex.Message), 400));
        }
    }

    [HttpPut("sessions/{sessionId}/title")]
    public ActionResult<ApiResponse<TerminalSessionDto>> Rename(string sessionId, [FromBody] RenameSessionRequest request)
    {
        try
        {
            var dto = _sessions.RenameSession(sessionId, request?.Title ?? string.Empty);
            return Ok(ApiResponse<TerminalSessionDto>.Ok(dto, "已重命名"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<TerminalSessionDto>.Error("会话不存在", 404));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<TerminalSessionDto>.Error(ex.Message, 400));
        }
    }

    [HttpGet("sessions/{sessionId}")]
    public ActionResult<ApiResponse<TerminalSessionDto>> GetSession(string sessionId)
    {
        var dto = _sessions.GetSession(sessionId);
        if (dto == null) return NotFound(ApiResponse<TerminalSessionDto>.Error("会话不存在", 404));
        return Ok(ApiResponse<TerminalSessionDto>.Ok(dto, "会话详情"));
    }

    [HttpPut("sessions/{sessionId}/permission-mode")]
    public ActionResult<ApiResponse<object>> SetSessionMode(string sessionId, [FromBody] SetPermissionModeRequest request)
    {
        try
        {
            _sessions.SetPermissionMode(sessionId, request.Mode);
            return Ok(ApiResponse<object>.Ok(new { sessionId, mode = request.Mode.ToString() }, "会话权限模式已更新"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("会话不存在", 404));
        }
    }

    [HttpPost("sessions/{sessionId}/run")]
    public ActionResult<ApiResponse<RunCommandResponse>> Run(string sessionId, [FromBody] RunCommandRequest request)
    {
        try
        {
            var result = _sessions.ProposeOrRun(sessionId, request?.Command ?? string.Empty, request?.AutoApproveIfAllowed ?? true);
            return Ok(ApiResponse<RunCommandResponse>.Ok(result, result.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<RunCommandResponse>.Error("会话不存在", 404));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<RunCommandResponse>.Error(ex.Message, 400));
        }
    }

    [HttpGet("sessions/{sessionId}/output")]
    public ActionResult<ApiResponse<OutputSnapshotDto>> Output(string sessionId, [FromQuery] int afterIndex = 0)
    {
        try
        {
            return Ok(ApiResponse<OutputSnapshotDto>.Ok(_sessions.GetOutput(sessionId, afterIndex), "输出快照"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<OutputSnapshotDto>.Error("会话不存在", 404));
        }
    }

    [HttpGet("sessions/{sessionId}/commands")]
    public ActionResult<ApiResponse<IReadOnlyList<TerminalCommandRecordDto>>> Commands(string sessionId)
    {
        try
        {
            return Ok(ApiResponse<IReadOnlyList<TerminalCommandRecordDto>>.Ok(_sessions.ListCommands(sessionId), "命令历史"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<IReadOnlyList<TerminalCommandRecordDto>>.Error("会话不存在", 404));
        }
    }

    [HttpPost("sessions/{sessionId}/rerun/{commandId}")]
    public ActionResult<ApiResponse<RunCommandResponse>> Rerun(string sessionId, string commandId)
    {
        try
        {
            var result = _sessions.Rerun(sessionId, commandId);
            return Ok(ApiResponse<RunCommandResponse>.Ok(result, result.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<RunCommandResponse>.Error(ex.Message, 404));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<RunCommandResponse>.Error(ex.Message, 400));
        }
    }

    [HttpPost("sessions/{sessionId}/close")]
    public ActionResult<ApiResponse<object>> Close(string sessionId)
    {
        if (!_sessions.CloseSession(sessionId))
            return NotFound(ApiResponse<object>.Error("会话不存在", 404));
        return Ok(ApiResponse<object>.Ok(new { sessionId, closed = true }, "会话已关闭"));
    }

    [HttpGet("approvals")]
    public ActionResult<ApiResponse<IReadOnlyList<PendingApprovalDto>>> ListApprovals([FromQuery] string? sessionId = null)
        => Ok(ApiResponse<IReadOnlyList<PendingApprovalDto>>.Ok(_sessions.ListPendingApprovals(sessionId), "待审批"));

    [HttpPost("approvals/{approvalId}")]
    public ActionResult<ApiResponse<RunCommandResponse>> ResolveApproval(string approvalId, [FromBody] ApprovalActionRequest request)
    {
        try
        {
            var result = _sessions.ResolveApproval(approvalId, request?.Approve ?? false, request?.Note);
            return Ok(ApiResponse<RunCommandResponse>.Ok(result, result.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<RunCommandResponse>.Error("审批项不存在", 404));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<RunCommandResponse>.Error(ex.Message, 400));
        }
    }

    [HttpGet("audit")]
    public ActionResult<ApiResponse<object>> Audit(
        [FromQuery] string? sessionId = null,
        [FromQuery] string? kind = null,
        [FromQuery] string? agentId = null,
        [FromQuery] int take = 100,
        [FromQuery] int skip = 0)
    {
        var items = _sessions.QueryAudit(sessionId, kind, agentId, take, skip);
        return Ok(ApiResponse<object>.Ok(new { items, take, skip }, "审计日志"));
    }
}
