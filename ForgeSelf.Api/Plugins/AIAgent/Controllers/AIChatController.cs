using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Controllers;

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
    private readonly IContext _ctx;

    /// <summary>SSE 事件序列化选项：camelCase（对齐前端事件/usage 字段）。</summary>
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // 注意：IToolRegistry 是宿主契约，插件子容器只含插件自身服务 + IContext，
    // 构造注入宿主契约会导致控制器激活 500（e2e 实测）。故注入 IContext，
    // 在 /tools 端点内运行期经 ctx.Get<IToolRegistry>() 获取。
    public AIChatController(IAIAgentService aiAgentService, IPluginMessageService messageService, IContext ctx)
    {
        _aiAgentService = aiAgentService;
        _messageService = messageService;
        _ctx = ctx;
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

            await _messageService.SaveMessageAsync(sessionId, "user", request.Message);

            var history = await _messageService.GetHistoryAsync(sessionId);

            var aiMessages = history.Select(m => new AIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            var finalContent = string.Empty;
            var toolCalls = new List<string>();
            var toolTracer = new ToolTraceCollector();
            UnifiedUsage? usage = null;
            string? error = null;

            await foreach (var ev in _aiAgentService.RunAgentLoopAsync(aiMessages, request.ChatModelId, request.AgentId, request.EnabledToolNames, request.SkillIds, true, cancellationToken))
            {
                switch (ev.Type)
                {
                    case "tool_call":
                        if (!string.IsNullOrEmpty(ev.Name)) toolCalls.Add(ev.Name);
                        toolTracer.OnCall(ev.Name, ev.Arguments);
                        break;
                    case "tool_result":
                        toolTracer.OnResult(ev.Name, ev.Result, ev.Success);
                        break;
                    case "usage":
                        usage = ev.Usage ?? usage;
                        break;
                    case "done":
                        finalContent = ev.Content ?? string.Empty;
                        usage = ev.Usage ?? usage;
                        break;
                    case "error":
                        error = ev.Content;
                        break;
                }
            }

            if (error != null)
            {
                return StatusCode(500, new { error = "处理请求时发生错误", details = error });
            }

            var toolCallsJson = toolTracer.ToJson();
            var responseId = await _messageService.SaveMessageAsync(sessionId, "assistant", finalContent, toolCallsJson);

            var response = new ChatResponse
            {
                Id = responseId,
                SessionId = sessionId,
                Role = "assistant",
                Content = finalContent,
                CreateTime = DateTime.Now,
                ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
                ToolCallsJson = toolCallsJson,
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

            await _messageService.SaveMessageAsync(sessionId, "user", request.Message);

            var history = await _messageService.GetHistoryAsync(sessionId);

            var aiMessages = history.Select(m => new AIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            var fullResponse = new System.Text.StringBuilder();
            var toolTracer = new ToolTraceCollector();

            await foreach (var ev in _aiAgentService.RunAgentLoopAsync(aiMessages, request.ChatModelId, request.AgentId, request.EnabledToolNames, request.SkillIds, true, cancellationToken))
            {
                switch (ev.Type)
                {
                    case "content":
                        fullResponse.Append(ev.Content);
                        await WriteEventAsync(new { type = "content", content = ev.Content, sessionId });
                        break;
                    case "tool_call":
                        await WriteEventAsync(new { type = "tool_call", name = ev.Name, arguments = ev.Arguments, sessionId });
                        toolTracer.OnCall(ev.Name, ev.Arguments);
                        break;
                    case "tool_result":
                        await WriteEventAsync(new { type = "tool_result", name = ev.Name, result = ev.Result, success = ev.Success, sessionId });
                        toolTracer.OnResult(ev.Name, ev.Result, ev.Success);
                        break;
                    case "usage":
                        await WriteEventAsync(new { type = "usage", usage = ev.Usage, sessionId });
                        break;
                    case "done":
                        var toolCallsJson = toolTracer.ToJson();
                        var responseId = await _messageService.SaveMessageAsync(sessionId, "assistant", fullResponse.ToString(), toolCallsJson);
                        await WriteEventAsync(new { type = "done", sessionId, responseId, usage = ev.Usage, toolCallsJson });
                        break;
                    case "error":
                        await WriteEventAsync(new { type = "error", content = ev.Content, sessionId });
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

    /// <summary>
    /// FreeLoop 工具调用轨迹收集器（031 方案A）：在 agent 循环期间累积 tool_call/tool_result，
    /// 随 assistant 消息落库为 ToolCallsJson，供历史消息渲染「刷新后仍可见」的工具卡片。
    /// 调用/结果配对用栈（agent 循环顺序执行，call 后必紧跟对应 result），并统计耗时。
    /// </summary>
    private sealed class ToolTraceCollector
    {
        private readonly List<ChatToolCallTrace> _records = new();
        private readonly Stack<int> _stack = new();
        private readonly List<DateTime> _starts = new();

        /// <summary>记录一次工具调用开始（参数）。</summary>
        public void OnCall(string? name, string? arguments)
        {
            var idx = _records.Count;
            _records.Add(new ChatToolCallTrace { Name = name, Args = arguments });
            _starts.Add(DateTime.Now);
            _stack.Push(idx);
        }

        /// <summary>记录一次工具调用结果（配对最近的调用），填充结果/成败/耗时。</summary>
        public void OnResult(string? name, string? result, bool? success)
        {
            if (_stack.Count == 0) return;
            var idx = _stack.Pop();
            var rec = _records[idx];
            rec.Result = result;
            rec.Success = success ?? false;
            rec.DurationMs = Math.Max(0, (long)(DateTime.Now - _starts[idx]).TotalMilliseconds);
        }

        /// <summary>序列化为 camelCase JSON 数组；无轨迹时返回 null（不写列）。</summary>
        public string? ToJson()
        {
            if (_records.Count == 0) return null;
            return JsonSerializer.Serialize(_records, SseJsonOptions);
        }
    }
}
