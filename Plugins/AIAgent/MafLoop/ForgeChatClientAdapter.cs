using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeSelf.Abstractions;
using Microsoft.Extensions.AI;

namespace ForgeSelf.Api.Plugins.AIAgent.MafLoop;

/// <summary>
/// M.E.AI（Microsoft.Extensions.AI）消息 ↔ 宿主 Unified 模型的双向翻译器（PILOT-051 FR-2.2）。
/// 让 MAF（Microsoft Agent Framework）的 <see cref="ChatClientAgent"/> 跑在宿主
/// <c>IAIProvider</c>（OpenAI 兼容/自定义 provider，含代理、模型路由、vLLM 推理内容处理）之上。
/// </summary>
/// <remarks>
/// 翻译约定（与 <see cref="ReactLoopAgent"/> 语义对齐）：
/// - System 指令（ChatOptions.Instructions + System 角色消息）→ <see cref="UnifiedChatRequest.SystemPrompt"/>；
/// - Assistant 消息中的 <see cref="FunctionCallContent"/> → <see cref="UnifiedChatMessage.ToolCalls"/>；
/// - Tool 角色 <see cref="FunctionResultContent"/> → <see cref="UnifiedChatMessage.ToolCallId"/> + Content；
/// - <see cref="ChatToolMode"/> → <see cref="UnifiedChatRequest.ToolChoice"/>（RequireAny → "required"）；
/// - <see cref="DataContent"/>/未知内容块：跳过（多模态 v1 不承诺，见 Spec Known Limitations）。
/// </remarks>
public static class ForgeChatMessageMapper
{
    private static readonly JsonSerializerOptions ArgsJsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>M.E.AI 消息列表 → 宿主统一消息列表（系统消息剔除，返回其文本）。</summary>
    public static (List<UnifiedChatMessage> Messages, string SystemPrompt) ToUnifiedMessages(IEnumerable<ChatMessage> messages)
    {
        var unified = new List<UnifiedChatMessage>();
        var systemTexts = new List<string>();

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                var text = ExtractText(message);
                if (!string.IsNullOrWhiteSpace(text)) systemTexts.Add(text);
                continue;
            }

