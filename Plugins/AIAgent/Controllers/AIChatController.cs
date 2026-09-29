using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
// B5（041）：Abstractions 的运行时状态枚举 AgentStatus 与插件「Agent 人设状态」Models.AgentStatus 同名，
// 本控制器用的是运行时状态（IAgent.Status），故显式别名消歧。
using AgentStatus = ForgeSelf.Abstractions.AgentStatus;

namespace ForgeSelf.Api.Plugins.AIAgent.Controllers;

/// <summary>
/// AI对话控制器
/// </summary>
/// <remarks>
/// 提供AI对话相关的API接口，包括普通对话、流式对话、历史消息管理和工具查询等功能。
/// <para>
/// B4（040）写路径改序：本控制器的消息写入一律走宿主 <see cref="ISessionStore"/>（唯一写路径），
/// 模型输入只从 <see cref="ISessionStore.DeriveMessages"/> 派生；插件自有 <c>AIChatMessage</c> 表
/// 降级为只读投影，由 <see cref="AIAgentProjectionService"/> 在响应返回前显式同步。
/// </para>
/// </remarks>
[ApiController]
[Route("api/ai-agent/chat")]
public class AIChatController : ControllerBase
{
    private readonly IAIAgentService _aiAgentService;
    private readonly IPluginMessageService _messageService;
    private readonly AIAgentProjectionService _projection;
    private readonly IContext _ctx;
    private ISessionStore? _sessionStore;
    private IAgentRegistry? _agentRegistry;

    /// <summary>SSE 事件序列化选项：camelCase（对齐前端事件/usage 字段）。</summary>
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // 注意：IToolRegistry 是宿主契约，插件子容器只含插件自身服务 + IContext，
    // 构造注入宿主契约会导致控制器激活 500（e2e 实测）。故注入 IContext，
    // 在 /tools 端点内运行期经 ctx.Get<IToolRegistry>() 获取。
    // ISessionStore / AIAgentProjectionService 同理走运行期解析。
    public AIChatController(IAIAgentService aiAgentService, IPluginMessageService messageService, AIAgentProjectionService projection, IContext ctx)
    {
        _aiAgentService = aiAgentService;
        _messageService = messageService;
        _projection = projection;
        _ctx = ctx;
    }

    /// <summary>宿主会话事件日志（运行期经 IContext 解析，见构造函数说明）。</summary>
    private ISessionStore? SessionStore => _sessionStore ??= _ctx.Get<ISessionStore>();

    /// <summary>
    /// B5（041）Agent 运行时注册表（运行期经 IContext 解析）：按会话 id 复用 turn/step 状态机，
    /// 控制器不再自己「落用户消息 → 跑循环 → 落助手消息」，改为「投喂收件箱 → 消费 TurnFrame」。
    /// </summary>
    private IAgentRegistry? AgentRegistry => _agentRegistry ??= _ctx.Get<IAgentRegistry>();

