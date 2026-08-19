using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.AIAgent.Models;
using OpenForgeSelf.Backend.Plugins.AIAgent.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Controllers;

/// <summary>
/// AI对话控制器
/// </summary>
/// <remarks>
/// 提供AI对话相关的API接口，包括普通对话、流式对话、历史消息管理和工具查询等功能。
/// </remarks>
[ApiController]
[Route("api/ai-agent/chat")]
public class AIChatController : ControllerBase
{
    private readonly IAIAgentService _aiAgentService;
    private readonly IPluginMessageService _messageService;
    private readonly IToolRegistry _toolRegistry;

    public AIChatController(IAIAgentService aiAgentService, IPluginMessageService messageService, IToolRegistry toolRegistry)
    {
        _aiAgentService = aiAgentService;
        _messageService = messageService;
        _toolRegistry = toolRegistry;
    }

    /// <summary>
    /// 发送AI对话消息
    /// </summary>
    /// <param name="request">对话请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>AI回复消息</returns>
    /// <response code="200">返回AI回复消息</response>
    /// <response code="400">请求参数错误</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "消息不能为空" });
            }

            var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString("N")
                : request.SessionId;

            XTrace.Log.Info("[AIAgentPlugin] 收到聊天请求，SessionId: {0}, 消息长度: {1}", sessionId, request.Message.Length);

            await _messageService.SaveMessageAsync(sessionId, "user", request.Message);

            var history = await _messageService.GetHistoryAsync(sessionId);

            var aiMessages = history.Select(m => new AIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            var aiResponse = await _aiAgentService.ChatAsync(aiMessages, true);

            var responseId = await _messageService.SaveMessageAsync(sessionId, "assistant", aiResponse);

            var response = new ChatResponse
            {
                Id = responseId,
                SessionId = sessionId,
                Role = "assistant",
                Content = aiResponse,
                CreateTime = DateTime.Now
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 处理聊天请求失败: {0}", ex.Message);
            return StatusCode(500, new { error = "处理请求时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 发送AI流式对话消息
    /// </summary>
    /// <param name="request">对话请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>以SSE格式流式返回AI回复内容</returns>
    /// <response code="200">流式返回AI回复内容</response>
    /// <response code="400">请求参数错误</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("stream")]
    public async Task SendMessageStream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync(JsonSerializer.Serialize(new { error = "消息不能为空" }));
                return;
            }

            var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString("N")
                : request.SessionId;

            XTrace.Log.Info("[AIAgentPlugin] 收到流式聊天请求，SessionId: {0}, 消息长度: {1}", sessionId, request.Message.Length);

            await _messageService.SaveMessageAsync(sessionId, "user", request.Message);

            var history = await _messageService.GetHistoryAsync(sessionId);

            var aiMessages = history.Select(m => new AIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var fullResponse = new System.Text.StringBuilder();

            await foreach (var chunk in _aiAgentService.ChatStreamAsync(aiMessages, true, cancellationToken))
            {
                fullResponse.Append(chunk);

                var sseData = JsonSerializer.Serialize(new { content = chunk, sessionId });
                await Response.WriteAsync($"data: {sseData}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            var responseId = await _messageService.SaveMessageAsync(sessionId, "assistant", fullResponse.ToString());

            var completeData = JsonSerializer.Serialize(new { done = true, sessionId, responseId });
            await Response.WriteAsync($"data: {completeData}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            XTrace.Log.Info("[AIAgentPlugin] 流式聊天完成，SessionId: {0}, 响应长度: {1}", sessionId, fullResponse.Length);
        }
        catch (OperationCanceledException)
        {
            XTrace.Log.Info("[AIAgentPlugin] 流式聊天请求被取消");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 处理流式聊天请求失败: {0}", ex.Message);
            Response.StatusCode = 500;
            await Response.WriteAsync(JsonSerializer.Serialize(new { error = "处理请求时发生错误", details = ex.Message }));
        }
    }

    /// <summary>
    /// 获取会话历史消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="limit">返回消息数量限制，默认50条</param>
    /// <returns>历史消息列表</returns>
    /// <response code="200">返回历史消息列表</response>
    /// <response code="400">请求参数错误</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("history/{sessionId}")]
    public async Task<ActionResult<List<ChatResponse>>> GetHistory(string sessionId, [FromQuery] int limit = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { error = "会话ID不能为空" });
            }

            XTrace.Log.Info("[AIAgentPlugin] 获取历史消息，SessionId: {0}, 限制: {1}", sessionId, limit);

            var messages = await _messageService.GetHistoryAsync(sessionId, limit);

            var response = messages.Select(m => new ChatResponse
            {
                Id = m.Id,
                SessionId = m.SessionId,
                Role = m.Role,
                Content = m.Content,
                CreateTime = m.CreateTime
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 获取历史消息失败: {0}", ex.Message);
            return StatusCode(500, new { error = "获取历史消息时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 删除会话消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>删除结果</returns>
    /// <response code="200">会话消息已删除</response>
    /// <response code="400">请求参数错误</response>
    /// <response code="500">服务器内部错误</response>
    [HttpDelete("session/{sessionId}")]
    public async Task<ActionResult> DeleteSession(string sessionId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { error = "会话ID不能为空" });
            }

            XTrace.Log.Info("[AIAgentPlugin] 删除会话消息，SessionId: {0}", sessionId);

            await _messageService.DeleteSessionAsync(sessionId);

            return Ok(new { success = true, message = "会话消息已删除" });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 删除会话消息失败: {0}", ex.Message);
            return StatusCode(500, new { error = "删除会话消息时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有可用的AI工具函数
    /// </summary>
    /// <returns>可用工具函数列表</returns>
    /// <response code="200">返回可用工具函数列表</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("tools")]
    public ActionResult<object> GetAvailableTools()
    {
        try
        {
            var toolList = _toolRegistry.GetAllTools();

            var result = toolList.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                description = t.Description,
                pluginId = t.PluginId
            }).ToList();

            return Ok(new { success = true, tools = result, count = result.Count });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 获取可用工具失败: {0}", ex.Message);
            return StatusCode(500, new { error = "获取可用工具时发生错误", details = ex.Message });
        }
    }
}
