using System.Collections.Concurrent;
using System.Text;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 把开放对话（IAgent 状态机）与已确认计划（编排器）接到同一条 /api/agent 事件流。
/// </summary>
public sealed class AgentFacadeRunExecutor : IAgentRunExecutor
{
    private readonly AgentFacadeService _facade;
    private readonly IContext? _ctx;
    private readonly IAIAgentService? _agentService;
    private readonly IRunOrchestratorService? _orchestrator;
    private readonly IChatModelResolver? _modelResolver;
    private readonly IAgentRegistry? _registryOverride;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);

    public AgentFacadeRunExecutor(
        AgentFacadeService facade,
        IContext? ctx = null,
        IAIAgentService? agentService = null,
        IRunOrchestratorService? orchestrator = null,
        IChatModelResolver? modelResolver = null,
        IAgentRegistry? registry = null)
    {
        _facade = facade;
        _ctx = ctx;
        _agentService = agentService;
        _orchestrator = orchestrator;
        _modelResolver = modelResolver;
        _registryOverride = registry;
    }

    public Task StartOpenAsync(string sessionId, string runId, CancellationToken cancellationToken)
        => DriveOpenAsync(sessionId, runId, followup: null, cancellationToken);

    public async Task StartPlanAsync(string sessionId, string runId, CancellationToken cancellationToken)
    {
        var snap = _facade.GetExecution(sessionId, runId);
        if (snap == null)
        {
            _facade.Publish(sessionId, "error", new { message = "执行不存在" }, runId);
            return;
        }

        var orch = _orchestrator ?? _ctx?.Get<IRunOrchestratorService>();
        if (orch == null)
        {
            _facade.Publish(sessionId, "error", new { message = "编排服务不可用，无法执行已确认的计划" }, runId);
            _facade.Publish(sessionId, "run.failed", new { message = "编排服务不可用" }, runId);
            return;
        }

        var plan = ToRuntimePlan(snap.Plan);
        if (plan.Steps.Count == 0)
        {
            _facade.Publish(sessionId, "error", new { message = "执行计划为空" }, runId);
            _facade.Publish(sessionId, "run.failed", new { message = "执行计划为空" }, runId);
            return;
        }

        ApplyWorkspace(snap.Cwd);
        var modelId = await ResolveModelAsync(snap.ModelId, cancellationToken).ConfigureAwait(false);
        if (modelId == null && _modelResolver != null)
        {
            const string msg = "未找到可用对话模型：请到「AI 模型」设置启用至少一个对话模型后再试。";
            _facade.Publish(sessionId, "error", new { message = msg }, runId);
            _facade.Publish(sessionId, "run.failed", new { message = msg }, runId);
            return;
        }

        var cts = AttachCancel(sessionId, runId, cancellationToken);
        try
        {
            var request = new RunRequest
            {
                SessionId = sessionId,
                AgentId = snap.AgentId ?? "",
                TaskInput = snap.Text,
                ChatModelId = modelId
            };
            await foreach (var ev in orch.ExecuteConfirmedPlanAsync(request, plan, cts.Token).ConfigureAwait(false))
            {
                if (cts.IsCancellationRequested) break;
                foreach (var mapped in AgentFacadeEventMapper.MapLoopEvent(ev))
                {
                    if (mapped.NativeRunId is > 0)
                        _facade.BindNativeRunId(sessionId, runId, mapped.NativeRunId.Value);
                    _facade.Publish(sessionId, mapped.Type, mapped.Payload, runId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stop 已写 run.cancelled
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AgentFacade] 计划执行失败 session={0} run={1}: {2}", sessionId, runId, ex.Message);
            _facade.Publish(sessionId, "error", new { message = ex.Message }, runId);
            _facade.Publish(sessionId, "run.failed", new { message = ex.Message }, runId);
        }
        finally
        {
            DetachCancel(sessionId, runId, cts);
        }
    }

    public Task ResumeAsync(string sessionId, string runId, string action, CancellationToken cancellationToken)
    {
        var snap = _facade.GetExecution(sessionId, runId);
        if (snap == null) return Task.CompletedTask;
        if (string.Equals(snap.Mode, "planned", StringComparison.OrdinalIgnoreCase) && snap.NativeRunId is > 0)
            return ResumePlanAsync(sessionId, runId, snap, action, cancellationToken);
        var followup = string.IsNullOrWhiteSpace(snap.ContinueInput) ? "请继续。" : snap.ContinueInput!;
        return DriveOpenAsync(sessionId, runId, followup, cancellationToken);
    }

    public async Task StopAsync(string sessionId, string runId)
    {
        if (_runs.TryGetValue(Key(sessionId, runId), out var cts))
        {
            try { cts.Cancel(); } catch { /* 忽略重复取消 */ }
        }

        var snap = _facade.GetExecution(sessionId, runId);
        if (snap?.NativeRunId is > 0)
        {
            var orch = _orchestrator ?? _ctx?.Get<IRunOrchestratorService>();
            if (orch != null)
            {
                try { await orch.CancelAsync(snap.NativeRunId.Value).ConfigureAwait(false); }
                catch (Exception ex) { XTrace.Log.Warn("[AgentFacade] 取消编排失败: {0}", ex.Message); }
            }
        }

        var registry = _registryOverride ?? _ctx?.Get<IAgentRegistry>();
        var agent = registry == null ? null : await registry.TryGetAsync(sessionId).ConfigureAwait(false);
        if (agent != null)
        {
            try { await agent.CancelAsync(new AgentCancelCause.User()).ConfigureAwait(false); }
            catch (Exception ex) { XTrace.Log.Warn("[AgentFacade] 取消对话失败: {0}", ex.Message); }
        }
    }

    private async Task DriveOpenAsync(string sessionId, string runId, string? followup, CancellationToken cancellationToken)
    {
        var snap = _facade.GetExecution(sessionId, runId);
        if (snap == null)
        {
            _facade.Publish(sessionId, "error", new { message = "执行不存在" }, runId);
            return;
        }

        var registry = _registryOverride ?? _ctx?.Get<IAgentRegistry>();
        var agentService = _agentService ?? _ctx?.Get<IAIAgentService>();
        if (registry == null || agentService == null)
        {
            const string msg = "运行时不可用，无法开始对话";
            _facade.Publish(sessionId, "error", new { message = msg }, runId);
            _facade.Publish(sessionId, "run.failed", new { message = msg }, runId);
            return;
        }

        ApplyWorkspace(snap.Cwd);
        var modelId = await ResolveModelAsync(snap.ModelId, cancellationToken).ConfigureAwait(false);
        if (modelId == null && _modelResolver != null)
        {
            const string msg = "未找到可用对话模型：请到「AI 模型」设置启用至少一个对话模型后再试。";
            _facade.Publish(sessionId, "error", new { message = msg }, runId);
            _facade.Publish(sessionId, "run.failed", new { message = msg }, runId);
            return;
        }

        var tools = snap.ToolScope?.AllowTools;
        var options = agentService.BuildAgentOptions(modelId, snap.AgentId, tools);
        var cts = AttachCancel(sessionId, runId, cancellationToken);
        try
        {
            var agent = await registry.GetOrCreateAsync(sessionId, options, cts.Token).ConfigureAwait(false);
            if (agent.Status == ForgeSelf.Abstractions.AgentStatus.Running)
            {
                _facade.Publish(sessionId, "error", new { message = "该会话上一轮仍在进行" }, runId);
                return;
            }

            var text = followup ?? snap.Text;
            if (!string.IsNullOrWhiteSpace(text))
                agent.Inbox.Followup(sessionId, text);

            var full = new StringBuilder();
            var store = _ctx?.Get<ISessionStore>();
            await foreach (var frame in agent.RunAsync(cts.Token).ConfigureAwait(false))
            {
                string? toolResult = null;
                string? toolName = null;
                if (frame is ToolCompleted done && store != null)
                {
                    var last = store.Replay(sessionId)
                        .OfType<ToolResultEvent>()
                        .LastOrDefault(r => string.Equals(r.CallId, done.CallId, StringComparison.Ordinal));
                    toolResult = last?.ResultJson;
                    toolName = last?.ToolName;
                }

                if (frame is AssistantDelta delta)
                    full.Append(delta.Content);

                foreach (var mapped in AgentFacadeEventMapper.MapFrame(frame, toolResult, toolName))
                    _facade.Publish(sessionId, mapped.Type, mapped.Payload, runId);

                if (frame is TurnCompleted)
                {
                    var textOut = full.ToString();
                    if (!string.IsNullOrWhiteSpace(textOut))
                        _facade.Publish(sessionId, "message.completed", new { text = textOut }, runId);
                    _facade.Publish(sessionId, "run.completed", new { text = textOut }, runId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stop 已写 run.cancelled
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AgentFacade] 开放对话失败 session={0} run={1}: {2}", sessionId, runId, ex.Message);
            _facade.Publish(sessionId, "error", new { message = ex.Message }, runId);
            _facade.Publish(sessionId, "run.failed", new { message = ex.Message }, runId);
        }
        finally
        {
            DetachCancel(sessionId, runId, cts);
        }
    }

    private async Task ResumePlanAsync(string sessionId, string runId, AgentRunExecutionSnapshot snap, string action, CancellationToken cancellationToken)
    {
        var orch = _orchestrator ?? _ctx?.Get<IRunOrchestratorService>();
        if (orch == null) return;
        var cts = AttachCancel(sessionId, runId, cancellationToken);
        try
        {
            var nativeId = snap.NativeRunId!.Value;
            if (action is "skip" or "override")
            {
                var native = await orch.GetRunAsync(nativeId).ConfigureAwait(false);
                var index = snap.InterveneStepIndex
                            ?? native?.CurrentStepIndex
                            ?? ResolveStepIndex(snap.Plan);
                await orch.InterveneAsync(nativeId, index, new InterveneRequest
                {
                    Action = action,
                    Output = snap.ContinueInput,
                    Note = action
                }).ConfigureAwait(false);
            }

            await foreach (var ev in orch.ResumeAsync(nativeId, cts.Token).ConfigureAwait(false))
            {
                if (cts.IsCancellationRequested) break;
                foreach (var mapped in AgentFacadeEventMapper.MapLoopEvent(ev))
                {
                    if (mapped.NativeRunId is > 0)
                        _facade.BindNativeRunId(sessionId, runId, mapped.NativeRunId.Value);
                    _facade.Publish(sessionId, mapped.Type, mapped.Payload, runId);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AgentFacade] 续跑失败 session={0} run={1}: {2}", sessionId, runId, ex.Message);
            _facade.Publish(sessionId, "error", new { message = ex.Message }, runId);
            _facade.Publish(sessionId, "run.failed", new { message = ex.Message }, runId);
        }
        finally
        {
            DetachCancel(sessionId, runId, cts);
        }
    }

    private async Task<string?> ResolveModelAsync(string? requested, CancellationToken ct)
    {
        var resolver = _modelResolver ?? _ctx?.Get<IChatModelResolver>();
        if (resolver == null) return requested;
        return await resolver.ResolveAsync(requested, ct).ConfigureAwait(false);
    }

    private void ApplyWorkspace(string? cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return;
        var workspace = _ctx?.Get<IProjectWorkspaceService>();
        if (workspace == null) return;
        if (!workspace.TrySetProjectRoot(cwd, out var error))
            XTrace.Log.Warn("[AgentFacade] 工作目录不可用 {0}: {1}", cwd, error);
    }

    private CancellationTokenSource AttachCancel(string sessionId, string runId, CancellationToken external)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(external);
        _runs[Key(sessionId, runId)] = cts;
        return cts;
    }

    private void DetachCancel(string sessionId, string runId, CancellationTokenSource cts)
    {
        _runs.TryRemove(Key(sessionId, runId), out _);
        cts.Dispose();
    }

    private static string Key(string sessionId, string runId) => sessionId + ":" + runId;

    private static int ResolveStepIndex(AgentPlanDto? plan)
    {
        if (plan?.Steps == null) return 0;
        var idx = plan.Steps.FindIndex(s => s.Status is "stuck" or "running");
        if (idx < 0) idx = plan.Steps.FindIndex(s => s.Status is "pending");
        return idx < 0 ? 0 : idx;
    }

    private static AgentPlan ToRuntimePlan(AgentPlanDto? dto)
    {
        var plan = new AgentPlan { Goal = dto?.Goal ?? "" };
        if (dto?.Steps == null) return plan;
        foreach (var step in dto.Steps)
        {
            plan.Steps.Add(new AgentPlanStep
            {
                Id = string.IsNullOrWhiteSpace(step.Id) ? Guid.NewGuid().ToString("N")[..8] : step.Id,
                Name = step.Name,
                Objective = string.IsNullOrWhiteSpace(step.Objective) ? step.Name : step.Objective
            });
        }
        return plan;
    }
}
