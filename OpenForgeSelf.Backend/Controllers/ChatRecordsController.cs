using OpenForgeSelf.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Controllers;

[ApiController]
[Route("api/chat-records")]
public class ChatRecordsController : ControllerBase
{
    private readonly IChatRecordService _chatRecordService;
    private readonly ILogService _logService;

    public ChatRecordsController(IChatRecordService chatRecordService, ILogService logService)
    {
        _chatRecordService = chatRecordService;
        _logService = logService;
    }

    /// <summary>
    /// 获取聊天记录列表（支持分页和过滤）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecords(
        [FromQuery] string? sessionId,
        [FromQuery] string? style,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var (records, total) = await _chatRecordService.GetRecordsAsync(sessionId, style, from, to, page, pageSize);

            return Ok(new
            {
                success = true,
                data = records,
                total,
                page,
                pageSize,
                pageCount = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logService.Error("获取聊天记录列表失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取聊天记录详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        try
        {
            var record = await _chatRecordService.GetByIdAsync(id);

            if (record == null)
            {
                return NotFound(new { success = false, message = $"聊天记录不存在，ID: {id}" });
            }

            return Ok(new { success = true, data = record });
        }
        catch (Exception ex)
        {
            _logService.Error("获取聊天记录详情失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}