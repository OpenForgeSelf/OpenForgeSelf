using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Proxy;
using NewLife.Log;

namespace ForgeSelf.Api.Controllers.UnifiedAI;

/// <summary>
/// 实验性测试接口：用 Microsoft Agent Framework 的 AIAgent 处理 OpenAI Chat 请求与工具循环。
/// 接口本身只负责把 <see cref="AgentResponseUpdate"/> 翻译为 OpenAI SSE，不手写工具循环，
/// 从而避免像原手写代理那样遗漏 tool_calls（本项目已修复的 bug）。
/// 路由：POST /v1/agent/chat/completions
///
/// 重要架构说明：
/// Agent Framework 默认在服务端「自动执行」注册的工具（tools 必须是服务端实现）。
/// 本接口注册了若干演示用服务端工具（见 <see cref="AgentDemoTools"/>）来证明工具调用链路完整可用。
/// 若你的场景是「代理模式」（工具由客户端执行、网关只转发），请使用现有的 /v1/chat/completions（已修复 tool_calls 转发）。
/// </summary>
[ApiController]
[Route("v1/agent")]
[Authorize("ApiKeyPolicy")]
public class AgentChatController : ControllerBase
{
    private readonly ILlmProxyPipeline _pipeline;
    private readonly ILogService _logService;

    private static readonly JsonSerializerOptions StreamSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public AgentChatController(ILlmProxyPipeline pipeline, ILogService logService)
    {
        _pipeline = pipeline;
        _logService = logService;
    }

    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions([FromBody] OpenAIChatCompletionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null || request.Messages == null || request.Messages.Count == 0)
                return BadRequest(new { error = new { message = "messages is required", type = "invalid_request_error" } });

            if (string.IsNullOrEmpty(request.Model))
                return BadRequest(new { error = new { message = "model is required", type = "invalid_request_error" } });

            var chatClient = new LlmProxyChatClient(_pipeline, request.Model);

            // 演示用服务端工具：证明 agent 能完成「模型决策 → 执行工具 → 汇总」
            var demoTools = new List<AITool>
            {
                AIFunctionFactory.Create((Func<string>)AgentDemoTools.GetCurrentDateTime, "get_current_datetime"),
                AIFunctionFactory.Create((Func<int, int, int>)AgentDemoTools.AddNumbers, "add_numbers")
            };

            var systemPrompt = request.Messages
                .FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                ?.GetTextContent();

            var agent = chatClient.AsAIAgent(
                name: "ForgeAgent",
                instructions: systemPrompt ?? "你是一个有帮助的助手，可以调用提供的工具来回答用户问题。",
                tools: demoTools);

            var messages = ConvertToChatMessages(request);

            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no");

            var id = $"chatcmpl-{Guid.NewGuid():N}";
            var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool roleEmitted = false;

            // 初始角色块（OpenAI 要求首块带 role）
            await WriteSseAsync(CreateStreamChunk(id, request.Model, created, 0, role: "assistant"));
            roleEmitted = true;

            await foreach (var update in agent.RunStreamingAsync(messages, cancellationToken: cancellationToken))
            {
                foreach (var chunk in AgentStreamTranslator.Translate(update, id, request.Model, created, ref roleEmitted))
                    await WriteSseAsync(chunk);
            }

            await WriteSseAsync(CreateStreamChunk(id, request.Model, created, 0, finishReason: "stop"));
            await WriteSseRawAsync("[DONE]");

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            var err = UnifiedUpstreamErrors.Resolve(ex);
            if (err.IsUpstream)
                _logService.Error("Agent 测试接口上游返回错误 {0}: {1}", err.StatusCode, err.Message);
            else
                _logService.Error("Agent 测试接口异常: {0}", err.Message);

            if (Response.HasStarted)
            {
                try
                {
                    await UnifiedUpstreamErrors.WriteOpenAiChatStreamErrorAsync(Response, StreamSerializerOptions, err, cancellationToken);
                }
                catch (Exception writeEx)
                {
                    _logService.Error("Agent 流式错误 SSE 回写失败: {0}", writeEx.Message);
                }
                return new EmptyResult();
            }

            return StatusCode(err.StatusCode, UnifiedUpstreamErrors.ToErrorBody(err));
        }
    }

    /// <summary>
    /// 将 OpenAI 请求消息转换为 Microsoft.Extensions.AI 的 ChatMessage 列表。
    /// 文本消息直接转换；工具的 tool 角色消息转为 FunctionResultContent；assistant 的工具调用转为 FunctionCallContent。
    /// </summary>
    private static List<ChatMessage> ConvertToChatMessages(OpenAIChatCompletionRequest request)
    {
        var messages = new List<ChatMessage>();
        foreach (var msg in request.Messages!)
        {
            var text = msg.GetTextContent();
            switch (msg.Role.ToLowerInvariant())
            {
                case "system":
                    messages.Add(new ChatMessage(ChatRole.System, text ?? string.Empty));
                    break;
                case "user":
                    messages.Add(new ChatMessage(ChatRole.User, text ?? string.Empty));
                    break;
                case "tool":
                    messages.Add(new ChatMessage(ChatRole.Tool, new List<AIContent>
                    {
                        new FunctionResultContent(msg.ToolCallId ?? string.Empty, text ?? string.Empty)
                    }));
                    break;
                case "assistant":
                    var contents = new List<AIContent>();
                    if (!string.IsNullOrEmpty(text))
                        contents.Add(new TextContent(text!));
                    if (msg.ToolCalls != null)
                    {
                        foreach (var tc in msg.ToolCalls)
                        {
                            var args = string.IsNullOrEmpty(tc.Function?.Arguments)
                                ? new Dictionary<string, object?>()
                                : JsonSerializer.Deserialize<Dictionary<string, object?>>(tc.Function!.Arguments)
                                  ?? new Dictionary<string, object?>();
                            contents.Add(new FunctionCallContent(tc.Id, tc.Function?.Name ?? string.Empty, args));
                        }
                    }
                    messages.Add(contents.Count > 0
                        ? new ChatMessage(ChatRole.Assistant, contents)
                        : new ChatMessage(ChatRole.Assistant, text ?? string.Empty));
                    break;
                default:
                    messages.Add(new ChatMessage(ChatRole.User, text ?? string.Empty));
                    break;
            }
        }
        return messages;
    }

    private async Task WriteSseAsync<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, StreamSerializerOptions);
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }

    private async Task WriteSseRawAsync(string data)
    {
        await Response.WriteAsync($"data: {data}\n\n");
        await Response.Body.FlushAsync();
    }

    private static OpenAIStreamChunk CreateStreamChunk(string id, string model, long created, int index, string? role = null, string? finishReason = null)
    {
        return new OpenAIStreamChunk
        {
            Id = id,
            Object = "chat.completion.chunk",
            Created = created,
            Model = model,
            Choices = new List<OpenAIStreamChoice>
            {
                new()
                {
                    Index = index,
                    Delta = new OpenAIStreamDelta { Role = role },
                    FinishReason = finishReason
                }
            }
        };
    }

}
