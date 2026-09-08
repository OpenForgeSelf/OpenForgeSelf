namespace ForgeSelf.Abstractions;

/// <summary>
/// AI Agent 服务契约（实现：ForgeSelf.Api.Plugins.AIAgent.Services.AIAgentService）。
/// 供 ScriptRunner 等兄弟插件按接口消费，避免直接依赖 AIAgent 插件程序集。
/// </summary>
public interface IAIAgentService
{
    Task<string> ChatAsync(List<AIChatMessage> messages, bool enableTools = true);
    IAsyncEnumerable<string> ChatStreamAsync(List<AIChatMessage> messages, bool enableTools = true, CancellationToken cancellationToken = default);
    Task<List<AIChatMessage>> ChatWithToolsAsync(List<AIChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agent 工具循环核心（流式 + 工具调用可见）。逐个产出 <see cref="AgentLoopEvent"/>；
    /// 直接对传入的 <paramref name="messages"/> 追加 assistant/tool 消息（调用方如需隔离请传副本）。
    /// </summary>
    /// <param name="messages">对话消息（会被追加 assistant/tool 消息）。</param>
    /// <param name="chatModelId">聊天模型 id（形如 provider:upstreamModelId）；空则回退默认 provider。</param>
    /// <param name="agentId">选中 Agent id；命中则注入其 SystemPrompt，空则不注入。</param>
    /// <param name="enableTools">是否挂载工具。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    IAsyncEnumerable<AgentLoopEvent> RunAgentLoopAsync(List<AIChatMessage> messages, string? chatModelId = null, string? agentId = null, bool enableTools = true, CancellationToken cancellationToken = default);

    Task<List<WorkflowRecommendationDto>> GetRecommendedWorkflowsAsync(string userMessage, int limit = 5);
    Task<GenerateScriptResponse> GenerateScriptAsync(string language, string description, string? requirements = null);
    Task<AnalyzeScriptErrorResponse> AnalyzeScriptErrorAsync(string language, string code, string errorMessage);
    Task<SuggestScriptFixResponse> SuggestScriptFixAsync(string language, string code, string errorMessage);
    Task<List<ScriptTemplate>> GetScriptTemplatesAsync(string? category = null);
}

/// <summary>
/// AI 对话消息（纯 DTO，随 IAIAgentService 契约迁入 Abstractions）。
/// 注意与 AIAgent 插件数据库实体（ForgeSelf.Api.Plugins.AIAgent.Entities.AIChatMessage）、
/// 宿主旧模型（ForgeSelf.Api.Models.AIChatMessage）区分。
/// </summary>
public class AIChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<AIToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// AI 工具调用（纯 DTO）。
/// </summary>
public class AIToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public AIFunctionCall Function { get; set; } = new();
    public int? Index { get; set; }
}

/// <summary>
/// AI 函数调用参数（纯 DTO）。
/// </summary>
public class AIFunctionCall
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}
