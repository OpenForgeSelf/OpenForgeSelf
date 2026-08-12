using OpenForgeSelf.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Controllers;

/// <summary>
/// 聊天会话视图 API：以「会话」为维度列出/查看，会话下挂轮次（ChatTurn）明细。
/// app 自有聊天（Source=App）与代理录制（Source=Proxy）统一在此聚合。
/// </summary>
[ApiController]
[Route("api/chat-sessions")]
public class ChatRecordsController : ControllerBase
{
    private readonly IChatSessionService _chatSessionService;
    private readonly ILogService _logService;

    public ChatRecordsController(IChatSessionService chatSessionService, ILogService logService)
    {
        _chatSessionService = chatSessionService;
        _logService = logService;
    }

    /// <summary>
    /// 分页列出会话。支持来源(App/Proxy/All)、客户端类型、API 风格、时间区间与关键字过滤。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSessions(
        [FromQuery] string? source,
        [FromQuery] string? clientKind,
        [FromQuery] string? style,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? key,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var (sessions, total) = await _chatSessionService.GetSessionsAsync(source, clientKind, style, from, to, key, page, pageSize);

            return Ok(new
            {
                success = true,
                data = sessions,
                total,
                page,
                pageSize,
                pageCount = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logService.Error("获取会话列表失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取单个会话及其轮次明细（按 TurnIndex 升序）。
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetSession(long id)
    {
        try
        {
            var (session, turns) = await _chatSessionService.GetSessionAsync(id);

            if (session == null)
            {
                return NotFound(new { success = false, message = $"会话不存在，ID: {id}" });
            }

            return Ok(new { success = true, data = new { session, turns } });
        }
        catch (Exception ex)
        {
            _logService.Error("获取会话详情失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