    /// <summary>安全同步投影：失败仅记日志，不中断聊天响应（对齐宿主侧语义）。</summary>
    private async Task SyncProjectionSafeAsync(string sessionId)
    {
        try
        {
            await _projection.SyncAsync(sessionId);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 会话投影同步失败（不影响聊天响应）: SessionId={0}, {1}", sessionId, ex.Message);
        }
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

            XTrace.Log.Info("[AIAgentPlugin] 收到聊天请求，SessionId: {0}, 消息长度: {1}, 模型: {2}, Agent: {3}",
                sessionId, request.Message.Length, request.ChatModelId ?? "(默认)", request.AgentId ?? "(默认)");

            var sessionStore = SessionStore;
            if (sessionStore == null)
            {
                return StatusCode(500, new { error = "宿主未提供 ISessionStore 契约" });
            }

            var registry = AgentRegistry;
            if (registry == null)
            {
                return StatusCode(500, new { error = "宿主未提供 IAgentRegistry 契约（Agent 运行时注册表）" });
            }

            // B5（041）：会话主路径经 Agent 运行时注册表跑 turn/step 状态机；
            // 用户消息经收件箱投喂（由状态机落 user/message），助手产出由状态机落 assistant/message。
            var options = _aiAgentService.BuildAgentOptions(
                request.ChatModelId, request.AgentId, request.EnabledToolNames, request.SkillIds);
            var agent = await registry.GetOrCreateAsync(sessionId, options, cancellationToken);
            if (agent.Status == AgentStatus.Running)
            {
                return StatusCode(409, new { error = "该会话上一轮对话仍在进行，请稍后再试" });
            }

            agent.Inbox.Followup(sessionId, request.Message);

            var finalContent = new System.Text.StringBuilder();
            var toolCalls = new List<string>();
            string? error = null;

            await foreach (var frame in agent.RunAsync(cancellationToken))
            {
                switch (frame)
                {
                    case AssistantDelta delta:
                        finalContent.Append(delta.Content);
                        break;
                    case ToolStarted call:
                        if (!toolCalls.Contains(call.ToolName)) toolCalls.Add(call.ToolName);
                        break;
                    case TurnFailed failed:
                        error = failed.Error;
                        break;
                }
            }

            // 返回前显式 await 投影：AIChatMessage 只读视图与日志对齐（杜绝异步火后即忘竞态）
            await SyncProjectionSafeAsync(sessionId);

            if (error != null)
            {
                return StatusCode(500, new { error = "处理请求时发生错误", details = error });
            }

            // 响应 id / 用量一律从日志最后一条 assistant/message 取（单一真源，不另存一份）
            var lastAssistant = sessionStore.Replay(sessionId).OfType<AssistantMessageEvent>().LastOrDefault();
            var usage = lastAssistant?.Usage == null
                ? null
                : new UnifiedUsage
                {
                    PromptTokens = (int)lastAssistant.Usage.PromptTokens,
                    CompletionTokens = (int)lastAssistant.Usage.CompletionTokens,
                    TotalTokens = (int)(lastAssistant.Usage.PromptTokens + lastAssistant.Usage.CompletionTokens)
                };

            var response = new ChatResponse
            {
                Id = lastAssistant?.Id ?? 0,
                SessionId = sessionId,
                Role = "assistant",
                Content = finalContent.ToString(),
                CreateTime = DateTime.Now,
                ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
                ToolCallsJson = toolCalls.Count > 0 ? _projection.BuildLastToolCallsJson(sessionId) : null,
                Usage = usage
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
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString("N")
            : request.SessionId;

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        // SSE 事件统一 camelCase（与前端 AgentUsage/事件字段 camelCase 对齐；
        // 默认序列化会把嵌套 UnifiedUsage 输出为 PascalCase 导致前端 token 解析为 0）。
        async Task WriteEventAsync(object payload)
        {
            var sseData = JsonSerializer.Serialize(payload, SseJsonOptions);
            await Response.WriteAsync($"data: {sseData}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                await WriteEventAsync(new { type = "error", content = "消息不能为空", sessionId });
                return;
            }

            XTrace.Log.Info("[AIAgentPlugin] 收到流式聊天请求，SessionId: {0}, 消息长度: {1}, 模型: {2}, Agent: {3}",
                sessionId, request.Message.Length, request.ChatModelId ?? "(默认)", request.AgentId ?? "(默认)");

            var sessionStore = SessionStore;
            if (sessionStore == null)
            {
                await WriteEventAsync(new { type = "error", content = "宿主未提供 ISessionStore 契约", sessionId });
                return;
            }

            var registry = AgentRegistry;
            if (registry == null)
            {
                await WriteEventAsync(new { type = "error", content = "宿主未提供 IAgentRegistry 契约（Agent 运行时注册表）", sessionId });
                return;
            }

            // B5（041）：与同步端点同款——经注册表取会话状态机，投喂收件箱后消费 TurnFrame
            var options = _aiAgentService.BuildAgentOptions(
                request.ChatModelId, request.AgentId, request.EnabledToolNames, request.SkillIds);
            var agent = await registry.GetOrCreateAsync(sessionId, options, cancellationToken);
            if (agent.Status == AgentStatus.Running)
            {
                await WriteEventAsync(new { type = "error", content = "该会话上一轮对话仍在进行，请稍后再试", sessionId });
                return;
            }

            agent.Inbox.Followup(sessionId, request.Message);

            var fullResponse = new System.Text.StringBuilder();

            await foreach (var frame in agent.RunAsync(cancellationToken))
            {
                switch (frame)
                {
                    case AssistantDelta delta:
                        fullResponse.Append(delta.Content);
                        await WriteEventAsync(new { type = "content", content = delta.Content, sessionId });
                        break;

                    case ToolStarted call:
                        await WriteEventAsync(new { type = "tool_call", name = call.ToolName, arguments = call.ArgsJson, sessionId });
                        break;

                    case ToolCompleted done:
                        // 工具结果文本从日志取（单一真源，避免帧里再带一份副本）
                        var toolResult = sessionStore.Replay(sessionId)
                            .OfType<ToolResultEvent>()
                            .LastOrDefault(r => string.Equals(r.CallId, done.CallId, StringComparison.Ordinal));
                        await WriteEventAsync(new
                        {
                            type = "tool_result",
                            name = toolResult?.ToolName ?? string.Empty,
                            result = toolResult?.ResultJson ?? string.Empty,
                            success = done.Outcome == ToolOutcome.Ok,
                            sessionId
                        });
                        break;

                    case TurnCompleted turn:
                        // 投影完成后再发 done：客户端收到 done 时只读视图已落地
                        await SyncProjectionSafeAsync(sessionId);

                        // 响应 id / 用量从日志最后一条 assistant/message 取（单一真源）
                        var lastAssistant = sessionStore.Replay(sessionId).OfType<AssistantMessageEvent>().LastOrDefault();
                        var usage = lastAssistant?.Usage == null
                            ? null
                            : new UnifiedUsage
                            {
                                PromptTokens = (int)lastAssistant.Usage.PromptTokens,
                                CompletionTokens = (int)lastAssistant.Usage.CompletionTokens,
                                TotalTokens = (int)(lastAssistant.Usage.PromptTokens + lastAssistant.Usage.CompletionTokens)
                            };

                        await WriteEventAsync(new
                        {
                            type = "done",
                            sessionId,
                            responseId = lastAssistant?.Id ?? 0,
                            usage,
                            toolCallsJson = _projection.BuildLastToolCallsJson(sessionId),
                            turnEndReason = turn.Reason.ToString()
                        });
                        break;

                    case TurnFailed failed:
                        await WriteEventAsync(new { type = "error", content = failed.Error, sessionId });
                        break;
                }
            }

            XTrace.Log.Info("[AIAgentPlugin] 流式聊天完成，SessionId: {0}, 响应长度: {1}", sessionId, fullResponse.Length);
        }
        catch (OperationCanceledException)
        {
            XTrace.Log.Info("[AIAgentPlugin] 流式聊天请求被取消");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 处理流式聊天请求失败: {0}", ex.Message);
            try
            {
                await WriteEventAsync(new { type = "error", content = ex.Message, sessionId });
            }
            catch
            {
                // 响应可能已断开，忽略二次写入失败
            }
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
                CreateTime = m.CreateTime,
                ToolCallsJson = m.ToolCallsJson
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
    /// 列出会话（按 SessionId 聚合），供前端历史会话列表展示与切换。
    /// 默认仅返回未归档会话（agent 页用）；可经 <paramref name="archived"/> 切换为仅归档 / 全部（会话管理页用）。
    /// </summary>
    /// <param name="archived">归档筛选：active（默认，仅未归档）/ archived（仅已归档）/ all（全部）</param>
    /// <returns>会话摘要列表（按最后消息时间倒序）</returns>
    /// <response code="200">返回会话摘要列表</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("sessions")]
    public async Task<ActionResult<List<SessionSummaryModel>>> GetSessions([FromQuery] string? archived = null)
    {
        try
        {
            // 参数解析：默认 active（agent 页天然隐藏已归档）；非法值按默认处理
            var filter = (archived ?? "").Trim().ToLowerInvariant() switch
            {
                "archived" => SessionArchivedFilter.Archived,
                "all" => SessionArchivedFilter.All,
                _ => SessionArchivedFilter.Active,
            };

            var sessions = await _messageService.GetSessionsAsync(filter);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 获取会话列表失败: {0}", ex.Message);
            return StatusCode(500, new { error = "获取会话列表时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 归档 / 取消归档会话（软标记，不删消息）。
    /// 归档后该会话默认不出现在 agent 页（GET /sessions 默认只列未归档）。
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">归档状态请求体：{ archived: true|false }</param>
    /// <returns>归档结果</returns>
    /// <response code="200">归档状态已更新</response>
    /// <response code="400">请求参数错误</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("session/{sessionId}/archive")]
    public async Task<ActionResult> ArchiveSession(string sessionId, [FromBody] SessionArchiveRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { error = "会话ID不能为空" });
            }

            var archived = request?.Archived ?? false;
            var changed = await _messageService.ArchiveSessionAsync(sessionId, archived);

            XTrace.Log.Info("[AIAgentPlugin] 归档会话，SessionId: {0}, Archived: {1}", sessionId, archived);
            return Ok(new { success = true, archived, changed });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 归档会话失败: {0}", ex.Message);
            return StatusCode(500, new { error = "归档会话时发生错误", details = ex.Message });
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
            var toolRegistry = _ctx.Get<IToolRegistry>();
            if (toolRegistry == null)
            {
                return StatusCode(500, new { error = "宿主未提供 IToolRegistry 契约" });
            }

            var toolList = toolRegistry.GetAllTools();

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

    /// <summary>
    /// 获取长期记忆列表，供 AI Agent 面板的「记忆」分组展示。
    /// 记忆服务经 L1 契约 IMemoryService 由 MemorySystem 插件提供（ctx.Get）。
    /// </summary>
    /// <response code="200">返回记忆列表</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("memories")]
    public async Task<ActionResult> GetMemories([FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var memoryService = _ctx.Get<IMemoryService>();
            if (memoryService == null)
            {
                return StatusCode(500, new { error = "宿主未提供 IMemoryService 契约" });
            }

            var result = await memoryService.SearchAsync(new SearchMemoryRequest
            {
                Keyword = string.IsNullOrWhiteSpace(q) ? null : q,
                Page = page,
                PageSize = pageSize
            });

            return Ok(new
            {
                success = true,
                items = result.Items.Select(m => new
                {
                    id = m.Id,
                    title = m.Title,
                    content = m.Content,
                    type = m.Type.ToString(),
                    importance = m.Importance.ToString(),
                    tags = m.Tags,
                    categoryName = m.CategoryName,
                    createdAt = m.CreatedAt,
                    lastAccessedAt = m.LastAccessedAt
                }),
                total = result.Total,
                page = result.Page,
                pageSize = result.PageSize
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 获取记忆列表失败: {0}", ex.Message);
            return StatusCode(500, new { error = "获取记忆列表时发生错误", details = ex.Message });
        }
    }
}