            var unifiedMessage = new UnifiedChatMessage
            {
                Role = ToUnifiedRole(message.Role),
                Content = ExtractText(message) ?? string.Empty
            };

            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent fcc:
                        // PC-015 自愈：历史里空名调用（旧缺陷落库的空壳）不再进模型请求
                        if (string.IsNullOrWhiteSpace(fcc.Name))
                        {
                            break;
                        }
                        unifiedMessage.ToolCalls ??= new List<UnifiedToolCall>();
                        unifiedMessage.ToolCalls.Add(new UnifiedToolCall
                        {
                            Id = fcc.CallId,
                            Name = fcc.Name ?? string.Empty,
                            Arguments = SerializeArguments(fcc.Arguments)
                        });
                        break;
                    case FunctionResultContent frc:
                        unifiedMessage.ToolCallId = frc.CallId;
                        if (unifiedMessage.Content.Length == 0)
                        {
                            unifiedMessage.Content = frc.Result?.ToString() ?? string.Empty;
                        }
                        break;
                }
            }

            unified.Add(unifiedMessage);
        }

        return (unified, string.Join("\n\n", systemTexts));
    }

    /// <summary>渲染系统提示：ChatOptions.Instructions + System 角色消息文本（合并）。</summary>
    public static string RenderSystemPrompt(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
        {
            parts.Add(options.Instructions!.Trim());
        }

        foreach (var message in messages)
        {
            if (message.Role != ChatRole.System) continue;
            var text = ExtractText(message);
            if (!string.IsNullOrWhiteSpace(text)) parts.Add(text.Trim());
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>M.E.AI 工具集合 → 宿主统一工具定义。</summary>
    public static List<UnifiedToolDefinition>? ToUnifiedTools(ChatOptions? options)
    {
        var tools = options?.Tools;
        if (tools == null || tools.Count == 0) return null;

        var list = new List<UnifiedToolDefinition>();
        foreach (var tool in tools)
        {
            if (tool is not AIFunction function) continue;
            list.Add(new UnifiedToolDefinition
            {
                Name = function.Name,
                Description = function.Description ?? string.Empty,
                Parameters = CloneSchema(function.JsonSchema)
            });
        }

        return list.Count > 0 ? list : null;
    }

    /// <summary>ChatToolMode → Unified ToolChoice（RequireAny → "required"；Auto/None v1 不映射）。</summary>
    public static string? ToToolChoice(ChatOptions? options)
    {
        var mode = options?.ToolMode;
        return mode switch
        {
            RequiredChatToolMode => "required",
            _ => null
        };
    }

    /// <summary>Unified 流式块 → M.E.AI 流式更新（文本/用量/结束原因；工具调用分片由 ForgeChatClientAdapter 聚合后整条产出，PC-015）。</summary>
    public static IEnumerable<ChatResponseUpdate> ToChatResponseUpdates(UnifiedStreamChunk chunk, string? responseId)
    {
        if (!string.IsNullOrEmpty(chunk.DeltaContent))
        {
            var textUpdate = new ChatResponseUpdate(ChatRole.Assistant,
                new List<AIContent> { new TextContent(chunk.DeltaContent) });
            ApplyCommon(textUpdate, responseId, chunk);
            yield return textUpdate;
        }

        // PC-015：这里不再逐片产出 FunctionCallContent——分片（尤其续片）Id/Name 为空，
        // 逐片成调用会把「空名调用」灌进会话日志并毒化后续所有请求（见 ForgeChatClientAdapter 聚合处注释）。

        if (chunk.Usage != null)
        {
            var usageUpdate = new ChatResponseUpdate(null, new List<AIContent>
            {
                new UsageContent(new UsageDetails
                {
                    InputTokenCount = chunk.Usage.PromptTokens,
                    OutputTokenCount = chunk.Usage.CompletionTokens,
                    TotalTokenCount = chunk.Usage.TotalTokens > 0
                        ? chunk.Usage.TotalTokens
                        : chunk.Usage.PromptTokens + chunk.Usage.CompletionTokens
                })
            });
            ApplyCommon(usageUpdate, responseId, chunk);
            yield return usageUpdate;
        }

        if (!string.IsNullOrEmpty(chunk.FinishReason))
        {
            var finishUpdate = new ChatResponseUpdate(null, new List<AIContent>());
            finishUpdate.FinishReason = MapFinishReason(chunk.FinishReason!);
            ApplyCommon(finishUpdate, responseId, chunk);
            yield return finishUpdate;
        }
    }

    /// <summary>宿主 finish 字符串 → M.E.AI ChatFinishReason。</summary>
    public static ChatFinishReason MapFinishReason(string finish)
        => finish.ToLowerInvariant() switch
        {
            "stop" => ChatFinishReason.Stop,
            "length" => ChatFinishReason.Length,
            "tool_calls" => ChatFinishReason.ToolCalls,
            "content_filter" => ChatFinishReason.ContentFilter,
            _ => new ChatFinishReason(finish)
        };

    private static void ApplyCommon(ChatResponseUpdate update, string? responseId, UnifiedStreamChunk chunk)
    {
        var id = responseId ?? chunk.Id;
        if (!string.IsNullOrEmpty(id)) update.ResponseId = id;
        if (!string.IsNullOrEmpty(chunk.Model)) update.ModelId = chunk.Model;
    }

    private static object CloneSchema(JsonElement schema)
        => schema.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? schema.Clone()
            : JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}");

    private static string ToUnifiedRole(ChatRole role)
    {
        if (role == ChatRole.User) return "user";
        if (role == ChatRole.Assistant) return "assistant";
        if (role == ChatRole.Tool) return "tool";
        return role.Value;
    }

 private static string? ExtractText(ChatMessage message)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var content in message.Contents)
        {
            if (content is TextContent { Text: { Length: > 0 } text })
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(text);
            }
            // TextReasoningContent / DataContent 等不进正文聚合（与宿主 vLLM 处理一致）
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static string SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments == null || arguments.Count == 0) return "{}";
        try
        {
            return JsonSerializer.Serialize(arguments, ArgsJsonOptions);
        }
        catch
        {
            return "{}";
        }
    }

    public static Dictionary<string, object?> ParseArguments(string? argsJson)
    {
        var dict = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(argsJson)) return dict;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.Clone();
            }
        }
        catch
        {
            // 参数非法：返回空字典，由工具端参数校验兜底报错
        }
        return dict;
    }
}

/// <summary>
/// 宿主 <c>IAIProvider</c> → M.E.AI <see cref="IChatClient"/> 适配器（PILOT-051 FR-2.2）。
/// 每次请求把 M.E.AI 消息/选项翻译为 <see cref="UnifiedChatRequest"/>，经 <c>IAIProvider.ChatStreamAsync</c>
/// 取流，再把 <see cref="UnifiedStreamChunk"/> 翻译回 <see cref="ChatResponseUpdate"/>。
/// </summary>
public sealed class ForgeChatClientAdapter : IChatClient
{
    private readonly IAIProvider _provider;
    private readonly string _modelId;

