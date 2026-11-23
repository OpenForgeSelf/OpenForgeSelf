using System.Collections.Concurrent;
using System.Text;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
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
    private readonly IPlanGeneratorService? _planGenerator;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);

    public AgentFacadeRunExecutor(
        AgentFacadeService facade,
        IContext? ctx = null,
        IAIAgentService? agentService = null,
        IRunOrchestratorService? orchestrator = null,
        IChatModelResolver? modelResolver = null,
        IAgentRegistry? registry = null,
        IPlanGeneratorService? planGenerator = null)
    {
        _facade = facade;
        _ctx = ctx;
        _agentService = agentService;
        _orchestrator = orchestrator;
        _modelResolver = modelResolver;
        _registryOverride = registry;
        _planGenerator = planGenerator;
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

    /// <summary>
    /// planned 模式规划回合（灾后按意图重实现 · PC-040 批语义恢复）：
    /// 先经 LLM 真规划（PlanGeneratorService，submit_plan 闭环），成功 → plan.proposed 替换草稿卡；
    /// 规划器不可用 / 失败 / 超时 → 保留草稿计划并标注「规划失败，已用回退计划」。
    /// 静默降级禁令：任一回退路径必带 Warn 日志。
    /// </summary>
    public async Task StartPlanningAsync(string sessionId, string runId, CancellationToken cancellationToken)
    {
        var snap = _facade.GetExecution(sessionId, runId);
        if (snap == null) return;
        var generator = _planGenerator ?? _ctx?.Get<IPlanGeneratorService>();
        if (generator == null)
        {
            XTrace.Log.Warn("[AgentFacade] 规划器不可用（组合根未装配 planGenerator），session={0} run={1}", sessionId, runId);
            FallbackToDraft(sessionId, runId, snap);
            return;
        }

        try
        {
            var modelId = await ResolveModelAsync(snap.ModelId, cancellationToken).ConfigureAwait(false);
            // 规划用临时 Run：不落库；Id 取负随机，避免与编排器 scratch 会话（plan:{runId}）互撞。
            var probe = new AgentRun
            {
                Id = -Random.Shared.Next(1, int.MaxValue),
                AgentId = snap.AgentId ?? string.Empty,
                TaskInput = snap.Text
            };
            var plan = await generator.GeneratePlanAsync(probe, 0, modelId, cancellationToken).ConfigureAwait(false);
            if (plan == null || plan.Steps.Count == 0 || LooksLikeGeneratorFallback(plan))
            {
                XTrace.Log.Warn("[AgentFacade] 规划失败 session={0} run={1}: {2}", sessionId, runId, "规划器未返回有效计划");
                FallbackToDraft(sessionId, runId, snap);
                return;
            }
            _facade.UpdatePlannedPlan(sessionId, runId, ToPlanDto(plan));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 规划超时（非用户取消）：回退草稿 + note，不中断确认流程。
            XTrace.Log.Warn("[AgentFacade] 规划失败 session={0} run={1}: {2}", sessionId, runId, "规划回合超时");
            FallbackToDraft(sessionId, runId, snap);
        }
        catch (OperationCanceledException)
        {
            // 用户取消：Stop 已收束状态，不写回退。
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AgentFacade] 规划失败 session={0} run={1}: {2}", sessionId, runId, ex.Message);
            FallbackToDraft(sessionId, runId, snap);
        }
    }

    /// <summary>规划失败回退：保留既有草稿计划（无则单步）并标注，重新走 plan.proposed 供确认。</summary>
    private void FallbackToDraft(string sessionId, string runId, AgentRunExecutionSnapshot snap)
    {
        var dto = snap.Plan ?? new AgentPlanDto
        {
            Goal = snap.Text,
            Steps = { new AgentPlanStepDto { Id = "s1", Name = "完成整体任务", Objective = snap.Text } }
        };
        dto.Note = "规划失败，已用回退计划";
        _facade.UpdatePlannedPlan(sessionId, runId, dto);
    }

    /// <summary>识别 PlanGeneratorService 的内置兜底（单步「完成整体任务」）——按回退语义换用会话草稿并标注。</summary>
    private static bool LooksLikeGeneratorFallback(AgentPlan plan)
        => plan.Steps.Count == 1
           && string.Equals(plan.Steps[0].Name, "完成整体任务", StringComparison.Ordinal);

    private static AgentPlanDto ToPlanDto(AgentPlan plan)
    {
        var dto = new AgentPlanDto { Goal = plan.Goal ?? string.Empty };
        foreach (var st in plan.Steps)
        {
            dto.Steps.Add(new AgentPlanStepDto
            {
                Id = string.IsNullOrWhiteSpace(st.Id) ? $"s{dto.Steps.Count + 1}" : st.Id,
                Name = st.Name,
                Objective = st.Objective
            });
        }
        return dto;
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

        var tools = snap.ToolScope?.ResolvedAllow();
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
            var sawTool = false;
            var endAfterPostToolText = false;
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
                    sawTool = true;
                }
                else if (frame is ToolCompleted)
                    sawTool = true;

                if (frame is AssistantDelta delta)
                {
                    var piece = delta.Content ?? "";
                    if (ShouldAppendDelta(full, piece))
                        full.Append(piece);
                    else
                        continue;
                    if (sawTool && !string.IsNullOrWhiteSpace(piece))
                        endAfterPostToolText = true;
                }

                foreach (var mapped in AgentFacadeEventMapper.MapFrame(frame, toolResult, toolName))
                    _facade.Publish(sessionId, mapped.Type, mapped.Payload, runId);

                if (endAfterPostToolText && frame is StepCompleted)
                {
                    var textOut = CollapseRepeated(full.ToString());
                    if (!string.IsNullOrWhiteSpace(textOut))
                        _facade.Publish(sessionId, "message.completed", new { text = textOut }, runId);
                    _facade.Publish(sessionId, "run.completed", new { text = textOut }, runId);
                    try { await agent.CancelAsync(new ForgeSelf.Abstractions.AgentCancelCause.User()).ConfigureAwait(false); }
                    catch { /* 已收束，取消只为停掉后续空转 */ }
                    break;
                }

                if (frame is TurnCompleted completed)
                {
                    if (completed.Reason == TurnEndReason.Suspended)
                    {
                        await PublishPendingApprovalAsync(sessionId, runId, cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        var textOut = CollapseRepeated(full.ToString());
                        if (!string.IsNullOrWhiteSpace(textOut))
                            _facade.Publish(sessionId, "message.completed", new { text = textOut }, runId);
                        _facade.Publish(sessionId, "run.completed", new { text = textOut }, runId);
                    }
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


    private static bool ShouldAppendDelta(StringBuilder full, string piece)
    {
        if (string.IsNullOrEmpty(piece)) return false;
        var current = full.ToString();
        if (current.EndsWith(piece, StringComparison.Ordinal)) return false;
        const string done = "工具已执行完毕。";
        if (piece.Contains(done, StringComparison.Ordinal) && current.Contains(done, StringComparison.Ordinal))
            return false;
        return true;
    }

    private static string CollapseRepeated(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return t;
        const string done = "工具已执行完毕。";
        if (t.Contains(done, StringComparison.Ordinal))
        {
            var rest = t.Replace(done, "", StringComparison.Ordinal).Trim();
            if (rest.Length == 0) return done;
        }
        return t;
    }

    private async Task PublishPendingApprovalAsync(string sessionId, string runId, CancellationToken ct)
    {
        var approvals = _ctx?.Get<IApprovalService>();
        if (approvals == null) return;
        var list = await approvals.ListBySessionAsync(sessionId, ct).ConfigureAwait(false);
        var pending = list.LastOrDefault(a =>
            string.Equals(a.Status, nameof(ApprovalStatus.Pending), StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Status, "pending", StringComparison.OrdinalIgnoreCase));
        if (pending == null) return;
        var approvalId = pending.Id.ToString();
        _facade.Publish(sessionId, "approval.required", new
        {
            approvalId,
            id = approvalId,
            summary = string.IsNullOrWhiteSpace(pending.Description) ? $"需要确认工具 {pending.ToolName}" : pending.Description,
            action = pending.ToolName,
            toolName = pending.ToolName,
            args = pending.ArgsJson,
            risk = pending.RiskLevel,
            callId = pending.CallId
        }, runId);
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
