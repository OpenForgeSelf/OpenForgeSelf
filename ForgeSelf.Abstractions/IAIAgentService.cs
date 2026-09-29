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
    /// Agent 工具循环核心（会话版，B4/040 写路径改序 + B5/041 turn/step 状态机）：
    /// 以 <paramref name="sessionId"/> 的会话日志为真相源 —— 模型输入只从日志派生，
    /// 循环内的 turn/step、助手产出（含中间迭代）、工具调用与结果<b>全部由状态机逐条落日志</b>。
    /// </summary>
    /// <remarks>
    /// B5（041）：旧的「消息列表版」重载已删除（它被 IM 网关当成会话主路径、绕过会话日志，QA 定性为旁路）。
    /// 无会话身份的一次性场景：B7 起 ad-hoc 循环已收口为私有核心（IChatCompletion 旧面），
    /// 新场景走 <see cref="CreateAgent"/> + <see cref="AgentOptions.ExitToolNames"/> 出口语义；
    /// 本方法不再要求调用方补落助手消息（助手产出由状态机落 <c>assistant/message</c>）。
    /// </remarks>
    /// <param name="sessionId">会话 ID（会话日志主键；本重载要求宿主已提供 <see cref="ISessionStore"/> 契约）。</param>
    /// <param name="chatModelId">聊天模型 id（形如 provider:upstreamModelId）；空则回退默认 provider。</param>
    /// <param name="agentId">选中 Agent id；命中则注入其 SystemPrompt，空则不注入。</param>
    /// <param name="enabledToolNames">本会话启用工具名白名单；空/未传 = 默认全挂（向后兼容）。</param>
    /// <param name="skillIds">本会话启用技能 id 列表；命中后把技能元信息注入 system prompt。</param>
    /// <param name="enableTools">是否挂载工具。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="extraTools">额外显式挂载的工具（计划驱动运行流特殊工具，不随白名单过滤）。</param>
    IAsyncEnumerable<AgentLoopEvent> RunAgentLoopAsync(string sessionId, string? chatModelId = null, string? agentId = null, List<string>? enabledToolNames = null, List<string>? skillIds = null, bool enableTools = true, CancellationToken cancellationToken = default, List<IToolFunctionExtension>? extraTools = null);

    /// <summary>
    /// 构建 Agent 运行选项（模型 id / 已渲染系统提示（人设 + 技能 + 关联工作流）/ 工具白名单）。
    /// 供控制器在 <see cref="IAgentRegistry.GetOrCreateAsync"/> 前把请求参数翻译成 Agent 选项。
    /// </summary>
    /// <remarks>
    /// B7（两套循环统一）：旧的 <c>RunAdHocLoopAsync</c>（无会话一次性循环）已从接口删除——
    /// 其两个 029 调用方（工作流规划 / 计划驱动步骤执行）已迁入统一状态机路径
    /// （scratch-session + 出口工具语义）；无会话一次性能力收敛为 <see cref="AIAgentService"/>
    /// 私有 ad-hoc 核心，仅供 <c>IChatCompletion</c> 旧面（ChatAsync/ChatStreamAsync/ChatWithToolsAsync）
    /// 使用（B6 定性合法例外，见 B7 报告）。新的无会话结构化产出一律走
    /// <see cref="CreateAgent"/> + <see cref="AgentOptions.ExitToolNames"/> 出口语义。
    /// </remarks>
    AgentOptions BuildAgentOptions(string? chatModelId = null, string? agentId = null, List<string>? enabledToolNames = null, List<string>? skillIds = null);

    /// <summary>
    /// 创建会话 Agent（turn/step 状态机本体）：解析 provider / 工具 schema / 模型 id 并装配运行期依赖。
    /// 会话主路径推荐走 <see cref="IAgentRegistry"/>（跨请求复用）；本方法用于一次性运行与注册表工厂接线。
    /// </summary>
    IAgent CreateAgent(string sessionId, AgentOptions options);

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