    public ForgeChatClientAdapter(IAIProvider provider, string modelId)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
    }

    /// <summary>上游模型 id（<c>provider:upstreamModelId</c> 的 upstream 段）。</summary>
    public string ModelId => _modelId;

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            updates.Add(update);
        }
        return updates.ToChatResponse();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var (unifiedMessages, systemFromMessages) = ForgeChatMessageMapper.ToUnifiedMessages(messageList);
        var systemPrompt = ForgeChatMessageMapper.RenderSystemPrompt(messageList, options);
        if (string.IsNullOrWhiteSpace(systemPrompt) && !string.IsNullOrWhiteSpace(systemFromMessages))
        {
            systemPrompt = systemFromMessages;
        }

        var request = new UnifiedChatRequest
        {
            Model = !string.IsNullOrWhiteSpace(options?.ModelId) ? options!.ModelId! : _modelId,
            Messages = unifiedMessages,
            Tools = ForgeChatMessageMapper.ToUnifiedTools(options),
            ToolChoice = ForgeChatMessageMapper.ToToolChoice(options),
            SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            Stream = true,
            StreamOptions = new UnifiedStreamOptions { IncludeUsage = true }
        };

        if (options?.Temperature is { } temperature)
        {
            request.Temperature = temperature;
        }
        if (options?.TopP is { } topP)
        {
            request.TopP = topP;
        }
        if (options?.MaxOutputTokens is { } maxTokens)
        {
            request.MaxTokens = maxTokens;
        }

        string? responseId = null;
        // PC-015（2026-10-29 缺陷修复）：OpenAI 流式工具调用只有首片带 Id/Name，续片只带参数增量。
        // 必须按调用聚合分片、流结束再整条产出；此前逐片产出 FunctionCallContent，续片变成空名调用
        // 并被 ForgeModelCallLogger 落库，下一次请求经 OpenAISdkAdapter.BuildChatMessages 构造
        // ChatToolCall(functionName:"") 直接抛 ArgumentException(functionName)，会话被永久毒化
        // （实例实证：mvt30ipk-quraxi 每次发言 1~2ms 内必败、响应长度 0）。
        // 聚合语义与 ReactLoopAgent.AccumulateToolCall（生产验证版）一致。
        var pendingToolCalls = new List<UnifiedToolCall>();
        await foreach (var chunk in _provider.ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(responseId) && !string.IsNullOrEmpty(chunk.Id))
            {
                responseId = chunk.Id;
            }
            if (chunk.DeltaToolCall != null)
            {
                AccumulateToolCall(pendingToolCalls, chunk.DeltaToolCall);
            }
            foreach (var update in ForgeChatMessageMapper.ToChatResponseUpdates(chunk, responseId))
            {
                yield return update;
            }
        }

        // 流结束：每个聚合完成的调用产出一条完整 FunctionCallContent（args 为分片拼接后的完整 JSON）。
        foreach (var call in pendingToolCalls)
        {
            if (string.IsNullOrWhiteSpace(call.Name))
            {
                continue; // 畸形调用（聚合后仍无名）：丢弃，空名不允许出门（PC-015）
            }
            var callUpdate = new ChatResponseUpdate(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(
                    string.IsNullOrEmpty(call.Id) ? $"call-{Guid.NewGuid():N}" : call.Id,
                    call.Name,
                    ForgeChatMessageMapper.ParseArguments(call.Arguments))
            });
            if (!string.IsNullOrEmpty(responseId)) callUpdate.ResponseId = responseId;
            yield return callUpdate;
        }
    }

    /// <summary>
    /// 累积流式工具调用分片：首片带 Id 视为新调用，续片（Id 空）向末位追加 name/args。
    /// 与 <c>ReactLoopAgent.AccumulateToolCall</c> 同语义（生产验证版）；已知残留限制：
    /// 平行调用分片交叉到达且续片无 Id 时按顺序归并（UnifiedToolCall 暂无 index 字段，与 ReactLoopAgent 同限）。
    /// </summary>
    private static void AccumulateToolCall(List<UnifiedToolCall> pending, UnifiedToolCall delta)
    {
        if (!string.IsNullOrEmpty(delta.Id))
        {
            pending.Add(new UnifiedToolCall { Id = delta.Id, Name = delta.Name, Arguments = delta.Arguments });
            return;
        }
        if (pending.Count == 0) return;
        var last = pending[^1];
        if (!string.IsNullOrEmpty(delta.Name)) last.Name += delta.Name;
        last.Arguments += delta.Arguments;
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose()
    {
        // provider 生命周期归宿主注册表所有，适配器不释放。
    }
}
