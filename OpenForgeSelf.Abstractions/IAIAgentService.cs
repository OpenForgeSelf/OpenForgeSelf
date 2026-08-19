namespace OpenForgeSelf.Abstractions;

/// <summary>
/// AI Agent 服务契约（实现：OpenForgeSelf.Backend.Plugins.AIAgent.Services.AIAgentService）。
/// 供 ScriptRunner 等兄弟插件按接口消费，避免直接依赖 AIAgent 插件程序集。
/// </summary>
public interface IAIAgentService
{
    Task<string> ChatAsync(List<AIChatMessage> messages, bool enableTools = true);
    IAsyncEnumerable<string> ChatStreamAsync(List<AIChatMessage> messages, bool enableTools = true, CancellationToken cancellationToken = default);
    Task<List<AIChatMessage>> ChatWithToolsAsync(List<AIChatMessage> messages, CancellationToken cancellationToken = default);
    Task<List<WorkflowRecommendationDto>> GetRecommendedWorkflowsAsync(string userMessage, int limit = 5);
    Task<GenerateScriptResponse> GenerateScriptAsync(string language, string description, string? requirements = null);
    Task<AnalyzeScriptErrorResponse> AnalyzeScriptErrorAsync(string language, string code, string errorMessage);
    Task<SuggestScriptFixResponse> SuggestScriptFixAsync(string language, string code, string errorMessage);
    Task<List<ScriptTemplate>> GetScriptTemplatesAsync(string? category = null);
}

/// <summary>
/// AI 对话消息（纯 DTO，随 IAIAgentService 契约迁入 Abstractions）。
/// 注意与 AIAgent 插件数据库实体（OpenForgeSelf.Backend.Plugins.AIAgent.Entities.AIChatMessage）、
/// 宿主旧模型（OpenForgeSelf.Backend.Models.AIChatMessage）区分。
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
