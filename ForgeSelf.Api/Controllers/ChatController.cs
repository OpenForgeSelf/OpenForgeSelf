using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
// 宿主旧版 AI 消息模型与 Abstractions.AIChatMessage 同名，用别名消除 CS0104 歧义。
using LegacyAIChatMessage = ForgeSelf.Api.Models.AIChatMessage;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Controllers;

/// <summary>
/// 聊天控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly IMessageService _messageService;
    private readonly ILogService _logService;
    private readonly IChatSessionService _chatSessionService;
    private readonly ForgeSelf.Abstractions.ISessionStore? _sessionStore;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ChatController(IAIService aiService, IMessageService messageService, ILogService logService, IChatSessionService chatSessionService, ForgeSelf.Abstractions.ISessionStore? sessionStore = null)
    {
        _aiService = aiService;
        _messageService = messageService;
        _logService = logService;
        _chatSessionService = chatSessionService;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// 安全保存聊天消息：持久化失败仅记录日志并返回 0，不向上抛出，避免中断聊天主流程。
    /// 典型场景：超长文本触发 XCode 长度校验/DB 写入异常时，用户仍应拿到 AI 回复。
    /// </summary>
    private async Task<long> SaveMessageSafeAsync(string sessionId, string role, string content)
    {
        try
        {
            return await _messageService.SaveMessageAsync(sessionId, role, content);
        }
        catch (Exception ex)
        {
            _logService.Error("保存聊天消息失败（不影响聊天响应）: SessionId={0}, Role={1}, {2}", sessionId, role, ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// 安全补写会话归属：持久化失败仅记录日志，不向上抛出，避免中断聊天主流程。
    /// </summary>
    private async Task UpsertSessionSafeAsync(string sessionId, string userMessage, int historyCount)
    {
        try
        {
            await _chatSessionService.UpsertSessionAsync(sessionId, SessionSource.App, null, ClientKind.App, "AppChat", userMessage, historyCount);
        }
        catch (Exception ex)
        {
            _logService.Error("补写会话归属失败（不影响聊天响应）: SessionId={0}, {1}", sessionId, ex.Message);
        }
    }

    /// <summary>
    /// 发送聊天消息
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聊天响应</returns>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userMessage = request.GetUserMessage();
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return BadRequest(new { error = "消息不能为空" });
            }

            // 生成会话ID（如果未提供）
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString("N")
                : request.SessionId;

            _logService.Info("收到聊天请求，SessionId: {0}, 消息长度: {1}", sessionId, userMessage.Length);

            // 保存用户消息（持久化失败不影响聊天主流程）
            await SaveMessageSafeAsync(sessionId, "user", userMessage);
            // P4 会话接缝：追加用户消息到仅追加事件日志（模型可见 = 已记录）
            _sessionStore?.Append(sessionId, new ForgeSelf.Abstractions.SessionEvent { Type = "user", Payload = userMessage });

            // 获取历史消息
            var history = await _messageService.GetHistoryAsync(sessionId);

            // 会话归属：app 自有聊天也纳入统一 ChatSession（不改路由，仅补写会话行；失败不影响流程）
            await UpsertSessionSafeAsync(sessionId, userMessage, history.Count + 1);

            // 构建AI请求消息
            var aiMessages = history.Select(m => new LegacyAIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();
            // 兜底：即便历史持久化失败，也确保本次用户消息进入模型上下文，保证正常对话
            if (aiMessages.Count == 0 || aiMessages[^1].Role != "user")
            {
                aiMessages.Add(new LegacyAIChatMessage { Role = "user", Content = userMessage });
            }

            // 获取AI响应（按所选模型路由提供方；未指定则走默认 AI 配置）
            var aiResponse = await _aiService.ChatAsync(aiMessages, request.ChatModelId);

            // 保存AI响应（持久化失败不影响聊天主流程）
            var responseId = await SaveMessageSafeAsync(sessionId, "assistant", aiResponse);
            // P4 会话接缝：追加 AI 响应到仅追加事件日志（模型可见 = 已记录）
            _sessionStore?.Append(sessionId, new ForgeSelf.Abstractions.SessionEvent { Type = "assistant", Payload = aiResponse });

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
            _logService.Error("处理聊天请求失败: {0}", ex.Message);
            return StatusCode(500, new { error = "处理请求时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 发送聊天消息（流式响应）
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应</returns>
    [HttpPost("stream")]
    public async Task SendMessageStream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userMessage = request.GetUserMessage();
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync(JsonSerializer.Serialize(new { error = "消息不能为空" }));
                return;
            }

            // 生成会话ID（如果未提供）
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString("N")
                : request.SessionId;

            _logService.Info("收到流式聊天请求，SessionId: {0}, 消息长度: {1}", sessionId, userMessage.Length);

            // 保存用户消息（持久化失败不影响聊天主流程）
            await SaveMessageSafeAsync(sessionId, "user", userMessage);

            // 获取历史消息
            var history = await _messageService.GetHistoryAsync(sessionId);

            // 会话归属：app 自有聊天也纳入统一 ChatSession（不改路由，仅补写会话行；失败不影响流程）
            await UpsertSessionSafeAsync(sessionId, userMessage, history.Count + 1);

            // 构建AI请求消息
            var aiMessages = history.Select(m => new LegacyAIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();
            // 兜底：即便历史持久化失败，也确保本次用户消息进入模型上下文，保证正常对话
            if (aiMessages.Count == 0 || aiMessages[^1].Role != "user")
            {
                aiMessages.Add(new LegacyAIChatMessage { Role = "user", Content = userMessage });
            }

            // 设置SSE响应头
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var fullResponse = new System.Text.StringBuilder();

            // 流式获取AI响应（按所选模型路由提供方；未指定则走默认 AI 配置）
            await foreach (var chunk in _aiService.ChatStreamAsync(aiMessages, request.ChatModelId, cancellationToken))
            {
                fullResponse.Append(chunk);

                // 发送SSE事件
                var sseData = JsonSerializer.Serialize(new { content = chunk, sessionId });
                await Response.WriteAsync($"data: {sseData}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // 保存完整的AI响应（持久化失败不影响聊天主流程）
            var responseId = await SaveMessageSafeAsync(sessionId, "assistant", fullResponse.ToString());

            // 发送完成事件
            var completeData = JsonSerializer.Serialize(new { done = true, sessionId, responseId });
            await Response.WriteAsync($"data: {completeData}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            _logService.Info("流式聊天完成，SessionId: {0}, 响应长度: {1}", sessionId, fullResponse.Length);
        }
        catch (OperationCanceledException)
        {
            _logService.Info("流式聊天请求被取消");
        }
        catch (Exception ex)
        {
            _logService.Error("处理流式聊天请求失败: {0}", ex.Message);
            Response.StatusCode = 500;
            await Response.WriteAsync(JsonSerializer.Serialize(new { error = "处理请求时发生错误", details = ex.Message }));
        }
    }

    /// <summary>
    /// 获取会话历史消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="limit">消息数量限制</param>
    /// <returns>历史消息列表</returns>
    [HttpGet("history/{sessionId}")]
    public async Task<ActionResult<List<ChatResponse>>> GetHistory(string sessionId, [FromQuery] int limit = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { error = "会话ID不能为空" });
            }

            _logService.Info("获取历史消息，SessionId: {0}, 限制: {1}", sessionId, limit);

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
            _logService.Error("获取历史消息失败: {0}", ex.Message);
            return StatusCode(500, new { error = "获取历史消息时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 删除会话消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete("session/{sessionId}")]
    public async Task<ActionResult> DeleteSession(string sessionId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { error = "会话ID不能为空" });
            }

            _logService.Info("删除会话消息，SessionId: {0}", sessionId);

            await _messageService.DeleteSessionAsync(sessionId);

            return Ok(new { success = true, message = "会话消息已删除" });
        }
        catch (Exception ex)
        {
            _logService.Error("删除会话消息失败: {0}", ex.Message);
            return StatusCode(500, new { error = "删除会话消息时发生错误", details = ex.Message });
        }
    }
}