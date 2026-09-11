using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.AIAgent.Models;

public class ChatMessageModel
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    /// <summary>工具调用轨迹（FreeLoop 执行记录，031 方案A），JSON 数组，空 = 无工具调用。</summary>
    public string ToolCallsJson { get; set; } = string.Empty;
}

public class ChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Stream { get; set; } = true;

    /// <summary>聊天模型 id（形如 provider:upstreamModelId）；空则回退默认 provider（向后兼容）。</summary>
    public string? ChatModelId { get; set; }

    /// <summary>选中 Agent id；命中则注入其 SystemPrompt，空则不注入（向后兼容）。</summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// 本会话启用工具名白名单（来自 composer 🔧 多选）。
    /// 空/未传 = 默认全挂（本插件 + memory-system 共 13 个，向后兼容）；非空 = 仅启用列表内工具（仍限白名单插件）。
    /// </summary>
    public List<string>? EnabledToolNames { get; set; }

    /// <summary>
    /// 本会话启用技能 id 列表（来自 composer ⚡ 多选，形如 <c>agents:.agents/skills/&lt;name&gt;/SKILL.md</c>）。
    /// 命中后把技能的 名称 + 描述 + 相对路径 注入 system prompt，供 Agent 按技能工作。
    /// </summary>
    public List<string>? SkillIds { get; set; }
}

public class ChatResponse
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }

    /// <summary>本次回复触发的工具调用（非流式返回时携带；流式经 SSE 事件下发）。</summary>
    public List<string>? ToolCalls { get; set; }

    /// <summary>工具调用轨迹（FreeLoop 执行记录，031 方案A）：name/args/result/success/durationMs 的 JSON 数组；
    /// 历史消息返回时携带，供前端渲染「刷新后仍可见」的工具调用卡片。</summary>
    public string? ToolCallsJson { get; set; }

    /// <summary>本次回复的 token 用量（上游返回时携带）。</summary>
    public UnifiedUsage? Usage { get; set; }
}

/// <summary>单条工具调用轨迹项（FreeLoop 落库用，与前端 ToolEvent 形状对齐：camelCase name/args/result/success）。</summary>
public class ChatToolCallTrace
{
    /// <summary>工具名。</summary>
    public string? Name { get; set; }
    /// <summary>调用参数（JSON 字符串）。</summary>
    public string? Args { get; set; }
    /// <summary>工具返回结果摘要。</summary>
    public string? Result { get; set; }
    /// <summary>执行是否成功。</summary>
    public bool Success { get; set; } = true;
    /// <summary>耗时（毫秒）。</summary>
    public long DurationMs { get; set; }
}

public class AIChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<AIChatMessage> Messages { get; set; } = new();
    public bool Stream { get; set; }
    public List<AIToolDefinition>? Tools { get; set; }
    public string? ToolChoice { get; set; }
}

public class AIChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<AIChatChoice> Choices { get; set; } = new();
}

public class AIChatChoice
{
    public int Index { get; set; }
    public AIChatMessageDelta? Message { get; set; }
    public AIChatMessageDelta? Delta { get; set; }
    public string FinishReason { get; set; } = string.Empty;
}

public class AIChatMessageDelta
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<AIToolCall>? ToolCalls { get; set; }
}
