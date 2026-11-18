using ForgeSelf.Api.Plugins.AIAgent.Models;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>统一 Agent 能力入口（会话、事件、介入）。</summary>
public interface IAgentFacadeService
{
    AgentSessionSummaryDto CreateSession(CreateAgentSessionRequest request);
    IReadOnlyList<AgentSessionSummaryDto> ListSessions(string pluginId, string? surfaceKey);
    AgentSessionDetailDto? GetSession(string sessionId);
    AgentSessionSummaryDto? PatchSession(string sessionId, PatchAgentSessionRequest request);
    bool DeleteSession(string sessionId);
    (int Status, object Body) PostMessage(string sessionId, PostAgentMessageRequest request);
    IReadOnlyList<AgentEventDto> ReplayEvents(string sessionId, long after);
    Task<AgentEventDto?> WaitEventAsync(string sessionId, long after, CancellationToken cancellationToken);
    object? ConfirmPlan(string sessionId);
    object? RevisePlan(string sessionId, RevisePlanRequest request);
    object? DecideApproval(string sessionId, string approvalId, DecideApprovalRequest request);
    object? StopRun(string sessionId, string runId);
    object? Intervene(string sessionId, string runId, InterveneRunRequest request);
    object? PauseWorkflow(string sessionId, string workflowId);
    object? CancelWorkflow(string sessionId, string workflowId);
    object? GetWorkspace(string sessionId, string? panel);
    object? WriteMemory(string sessionId, WorkspaceWritebackRequest request);
    object? CommitDesign(string sessionId, WorkspaceWritebackRequest request);
    object? DispatchTodo(string sessionId, string todoId);
    object? SaveSnippet(string sessionId, WorkspaceWritebackRequest request);
    IReadOnlyList<CatalogModelDto> ListModels();
    IReadOnlyList<CatalogAgentDto> ListAgents();
}

/// <summary>可选的真实运行时适配。缺省时状态机仍可用，确认前不会开跑。</summary>
public interface IAgentRunExecutor
{
    Task StartOpenAsync(string sessionId, string runId, CancellationToken cancellationToken);
    Task StartPlanAsync(string sessionId, string runId, CancellationToken cancellationToken);
    Task ResumeAsync(string sessionId, string runId, string action, CancellationToken cancellationToken);
    Task StopAsync(string sessionId, string runId);
}
