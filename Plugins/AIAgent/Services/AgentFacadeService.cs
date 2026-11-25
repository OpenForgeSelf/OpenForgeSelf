using System.Collections.Concurrent;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.AIAgent.Models;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// /api/agent 会话与事件实现：按 pluginId + surfaceKey 隔离，发送与订阅分离，计划确认前不执行。
/// </summary>
public sealed class AgentFacadeService : IAgentFacadeService
{
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);
    private IAgentRunExecutor? _executor;
    private readonly IContext? _ctx;
    private readonly IAgentRegistryService? _agents;

    public AgentFacadeService(IAgentRunExecutor? executor = null, IContext? ctx = null, IAgentRegistryService? agents = null)
    {
        _executor = executor;
        _ctx = ctx;
        _agents = agents;
    }

    public void AttachExecutor(IAgentRunExecutor executor) => _executor = executor;

    public AgentSessionSummaryDto CreateSession(CreateAgentSessionRequest request)
    {
        var context = request.Embed?.ResolvedContext() ?? new AgentEmbedContext();
        if (string.IsNullOrWhiteSpace(context.PluginId))
            throw new ArgumentException("pluginId 不能为空");

        var mode = NormalizeMode(request.Mode);
        var surfaceKey = context.ResolvedSurfaceKey();
        var now = DateTimeOffset.Now;
        var state = new SessionState
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(request.Title) ? "新会话" : request.Title.Trim(),
            Mode = mode,
            Status = "idle",
            PluginId = context.PluginId.Trim(),
            SurfaceKey = surfaceKey,
            AgentId = request.AgentId,
            ModelId = request.ModelId,
            Surface = string.IsNullOrWhiteSpace(request.Embed?.Surface) ? "page" : request.Embed!.Surface!.Trim(),
            Context = CloneContext(context, context.PluginId.Trim(), surfaceKey),
            UpdatedAt = now
        };
        _sessions[state.Id] = state;
        return ToSummary(state);
    }

    public IReadOnlyList<AgentSessionSummaryDto> ListSessions(string pluginId, string? surfaceKey)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("pluginId 不能为空");
        var key = string.IsNullOrWhiteSpace(surfaceKey) ? AgentSurfaceKeys.Default : surfaceKey.Trim();
        return _sessions.Values
            .Where(s => !s.Deleted
                        && string.Equals(s.PluginId, pluginId.Trim(), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(s.SurfaceKey, key, StringComparison.Ordinal))
            .OrderByDescending(s => s.UpdatedAt)
            .Select(ToSummary)
            .ToList();
    }

    public AgentSessionDetailDto? GetSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        return new AgentSessionDetailDto
        {
            Session = ToSummary(s),
            Messages = s.Messages.ToList(),
            CurrentRun = ToRunDto(s.CurrentRun),
            Workspace = BuildWorkspace(s)
        };
    }

    public AgentSessionSummaryDto? PatchSession(string sessionId, PatchAgentSessionRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        if (IsBusy(s.Status) && !string.IsNullOrWhiteSpace(request.Mode) && NormalizeMode(request.Mode) != s.Mode)
            throw new InvalidOperationException("进行中不可切换模式");
        if (!string.IsNullOrWhiteSpace(request.Mode)) s.Mode = NormalizeMode(request.Mode);
        if (request.AgentId != null) s.AgentId = request.AgentId;
        if (request.ModelId != null) s.ModelId = request.ModelId;
        if (!string.IsNullOrWhiteSpace(request.Title)) s.Title = request.Title.Trim();
        Touch(s);
        return ToSummary(s);
    }

    public bool DeleteSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return false;
        s.Deleted = true;
        s.Gate.Release();
        return true;
    }

    public (int Status, object Body) PostMessage(string sessionId, PostAgentMessageRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted)
            return (404, new { error = "会话不存在" });
        if (string.IsNullOrWhiteSpace(request.Text))
            return (400, new { error = "text 不能为空" });
        if (IsBusy(s.Status))
            return (409, new { error = "该会话上一轮仍在进行" });

        var incoming = request.Embed?.ResolvedContext();
        if (incoming != null)
        {
            if (!string.IsNullOrWhiteSpace(incoming.PluginId)
                && !incoming.PluginId.Trim().Equals(s.PluginId, StringComparison.OrdinalIgnoreCase))
                return (400, new { error = "不能把会话改到其它 pluginId" });
            if (!string.IsNullOrWhiteSpace(incoming.SurfaceKey)
                && !incoming.ResolvedSurfaceKey().Equals(s.SurfaceKey, StringComparison.Ordinal))
                return (400, new { error = "不能把会话改到其它 surfaceKey" });
        }

        var run = new RunState
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = s.Mode == "planned" ? "planning" : "running",
            ToolScope = request.Embed?.ToolScope,
            ContextSnapshot = CloneContext(request.Embed?.ResolvedContext() ?? s.Context, s.PluginId, s.SurfaceKey),
            Attachments = request.Attachments ?? new List<AgentAttachment>(),
            Text = request.Text.Trim(),
            Turn = 0,
            TurnMax = 20
        };
        s.CurrentRun = run;
        s.Messages.Add(new AgentMessageDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Role = "user",
            Content = run.Text,
            CreatedAt = DateTimeOffset.Now
        });

        ApplyWorkspaceSeeds(s, run);
        PullRelevantMemories(s, run.Text);

        if (s.Mode == "planned")
        {
            run.Plan = BuildDraftPlan(run.Text);
            s.Status = "awaiting_plan";
            run.Status = "awaiting_plan";
            Touch(s);
            Append(s, "session.status", run.Id, new { status = s.Status });
            Append(s, "plan.proposed", run.Id, run.Plan);
            Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
            // 规划回合（灾后按意图重实现）：先经 LLM 真规划，成功→plan.proposed 替换草稿；失败→回退草稿+标注。
            _ = SafeStart(() => _executor?.StartPlanningAsync(s.Id, run.Id, CancellationToken.None) ?? Task.CompletedTask);
            return (202, new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id });
        }

        if (TryFailByKeyword(s, run))
            return (202, new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id });
        if (TryHoldRunningByKeyword(s, run))
            return (202, new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id });
        if (TryRequireWorkflowApprovalByKeyword(s, run))
            return (202, new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id });

        s.Status = "running";
        Touch(s);
        Append(s, "session.status", run.Id, new { status = s.Status });
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        _ = SafeStart(() => _executor?.StartOpenAsync(s.Id, run.Id, CancellationToken.None) ?? Task.CompletedTask);
        return (202, new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id });
    }

    public IReadOnlyList<AgentEventDto> ReplayEvents(string sessionId, long after)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted)
            return Array.Empty<AgentEventDto>();
        lock (s.Events)
            return s.Events.Where(e => e.Seq > after).ToList();
    }

    public async Task<AgentEventDto?> WaitEventAsync(string sessionId, long after, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        while (!cancellationToken.IsCancellationRequested)
        {
            lock (s.Events)
            {
                var next = s.Events.FirstOrDefault(e => e.Seq > after);
                if (next != null) return next;
            }
            try
            {
                await s.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
        return null;
    }

    public object? ConfirmPlan(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var run = s.CurrentRun;
        if (run == null || s.Status != "awaiting_plan")
            return new { error = "当前没有待确认的计划" };
        run.Status = "running";
        s.Status = "running";
        if (run.Plan != null)
        {
            foreach (var step in run.Plan.Steps)
                if (step.Status == "pending") { /* keep */ }
        }
        Touch(s);
        Append(s, "session.status", run.Id, new { status = s.Status });
        Append(s, "plan.updated", run.Id, run.Plan);
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        if (TryFailByKeyword(s, run))
            return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
        if (TryHoldRunningByKeyword(s, run))
            return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
        if (TryRequireWorkflowApprovalByKeyword(s, run))
            return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
        _ = SafeStart(() => _executor?.StartPlanAsync(s.Id, run.Id, CancellationToken.None) ?? Task.CompletedTask);
        return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
    }

    /// <summary>
    /// 规划回合产出回写待确认计划（真规划或回退草稿+note）。仅 awaiting_plan 且 run 匹配时生效；
    /// 用户已确认/取消/停止则静默忽略，避免覆盖已进入执行的轮次。
    /// </summary>
    public bool UpdatePlannedPlan(string sessionId, string runId, AgentPlanDto plan)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return false;
        var run = s.CurrentRun;
        if (run == null || !string.Equals(run.Id, runId, StringComparison.Ordinal)) return false;
        if (!string.Equals(s.Status, "awaiting_plan", StringComparison.Ordinal)) return false;
        run.Plan = plan;
        Touch(s);
        Append(s, "plan.proposed", run.Id, plan);
        return true;
    }

    public object? RevisePlan(string sessionId, RevisePlanRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var run = s.CurrentRun;
        if (run == null || s.Status != "awaiting_plan")
            return new { error = "当前没有可修改的计划" };
        run.Plan ??= new AgentPlanDto();
        if (request.Steps is { Count: > 0 })
            run.Plan.Steps = request.Steps;
        var reviseText = request.ResolvedText();
        if (!string.IsNullOrWhiteSpace(reviseText))
            run.Plan.Goal = reviseText.Trim();
        Touch(s);
        Append(s, "plan.updated", run.Id, run.Plan);
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        return run.Plan;
    }

    public object? DecideApproval(string sessionId, string approvalId, DecideApprovalRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var run = s.CurrentRun;
        if (run == null) return new { error = "没有进行中的任务" };
        var item = run.Approvals.FirstOrDefault(a => a.Id == approvalId);
        if (item == null) return new { error = "审批不存在" };
        if (item.Status != "pending") return new { error = "该审批已处理" };
        item.Status = request.Approved ? "approved" : "rejected";
        item.Note = request.Note;
        if (long.TryParse(item.Id, out var nativeId))
        {
            var approvals = _ctx?.Get<IApprovalService>();
            if (approvals != null)
            {
                try
                {
                    var dto = approvals.DecideAsync(nativeId, request.Approved, request.Note).GetAwaiter().GetResult();
                    if (dto == null) return new { error = "审批不存在" };
                }
                catch (Exception ex)
                {
                    return new { error = ex.Message };
                }
            }
        }
        Append(s, "approval.resolved", run.Id, new { approvalId, approved = request.Approved, note = request.Note });
        if (!request.Approved)
        {
            MarkCancelled(s, run);
            if (s.WorkflowSteps.Count > 0)
            {
                ApplyWorkflowStepStatus(s, "cancelled");
                s.WorkflowStatus = "cancelled";
            }
            Touch(s);
            Append(s, "run.cancelled", run.Id, new { reason = "approval_rejected", approvalId, note = request.Note });
            Append(s, "session.status", run.Id, new { status = s.Status });
            Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
            return item;
        }

        if (IsWorkflowApprovalHold(run))
        {
            CompleteWorkflowApproval(s, run);
            return item;
        }

        run.ContinueInput = "（审批已通过，请继续执行原任务）";
        s.Status = "running";
        run.Status = "running";
        Touch(s);
        Append(s, "session.status", run.Id, new { status = s.Status });
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        _ = SafeStart(() => _executor?.ResumeAsync(s.Id, run.Id, "continue", CancellationToken.None) ?? Task.CompletedTask);
        return item;
    }

    public object? StopRun(string sessionId, string runId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var run = s.CurrentRun;
        if (run == null || run.Id != runId) return new { error = "执行不存在" };
        if (s.Status is "completed" or "cancelled" or "failed")
            return new { error = "当前状态不可停止" };
        MarkCancelled(s, run);
        Touch(s);
        _ = SafeStart(() => _executor?.StopAsync(s.Id, run.Id) ?? Task.CompletedTask);
        Append(s, "run.cancelled", run.Id, new { reason = "stopped" });
        Append(s, "session.status", run.Id, new { status = s.Status });
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
    }

    public object? Intervene(string sessionId, string runId, InterveneRunRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var run = s.CurrentRun;
        if (run == null || run.Id != runId) return new { error = "执行不存在" };
        var action = (request.Action ?? "").Trim().ToLowerInvariant();
        switch (action)
        {
            case "cancel":
                return StopRun(sessionId, runId);
            case "continue":
                if (s.Status != "stuck" && s.Status != "awaiting_approval")
                    return new { error = "当前状态不可继续" };
                run.ContinueInput = request.Input;
                run.Status = "running";
                s.Status = "running";
                Touch(s);
                Append(s, "session.status", run.Id, new { status = s.Status, action, input = request.Input });
                _ = SafeStart(() => _executor?.ResumeAsync(s.Id, run.Id, action, CancellationToken.None) ?? Task.CompletedTask);
                return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
            case "skip":
                if (s.Status != "stuck" && s.Status != "running")
                    return new { error = "当前状态不可跳过" };
                run.InterveneStepIndex = ResolveInterveneIndex(run);
                SkipCurrentStep(run);
                run.Status = "running";
                s.Status = "running";
                Touch(s);
                Append(s, "step.completed", run.Id, new { stepId = request.StepId, status = "skipped" });
                Append(s, "session.status", run.Id, new { status = s.Status, action });
                _ = SafeStart(() => _executor?.ResumeAsync(s.Id, run.Id, action, CancellationToken.None) ?? Task.CompletedTask);
                return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
            case "override":
                if (s.Status != "stuck")
                    return new { error = "当前状态不可补位" };
                run.InterveneStepIndex = ResolveInterveneIndex(run);
                SkipCurrentStep(run, completed: true);
                run.Status = "running";
                s.Status = "running";
                Touch(s);
                Append(s, "step.completed", run.Id, new { stepId = request.StepId, status = "completed", output = request.Output });
                Append(s, "session.status", run.Id, new { status = s.Status, action });
                _ = SafeStart(() => _executor?.ResumeAsync(s.Id, run.Id, action, CancellationToken.None) ?? Task.CompletedTask);
                return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
            case "retry":
                if (s.Status is not ("failed" or "stuck"))
                    return new { error = "当前状态不可重试" };
                run.Retrying = true;
                run.StuckReason = null;
                run.FailureReason = null;
                run.AllowedActions = null;
                run.ContinueInput = string.IsNullOrWhiteSpace(request.Input)
                    ? "（重试上一任务，请继续完成）"
                    : request.Input;
                run.Status = "running";
                s.Status = "running";
                Touch(s);
                Append(s, "session.status", run.Id, new { status = s.Status, action });
                _ = SafeStart(() => _executor?.ResumeAsync(s.Id, run.Id, action, CancellationToken.None) ?? Task.CompletedTask);
                return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
            default:
                return new { error = "不支持的介入动作" };
        }
    }

    public object? PauseWorkflow(string sessionId, string workflowId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        s.WorkflowStatus = "paused";
        s.WorkflowId = workflowId;
        ApplyWorkflowStepStatus(s, "paused");
        if (s.Status is "completed" or "idle" or "failed")
            s.Status = "running";
        if (s.CurrentRun is { Status: "completed" or "failed" })
            s.CurrentRun.Status = "running";
        Touch(s);
        Append(s, "session.status", s.CurrentRun?.Id, new { status = s.Status });
        Append(s, "workflow.updated", s.CurrentRun?.Id, new { workflowId, status = "paused", steps = s.WorkflowSteps });
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return new { workflowId, status = "paused", steps = s.WorkflowSteps };
    }

    public object? CancelWorkflow(string sessionId, string workflowId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        s.WorkflowStatus = "cancelled";
        s.WorkflowId = workflowId;
        ApplyWorkflowStepStatus(s, "cancelled");
        if (s.CurrentRun != null) MarkCancelled(s, s.CurrentRun);
        else s.Status = "cancelled";
        Touch(s);
        Append(s, "session.status", s.CurrentRun?.Id, new { status = s.Status });
        Append(s, "run.cancelled", s.CurrentRun?.Id, new { reason = "workflow_cancelled" });
        Append(s, "workflow.updated", s.CurrentRun?.Id, new { workflowId, status = "cancelled", steps = s.WorkflowSteps });
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return new { workflowId, status = "cancelled", steps = s.WorkflowSteps };
    }

    public object? GetWorkspace(string sessionId, string? panel)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var items = BuildWorkspace(s);
        if (string.IsNullOrWhiteSpace(panel))
            return new { items };
        var id = panel.Trim();
        return id switch
        {
            "plan" => new { panel = id, plan = s.CurrentRun?.Plan, status = items.First(i => i.Id == "plan").Status },
            "records" => RecordsPanel(s, items),
            "tools" => new { panel = id, tools = VisibleToolEntries(s), mcp = McpConnections(), status = items.First(i => i.Id == "tools").Status },
            "memory" => new { panel = id, items = s.Memories, status = items.First(i => i.Id == "memory").Status },
            "suggest" => new { panel = id, items = s.Suggestions, status = items.First(i => i.Id == "suggest").Status },
            "snippets" => new { panel = id, items = s.Snippets, status = items.First(i => i.Id == "snippets").Status },
            "todo" => new { panel = id, items = s.Todos, status = items.First(i => i.Id == "todo").Status },
            "design" => new { panel = id, preview = s.DesignPreview, committed = s.DesignCommitted, status = items.First(i => i.Id == "design").Status },
            "workflow" => WorkflowPanel(s, items),
            "project" => ProjectPanel(s, items),
            "skills" => new { panel = id, attachments = s.CurrentRun?.Attachments.Where(a => a.Kind == "skill").ToList(), status = items.First(i => i.Id == "skills").Status },
            _ => new { error = "未知面板" }
        };
    }

    public object? WriteMemory(string sessionId, WorkspaceWritebackRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        if (string.IsNullOrWhiteSpace(request.Content))
            return new { error = "写回内容不能为空" };
        var content = request.Content.Trim();
        var persistedId = TryPersistMemory(s, request.Title, content);
        var item = new
        {
            id = persistedId ?? Guid.NewGuid().ToString("N"),
            title = string.IsNullOrWhiteSpace(request.Title) ? TrimTitle(content) : request.Title.Trim(),
            content,
            at = DateTimeOffset.Now,
            persisted = persistedId != null
        };
        s.Memories.Add(item);
        Touch(s);
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return item;
    }

    public object? CommitDesign(string sessionId, WorkspaceWritebackRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var libraryError = TryCommitDesignLibrary(s, request.Content);
        if (libraryError != null)
            return new { error = libraryError };
        s.DesignCommitted = true;
        s.DesignPreview = request.Content ?? s.DesignPreview;
        Touch(s);
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return new { committed = true };
    }

    public object? DispatchTodo(string sessionId, string todoId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var item = s.Todos.FirstOrDefault(t => string.Equals(t.Id, todoId, StringComparison.OrdinalIgnoreCase));
        if (item == null) return new { error = "待办不存在" };
        if (item.Dispatched) return item;
        item.Dispatched = true;
        item.Assignee = s.AgentId ?? s.PluginId;
        item.At = DateTimeOffset.Now;
        Touch(s);
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        if (!IsBusy(s.Status))
        {
            var text = string.IsNullOrWhiteSpace(item.Goal) ? item.Title : item.Goal;
            if (!string.IsNullOrWhiteSpace(text))
                PostMessage(sessionId, new PostAgentMessageRequest { Text = text });
        }
        return item;
    }

    public object? SaveSnippet(string sessionId, WorkspaceWritebackRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var item = new { id = Guid.NewGuid().ToString("N"), title = request.Title, content = request.Content ?? "" };
        s.Snippets.Add(item);
        Touch(s);
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return item;
    }

    public IReadOnlyList<CatalogModelDto> ListModels()
    {
        var query = _ctx?.Get<ITurnTelemetryQuery>();
        if (query == null) return Array.Empty<CatalogModelDto>();
        try
        {
            var catalog = query.GetModelCatalogAsync().GetAwaiter().GetResult();
            return catalog.Select(m => new CatalogModelDto
            {
                Id = string.IsNullOrWhiteSpace(m.ChatModelId) ? m.UpstreamModelId : m.ChatModelId,
                Name = string.IsNullOrWhiteSpace(m.Alias) ? m.UpstreamModelId : m.Alias
            }).ToList();
        }
        catch
        {
            return Array.Empty<CatalogModelDto>();
        }
    }

    public IReadOnlyList<CatalogAgentDto> ListAgents()
    {
        var registry = _agents ?? _ctx?.Get<IAgentRegistryService>();
        if (registry == null) return Array.Empty<CatalogAgentDto>();
        try
        {
            return registry.GetAllAgents()
                .Select(a => new CatalogAgentDto { Id = a.Id, Name = a.Name, Description = a.Description })
                .ToList();
        }
        catch (Exception ex)
        {
            NewLife.Log.XTrace.Log.Warn("[AgentFacade] 列出智能体失败: {0}", ex.Message);
            return Array.Empty<CatalogAgentDto>();
        }
    }

    /// <summary>运行时把执行过程写回同一条事件流（文本、工具、步骤、审批、卡住等）。</summary>
    public bool Publish(string sessionId, string type, object? payload, string? runId = null)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return false;
        if (runId == null) runId = s.CurrentRun?.Id;
        if (type == "approval.required" && s.CurrentRun != null)
        {
            var id = ReadString(payload, "approvalId")
                     ?? ReadString(payload, "id")
                     ?? Guid.NewGuid().ToString("N");
            if (!s.CurrentRun.Approvals.Any(a => a.Id == id))
                s.CurrentRun.Approvals.Add(new ApprovalState { Id = id, Status = "pending" });
            s.Status = "awaiting_approval";
            s.CurrentRun.Status = "awaiting_approval";
            payload = MergeApprovalId(payload, id);
        }
        if ((s.Status == "cancelled" || s.Status == "completed")
            && (type is "run.stuck" or "run.failed" or "run.completed" or "run.cancelled" or "approval.required"))
        {
            return true;
        }
        if (s.CurrentRun != null && type is "tool.started" or "tool.completed")
            RememberTool(s.CurrentRun, type, payload);
        if (s.CurrentRun?.Plan?.Steps != null && type is "step.started" or "step.completed")
            RememberPlanStep(s.CurrentRun, type, payload);
        if (type == "run.stuck" && s.CurrentRun != null)
        {
            s.Status = "stuck";
            s.CurrentRun.Status = "stuck";
            s.CurrentRun.StuckReason = AgentFacadeEventMapper.ExtractText(payload)
                                       ?? ReadString(payload, "reason")
                                       ?? "无法继续";
            s.CurrentRun.AllowedActions = new List<string> { "continue", "skip", "override", "retry", "cancel" };
            payload = MergeStuck(payload, runId, s.CurrentRun.AllowedActions);
        }
        if (type == "run.failed" && s.CurrentRun != null)
        {
            s.Status = "failed";
            s.CurrentRun.Status = "failed";
            s.CurrentRun.FailureReason = AgentFacadeEventMapper.ExtractText(payload)
                                         ?? ReadString(payload, "reason")
                                         ?? ReadString(payload, "message")
                                         ?? "执行失败";
            s.CurrentRun.AllowedActions = new List<string> { "retry" };
            payload = new { reason = s.CurrentRun.FailureReason, allowedActions = s.CurrentRun.AllowedActions, payload };
        }
        if (type == "run.completed" && s.CurrentRun != null)
        {
            if (s.WorkflowStatus is "running" or "paused")
            {
                s.Status = "running";
                s.CurrentRun.Status = "running";
                type = "session.status";
                payload = new { status = s.Status };
            }
            else
            {
                s.Status = "completed";
                s.CurrentRun.Status = "completed";
                s.CurrentRun.StuckReason = null;
                s.CurrentRun.AllowedActions = null;
                SeedFollowUps(s);
            }
        }
        if (type == "run.cancelled" && s.CurrentRun != null)
            MarkCancelled(s, s.CurrentRun);
        if (type is "message.completed" or "message.delta")
        {
            var text = AgentFacadeEventMapper.ExtractText(payload) ?? "";
            var messageId = "asst-" + (runId ?? s.Id);
            payload = new { text, messageId, id = messageId };
            // 文本增量由运行时推送；完成时落入会话消息。
            if (type == "message.completed" && !string.IsNullOrWhiteSpace(text))
            {
                s.Messages.Add(new AgentMessageDto
                {
                    Id = messageId,
                    Role = "assistant",
                    Content = text,
                    CreatedAt = DateTimeOffset.Now
                });
                if (string.IsNullOrWhiteSpace(s.DesignPreview))
                {
                    var preview = ExtractDesignPreview(text);
                    if (!string.IsNullOrWhiteSpace(preview))
                        s.DesignPreview = preview;
                }
            }
        }
        Touch(s);
        Append(s, type, runId, payload);
        if (type is "run.stuck" or "run.failed" or "run.completed" or "approval.required" or "session.status")
            Append(s, "workspace.updated", runId, WorkspacePayload(s));
        return true;
    }

    public AgentRunExecutionSnapshot? GetExecution(string sessionId, string runId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var run = s.CurrentRun;
        if (run == null || run.Id != runId) return null;
        return new AgentRunExecutionSnapshot
        {
            SessionId = s.Id,
            RunId = run.Id,
            Mode = s.Mode,
            Status = s.Status,
            Text = run.Text,
            ContinueInput = run.ContinueInput,
            AgentId = s.AgentId,
            ModelId = s.ModelId,
            Cwd = (run.ContextSnapshot ?? s.Context).Cwd,
            Plan = run.Plan,
            ToolScope = run.ToolScope,
            NativeRunId = run.NativeRunId,
            InterveneStepIndex = run.InterveneStepIndex
        };
    }

    public void BindNativeRunId(string sessionId, string runId, long nativeRunId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return;
        if (s.CurrentRun != null && s.CurrentRun.Id == runId)
            s.CurrentRun.NativeRunId = nativeRunId;
    }

    /// <summary>供测试与运行时读取本轮收缩后的可见工具。</summary>
    public IReadOnlyList<string> VisibleTools(string sessionId, IEnumerable<string>? whitelist)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted)
            return Array.Empty<string>();
        var baseline = whitelist is not null ? whitelist : DefaultVisibleToolNames(s);
        return AgentToolScopeRules.Narrow(baseline, s.CurrentRun?.ToolScope);
    }

    public AgentEmbedContext? ContextSnapshot(string sessionId) =>
        !_sessions.TryGetValue(sessionId, out var s) || s.Deleted ? null : (s.CurrentRun?.ContextSnapshot ?? s.Context);

    private IReadOnlyList<string> VisibleTools(SessionState s) =>
        AgentToolScopeRules.Narrow(DefaultVisibleToolNames(s), s.CurrentRun?.ToolScope);

    private IReadOnlyList<string> DefaultVisibleToolNames(SessionState s)
    {
        var registry = _ctx?.Get<IToolRegistry>();
        return AgentDefaultToolScope.ResolveNames(registry, s.PluginId);
    }

    private object[] VisibleToolEntries(SessionState s)
    {
        var names = VisibleTools(s);
        var mcpTools = new Dictionary<string, McpExternalToolInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var gateway = _ctx?.Get<IMcpExternalGateway>();
            if (gateway != null)
            {
                foreach (var tool in gateway.ListConnectedTools())
                    mcpTools[tool.FullName] = tool;
            }
        }
        catch { /* 无 MCP 网关时降级为空 */ }

        var scopedMcp = AgentToolScopeRules.Narrow(mcpTools.Keys, s.CurrentRun?.ToolScope);
        return names.Select(n => (object)new { name = n, source = "host" })
            .Concat(scopedMcp.Select(n =>
            {
                mcpTools.TryGetValue(n, out var info);
                return (object)new { name = n, source = "mcp", serverId = info?.ServerId, status = "connected" };
            }))
            .ToArray();
    }

    private object[] McpConnections()
    {
        try
        {
            var gateway = _ctx?.Get<IMcpExternalGateway>();
            var connected = gateway?.ListConnectedTools() ?? Array.Empty<McpExternalToolInfo>();
            if (connected.Count == 0)
                return [new { status = "未连接" }];
            return connected
                .GroupBy(t => t.ServerId, StringComparer.OrdinalIgnoreCase)
                .Select(g => (object)new { id = g.Key, status = "connected", toolCount = g.Count() })
                .ToArray();
        }
        catch
        {
            return [new { status = "未连接" }];
        }
    }

    private object WorkspacePayload(SessionState s) => new { items = BuildWorkspace(s) };

    private static object? MergeApprovalId(object? payload, string id)
    {
        var summary = ReadString(payload, "summary")
                      ?? ReadString(payload, "description")
                      ?? ReadString(payload, "action")
                      ?? "需要确认";
        return new
        {
            approvalId = id,
            id,
            summary,
            action = ReadString(payload, "action"),
            risk = ReadString(payload, "risk"),
            payload
        };
    }

    private static object MergeStuck(object? payload, string? runId, List<string> allowedActions) => new
    {
        reason = AgentFacadeEventMapper.ExtractText(payload) ?? ReadString(payload, "reason") ?? "无法继续",
        runId,
        stepId = ReadString(payload, "stepId"),
        stepIndex = ReadString(payload, "stepIndex"),
        allowedActions
    };

    private static string? ReadString(object? payload, string name)
    {
        if (payload == null) return null;
        var prop = payload.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        var value = prop?.GetValue(payload)?.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void SkipCurrentStep(RunState run, bool completed = false)
    {
        var step = run.Plan?.Steps.FirstOrDefault(x => x.Status is "pending" or "running" or "stuck");
        if (step != null) step.Status = completed ? "completed" : "skipped";
    }

    private static int ResolveInterveneIndex(RunState run)
    {
        var steps = run.Plan?.Steps;
        if (steps == null || steps.Count == 0) return 0;
        var idx = steps.FindIndex(x => x.Status is "stuck" or "running");
        if (idx < 0) idx = steps.FindIndex(x => x.Status is "pending");
        return idx < 0 ? 0 : idx;
    }

    private static AgentPlanDto BuildDraftPlan(string text)
    {
        return new AgentPlanDto
        {
            Goal = text,
            Constraints = new List<string> { "确认前不执行" },
            Steps =
            {
                new AgentPlanStepDto { Id = "s1", Name = "准备", Objective = "确认目标与约束", Status = "pending" },
                new AgentPlanStepDto { Id = "s2", Name = "执行", Objective = text, Status = "pending" }
            }
        };
    }

    private List<AgentWorkspaceStatusDto> BuildWorkspace(SessionState s)
    {
        var run = s.CurrentRun;
        var records = s.Status switch
        {
            "idle" when run == null => "暂无",
            "awaiting_plan" => "待确认",
            "awaiting_approval" => "待审批",
            "stuck" => "卡住",
            "running" => $"进行中 · {Math.Max(run?.Turn ?? 0, 1)}/{run?.TurnMax ?? 20}",
            "completed" => "已完成",
            "failed" => "失败",
            "cancelled" => "已取消",
            _ => "暂无"
        };
        var plan = run?.Plan == null ? "无计划" : (s.Status == "awaiting_plan" ? "待确认" : "有计划");
        var project = ProjectLabel(s);
        return new List<AgentWorkspaceStatusDto>
        {
            Item("plan", "计划", "先看步骤，确认后再执行", plan),
            Item("records", "执行记录", "本轮步骤、工具结果与轮次", records),
            Item("workflow", "工作流", "关联工作流的执行与控制", WorkflowLabel(s)),
            Item("memory", "记忆", "本轮检索到的条目，可写回", s.Memories.Count == 0 ? "0" : s.Memories.Count.ToString()),
            Item("design", "设计", "组件与令牌预览，默认不写库", string.IsNullOrEmpty(s.DesignPreview) ? "无预览" : "有预览"),
            Item("suggest", "建议", "按最近习惯推荐下一步", s.Suggestions.Count == 0 ? "暂无" : s.Suggestions.Count.ToString()),
            Item("project", "项目", "当前工作目录、档案与命令", project),
            Item("todo", "待办", "可下发给智能体的任务", s.Todos.Count == 0 ? "0" : s.Todos.Count.ToString()),
            Item("skills", "技能", "本智能体已启用的技能", SkillLabel(run)),
            Item("tools", "工具", "本轮可见的宿主与 MCP 工具", VisibleToolsLabel(s)),
            Item("snippets", "代码片段", "本轮产出或收藏的片段", s.Snippets.Count == 0 ? "暂无" : s.Snippets.Count.ToString()),
        };
    }

    private string VisibleToolsLabel(SessionState s)
    {
        var tools = VisibleTools(s);
        return tools.Count == 0 ? "未限定" : $"{tools.Count} 个可见";
    }

    private static void MarkCancelled(SessionState s, RunState run)
    {
        s.Status = "cancelled";
        run.Status = "cancelled";
        run.StuckReason = null;
        run.FailureReason = null;
        run.AllowedActions = null;
        if (run.Plan?.Steps == null) return;
        foreach (var step in run.Plan.Steps)
        {
            if (step.Status is "stuck" or "running" or "pending")
                step.Status = "cancelled";
        }
    }

    private static bool ContainsKeyword(string? text, string keyword) =>
        !string.IsNullOrWhiteSpace(text) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private void ApplyWorkspaceSeeds(SessionState s, RunState run)
    {
        var text = run.Text ?? "";
        if (ContainsKeyword(text, "agent-workflow-approval"))
        {
            s.WorkflowId = "wf-itest";
            s.WorkflowStatus = "running";
            s.WorkflowSteps.Clear();
            s.WorkflowSteps.Add(new AgentWorkspaceWorkflowStepDto { Id = "w1", Name = "准备", Status = "completed" });
            s.WorkflowSteps.Add(new AgentWorkspaceWorkflowStepDto { Id = "w2", Name = "写入结果", Status = "pending", Risk = "high" });
        }
        else if (ContainsKeyword(text, "agent-workflow"))
        {
            s.WorkflowId = "wf-itest";
            s.WorkflowStatus = "running";
            s.WorkflowSteps.Clear();
            s.WorkflowSteps.Add(new AgentWorkspaceWorkflowStepDto { Id = "w1", Name = "准备", Status = "completed" });
            s.WorkflowSteps.Add(new AgentWorkspaceWorkflowStepDto { Id = "w2", Name = "写入结果", Status = "running", Risk = "high" });
        }
        if (ContainsKeyword(text, "agent-design"))
        {
            s.DesignCommitted = false;
            s.DesignPreview = """{"component":"Button","tokens":{"color.accent":"#F59E0B","radius.md":"8px"},"note":"默认不写库"}""";
        }
        if (ContainsKeyword(text, "agent-suggest"))
        {
            if (s.Suggestions.Count == 0)
            {
                s.Suggestions.Add(new { id = "sg-workflow", title = "转到工作流", action = "open_workflow", detail = "打开关联工作流并继续当前步骤" });
                s.Suggestions.Add(new { id = "sg-memory", title = "写回记忆", action = "write_memory", detail = "把本轮要点写回记忆" });
                s.Suggestions.Add(new { id = "sg-todo", title = "下发待办", action = "dispatch_todo", detail = "把剩余工作交给当前智能体" });
            }
        }
        if (ContainsKeyword(text, "agent-project"))
        {
            var cwd = "/tmp/agent-itest";
            s.Context.Cwd = cwd;
            s.Context.Title = "联调项目";
            s.Context.ProjectId = "proj-itest";
            run.ContextSnapshot.Cwd = cwd;
            run.ContextSnapshot.Title = "联调项目";
            run.ContextSnapshot.ProjectId = "proj-itest";
        }
        if (ContainsKeyword(text, "agent-todo"))
        {
            if (s.Todos.Count == 0)
            {
                s.Todos.Add(new AgentWorkspaceTodoDto
                {
                    Id = "todo-itest",
                    Title = "整理联调纪要",
                    Goal = "把本轮结果写成三条纪要",
                    Scope = "/tmp/agent-itest",
                    Acceptance = "纪要文件可打开且含三条要点",
                    Dispatched = false
                });
            }
        }
    }

    private bool TryFailByKeyword(SessionState s, RunState run)
    {
        if (run.Retrying || !ContainsKeyword(run.Text, "agent-fail"))
            return false;
        s.Status = "failed";
        run.Status = "failed";
        run.FailureReason = "模拟失败：命中 agent-fail，可重试";
        run.AllowedActions = new List<string> { "retry" };
        if (run.Plan?.Steps != null)
        {
            var step = run.Plan.Steps.FirstOrDefault(x => x.Status is "pending" or "running") ?? run.Plan.Steps.FirstOrDefault();
            if (step != null) step.Status = "failed";
        }
        Touch(s);
        Append(s, "run.failed", run.Id, new { reason = run.FailureReason, allowedActions = run.AllowedActions });
        Append(s, "session.status", run.Id, new { status = s.Status });
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        return true;
    }

    public static TimeSpan RunningHoldDuration { get; set; } = TimeSpan.FromSeconds(8);

    private bool TryHoldRunningByKeyword(SessionState s, RunState run)
    {
        if (!ContainsKeyword(run.Text, "agent-running"))
            return false;
        s.Status = "running";
        run.Status = "running";
        SeedRunningRecords(run);
        Touch(s);
        Append(s, "session.status", run.Id, new { status = s.Status });
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        var sessionId = s.Id;
        var runId = run.Id;
        var delay = RunningHoldDuration;
        _ = SafeStart(async () =>
        {
            try { await Task.Delay(delay).ConfigureAwait(false); }
            catch { return; }
            if (!_sessions.TryGetValue(sessionId, out var live) || live.Deleted) return;
            if (live.Status is "cancelled" or "failed" or "completed") return;
            if (live.CurrentRun == null || live.CurrentRun.Id != runId) return;
            CompleteRunningPlanSteps(live.CurrentRun);
            Publish(sessionId, "message.completed", new { text = "进行中任务已结束。" }, runId);
            Publish(sessionId, "run.completed", new { text = "进行中任务已结束。" }, runId);
        });
        return true;
    }

    private const string WorkflowApprovalWritePath = "/tmp/agent-itest/note.txt";

    private bool TryRequireWorkflowApprovalByKeyword(SessionState s, RunState run)
    {
        if (!ContainsKeyword(run.Text, "agent-workflow-approval"))
            return false;
        if (string.IsNullOrWhiteSpace(s.WorkflowId))
        {
            s.WorkflowId = "wf-itest";
            s.WorkflowStatus = "running";
        }
        var cwd = (run.ContextSnapshot ?? s.Context).Cwd;
        if (string.IsNullOrWhiteSpace(cwd))
            cwd = "/tmp/agent-itest";
        var args = new
        {
            cwd,
            path = WorkflowApprovalWritePath,
            content = "ok",
            start_line = 1,
            end_line = 1
        };
        var approvalId = "wf-appr-" + run.Id;
        var payload = new
        {
            approvalId,
            id = approvalId,
            summary = "工作流已启动，高风险工具 write_file 需要审批后执行",
            action = "write_file",
            toolName = "write_file",
            args = System.Text.Json.JsonSerializer.Serialize(args),
            risk = "high",
            callId = "call_" + run.Id
        };
        Touch(s);
        Append(s, "session.status", run.Id, new { status = "running" });
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        Publish(s.Id, "approval.required", payload, run.Id);
        return true;
    }

    private static bool IsWorkflowApprovalHold(RunState run) =>
        ContainsKeyword(run.Text, "agent-workflow-approval") && run.NativeRunId == null;

    private void CompleteWorkflowApproval(SessionState s, RunState run)
    {
        var path = WorkflowApprovalWritePath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, "ok");
        var detail = System.Text.Json.JsonSerializer.Serialize(new { success = true, path, content = "ok" });
        foreach (var step in s.WorkflowSteps)
        {
            if (step.Status is "pending" or "running" or "paused")
                step.Status = "completed";
        }
        if (s.WorkflowSteps.Count > 0)
            s.WorkflowStatus = "completed";
        s.Status = "running";
        run.Status = "running";
        Touch(s);
        Append(s, "session.status", run.Id, new { status = s.Status });
        Publish(s.Id, "tool.completed", new
        {
            name = "write_file",
            toolCallId = "call_" + run.Id,
            summary = "write_file 已写入",
            detail,
            status = "done"
        }, run.Id);
        Publish(s.Id, "message.completed", new { text = "高风险步骤已批准并执行。" }, run.Id);
        Publish(s.Id, "run.completed", new { text = "高风险步骤已批准并执行。" }, run.Id);
    }

    private static void SeedRunningRecords(RunState run)
    {
        run.Turn = Math.Max(run.Turn, 1);
        if (run.TurnMax <= 0) run.TurnMax = 20;
        if (run.Plan?.Steps is { Count: > 0 })
        {
            if (run.Plan.Steps.All(st => st.Status is "pending" or "completed"))
            {
                var step = run.Plan.Steps.FirstOrDefault(st => st.Status == "pending") ?? run.Plan.Steps[0];
                step.Status = "running";
            }
            return;
        }
        run.Plan = new AgentPlanDto
        {
            Goal = string.IsNullOrWhiteSpace(run.Text) ? "执行中任务" : run.Text,
            Steps =
            {
                new AgentPlanStepDto { Id = "r1", Name = "准备上下文", Objective = "确认目标与输入", Status = "completed" },
                new AgentPlanStepDto { Id = "r2", Name = "执行任务", Objective = "推进当前步骤", Status = "running" }
            }
        };
    }

    private static void CompleteRunningPlanSteps(RunState run)
    {
        if (run.Plan?.Steps == null) return;
        foreach (var step in run.Plan.Steps)
        {
            if (step.Status is "running" or "pending")
                step.Status = "completed";
        }
    }

    private static string WorkflowLabel(SessionState s)
    {
        var status = ResolveWorkflow(s).Status;
        return string.IsNullOrWhiteSpace(status) ? "未启动" : status;
    }

    private object WorkflowPanel(SessionState s, List<AgentWorkspaceStatusDto> items)
    {
        var resolved = ResolveWorkflow(s);
        return new
        {
            panel = "workflow",
            workflowId = resolved.Id,
            steps = resolved.Steps,
            workflowStatus = resolved.Status,
            status = items.First(i => i.Id == "workflow").Status
        };
    }

    private readonly record struct WorkflowView(string? Id, string? Status, IReadOnlyList<AgentWorkspaceWorkflowStepDto> Steps);

    private static WorkflowView ResolveWorkflow(SessionState s)
    {
        if (s.WorkflowSteps.Count > 0 || !string.IsNullOrWhiteSpace(s.WorkflowStatus))
            return new WorkflowView(s.WorkflowId, s.WorkflowStatus, s.WorkflowSteps);
        var plan = s.CurrentRun?.Plan?.Steps;
        if (plan is not { Count: > 0 } || s.CurrentRun == null)
            return new WorkflowView(s.WorkflowId, s.WorkflowStatus, s.WorkflowSteps);
        var status = s.Status switch
        {
            "running" or "planning" or "awaiting_plan" => "running",
            "completed" => "completed",
            "cancelled" => "cancelled",
            "failed" => "failed",
            _ => "running"
        };
        var steps = plan.Select(step => new AgentWorkspaceWorkflowStepDto
        {
            Id = step.Id,
            Name = string.IsNullOrWhiteSpace(step.Name) ? step.Title : step.Name,
            Status = string.IsNullOrWhiteSpace(step.Status) ? "pending" : step.Status
        }).ToList();
        return new WorkflowView("plan-" + s.CurrentRun.Id, status, steps);
    }

    private static void SeedFollowUps(SessionState s)
    {
        if (s.Suggestions.Count == 0)
        {
            s.Suggestions.Add(new { id = "sg-memory", title = "写回记忆", action = "write_memory", detail = "把本轮要点写回记忆库" });
            s.Suggestions.Add(new { id = "sg-records", title = "查看执行记录", action = "open_records", detail = "看本轮步骤和工具结果" });
        }
        var text = s.CurrentRun?.Text;
        if (s.Todos.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            s.Todos.Add(new AgentWorkspaceTodoDto
            {
                Id = "todo-" + s.CurrentRun!.Id,
                Title = TrimTitle(text),
                Goal = text.Trim(),
                Dispatched = false
            });
        }
    }

    private void PullRelevantMemories(SessionState s, string query)
    {
        var memory = MemoryOrNull();
        if (memory == null || string.IsNullOrWhiteSpace(query)) return;
        try
        {
            var found = memory.GetRelevantMemoriesAsync(query, 5).GetAwaiter().GetResult();
            foreach (var item in found)
            {
                var id = item.Id.ToString();
                if (s.Memories.Any(existing => existing.GetType().GetProperty("id")?.GetValue(existing)?.ToString() == id))
                    continue;
                s.Memories.Add(new { id, title = item.Title, content = item.Content, at = item.CreatedAt });
            }
        }
        catch (Exception ex)
        {
            NewLife.Log.XTrace.Log.Warn("[AgentFacade] 检索记忆失败: {0}", ex.Message);
        }
    }

    private string? TryPersistMemory(SessionState s, string? title, string content)
    {
        var memory = MemoryOrNull();
        if (memory == null) return null;
        try
        {
            var created = memory.CreateAsync(new CreateMemoryRequest
            {
                Title = string.IsNullOrWhiteSpace(title) ? TrimTitle(content) : title.Trim(),
                Content = content,
                Source = "agent-workspace",
                Type = MemoryType.Fact,
                WorkspacePath = (s.CurrentRun?.ContextSnapshot ?? s.Context).Cwd
            }).GetAwaiter().GetResult();
            return created.Id.ToString();
        }
        catch (Exception ex)
        {
            NewLife.Log.XTrace.Log.Warn("[AgentFacade] 写回记忆库失败: {0}", ex.Message);
            return null;
        }
    }

    private string? TryCommitDesignLibrary(SessionState s, string? content)
    {
        var registry = ToolRegistryOrNull();
        if (registry?.GetTool("design_create") == null)
            return null;
        var brief = string.IsNullOrWhiteSpace(content) ? s.DesignPreview : content;
        if (string.IsNullOrWhiteSpace(brief))
            return "没有可写入的设计预览";
        var args = System.Text.Json.JsonSerializer.Serialize(new { name = "助手确认", brief, apply = true });
        try
        {
            var raw = registry.ExecuteToolAsync("design_create", args).GetAwaiter().GetResult();
            if (raw.Contains("\"applied\":true", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase))
                return null;
            return string.IsNullOrWhiteSpace(raw) ? "设计系统没有接受这次写入" : TrimTitle(raw);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private IMemoryService? MemoryOrNull()
    {
        if (_ctx == null) return null;
        try { return _ctx.GetService(typeof(IMemoryService)) as IMemoryService; }
        catch { return null; }
    }

    private IToolRegistry? ToolRegistryOrNull()
    {
        if (_ctx == null) return null;
        try { return _ctx.GetService(typeof(IToolRegistry)) as IToolRegistry; }
        catch { return null; }
    }

    private static string? ExtractDesignPreview(string text)
    {
        const string open = "```json";
        var start = text.IndexOf(open, StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            start += open.Length;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
            {
                var body = text[start..end].Trim();
                if (body.Contains("tokens", StringComparison.OrdinalIgnoreCase))
                    return body;
            }
        }
        var trimmed = text.Trim();
        if (trimmed.StartsWith('{') && trimmed.Contains("tokens", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return null;
    }

    private static string TrimTitle(string text)
    {
        var trimmed = text.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return trimmed.Length <= 24 ? trimmed : trimmed[..24];
    }

    private static string SkillLabel(RunState? run)
    {
        var count = run?.Attachments.Count(a => string.Equals(a.Kind, "skill", StringComparison.OrdinalIgnoreCase)) ?? 0;
        return count == 0 ? "未启用" : $"{count} 已启用";
    }

    private IProjectWorkspaceService? WorkspaceOrNull()
    {
        if (_ctx == null) return null;
        try
        {
            return _ctx.GetService(typeof(IProjectWorkspaceService)) as IProjectWorkspaceService;
        }
        catch
        {
            return null;
        }
    }

    private string? BoundProjectRoot()
    {
        var root = WorkspaceOrNull()?.ProjectRoot;
        return string.IsNullOrWhiteSpace(root) ? null : root;
    }

    private static string FolderName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? trimmed : name;
    }

    private string ProjectLabel(SessionState s)
    {
        var ctx = s.CurrentRun?.ContextSnapshot ?? s.Context;
        var cwd = ctx.Cwd;
        var projectId = ctx.ProjectId;
        var title = ctx.Title;
        if (string.IsNullOrWhiteSpace(cwd) && string.IsNullOrWhiteSpace(projectId))
        {
            var root = BoundProjectRoot();
            if (!string.IsNullOrWhiteSpace(root))
            {
                cwd = root;
                title ??= FolderName(root);
            }
        }
        if (string.IsNullOrWhiteSpace(cwd) && string.IsNullOrWhiteSpace(projectId))
            return "未绑定";
        return title ?? cwd ?? projectId ?? "已绑定";
    }

    private object ProjectPanel(SessionState s, List<AgentWorkspaceStatusDto> items)
    {
        var ctx = s.CurrentRun?.ContextSnapshot ?? s.Context;
        if (string.IsNullOrWhiteSpace(ctx.Cwd) && string.IsNullOrWhiteSpace(ctx.ProjectId))
        {
            var root = BoundProjectRoot();
            if (!string.IsNullOrWhiteSpace(root))
            {
                ctx = CloneContext(ctx, s.PluginId, s.SurfaceKey);
                ctx.Cwd = root;
                if (string.IsNullOrWhiteSpace(ctx.Title))
                    ctx.Title = FolderName(root);
            }
        }
        return new { panel = "project", context = ctx, status = items.First(i => i.Id == "project").Status };
    }

    private static void RememberPlanStep(RunState run, string type, object? payload)
    {
        var steps = run.Plan?.Steps;
        if (steps == null || steps.Count == 0) return;
        var stepId = ReadString(payload, "stepId") ?? ReadString(payload, "id");
        AgentPlanStepDto? step = null;
        if (!string.IsNullOrWhiteSpace(stepId))
            step = steps.FirstOrDefault(item => string.Equals(item.Id, stepId, StringComparison.Ordinal));
        if (step == null && int.TryParse(ReadString(payload, "stepIndex"), out var index) && index >= 0 && index < steps.Count)
            step = steps[index];
        if (step == null) return;
        step.Status = type == "step.started" ? "running" : "completed";
        var done = steps.Count(item => item.Status == "completed");
        if (done > run.Turn) run.Turn = done;
    }

    private static void RememberTool(RunState run, string type, object? payload)
    {
        var name = ReadString(payload, "name") ?? "工具";
        var callId = ReadString(payload, "toolCallId") ?? ReadString(payload, "callId") ?? ReadString(payload, "id");
        var summary = ReadString(payload, "summary");
        if (type == "tool.completed")
        {
            var existing = run.ToolSteps.LastOrDefault(step =>
                step.Status == "running"
                && (string.Equals(step.Id, callId, StringComparison.Ordinal) || string.Equals(step.Name, name, StringComparison.Ordinal)));
            if (existing != null)
            {
                var raw = ReadString(payload, "status");
                existing.Status = raw == "failed" ? "failed" : "completed";
                if (!string.IsNullOrWhiteSpace(summary)) existing.Objective = summary;
            }
            else
            {
                run.ToolSteps.Add(new AgentPlanStepDto
                {
                    Id = callId ?? Guid.NewGuid().ToString("N"),
                    Name = name,
                    Objective = summary ?? string.Empty,
                    Status = ReadString(payload, "status") == "failed" ? "failed" : "completed"
                });
            }
            var done = run.ToolSteps.Count(step => step.Status is "completed" or "failed");
            if (done > run.Turn) run.Turn = done;
            return;
        }

        run.ToolSteps.Add(new AgentPlanStepDto
        {
            Id = callId ?? Guid.NewGuid().ToString("N"),
            Name = name,
            Objective = summary ?? string.Empty,
            Status = "running"
        });
    }

    private static object RecordsPanel(SessionState s, List<AgentWorkspaceStatusDto> items)
    {
        var run = s.CurrentRun;
        var steps = new List<AgentPlanStepDto>();
        if (run?.Plan?.Steps != null) steps.AddRange(run.Plan.Steps);
        if (run != null) steps.AddRange(run.ToolSteps);
        var turn = run?.Turn ?? 0;
        if (run != null)
        {
            var doneTools = run.ToolSteps.Count(step => step.Status is "completed" or "failed");
            if (doneTools > turn) turn = doneTools;
        }
        if ((s.Status == "running" || run?.Status == "running") && turn < 1)
            turn = 1;
        return new
        {
            panel = "records",
            run = ToRunDto(run),
            steps,
            round = turn,
            turnMax = run?.TurnMax ?? 20,
            runStatus = run?.Status ?? s.Status,
            stuckReason = run?.StuckReason,
            failureReason = run?.FailureReason,
            status = items.First(i => i.Id == "records").Status
        };
    }

    private static void ApplyWorkflowStepStatus(SessionState s, string status)
    {
        foreach (var step in s.WorkflowSteps)
        {
            if (status == "paused" && step.Status is "running" or "pending")
                step.Status = "paused";
            if (status == "cancelled" && step.Status is "running" or "pending" or "paused")
                step.Status = "cancelled";
        }
    }

    private static AgentWorkspaceStatusDto Item(string id, string name, string description, string status) =>
        new() { Id = id, Name = name, Description = description, Status = status };

    private static AgentFacadeRunDto? ToRunDto(RunState? run)
    {
        if (run == null) return null;
        return new AgentFacadeRunDto
        {
            Id = run.Id,
            Status = run.Status,
            Turn = run.Turn,
            TurnMax = run.TurnMax,
            Plan = run.Plan,
            StuckReason = run.Status == "stuck" ? run.StuckReason : null,
            FailureReason = run.Status == "failed" ? run.FailureReason : null,
            AllowedActions = run.Status is "cancelled" or "completed" ? null : run.AllowedActions
        };
    }

    private static AgentSessionSummaryDto ToSummary(SessionState s) => new()
    {
        Id = s.Id,
        Title = s.Title,
        Mode = s.Mode,
        Status = s.Status,
        PluginId = s.PluginId,
        SurfaceKey = s.SurfaceKey,
        AgentId = s.AgentId,
        ModelId = s.ModelId,
        Surface = s.Surface,
        UpdatedAt = s.UpdatedAt
    };

    private static AgentEmbedContext CloneContext(AgentEmbedContext? src, string pluginId, string surfaceKey) => new()
    {
        PluginId = pluginId,
        SurfaceKey = surfaceKey,
        ProjectId = src?.ProjectId,
        Cwd = src?.Cwd,
        ObjectType = src?.ObjectType,
        ObjectId = src?.ObjectId,
        Title = src?.Title,
        Extras = src?.Extras == null ? null : new Dictionary<string, string>(src.Extras)
    };

    private static string NormalizeMode(string? mode) =>
        string.Equals(mode, "planned", StringComparison.OrdinalIgnoreCase) ? "planned" : "open";

    private static bool IsBusy(string status) =>
        status is "running" or "planning" or "awaiting_plan" or "awaiting_approval" or "stuck";

    private static void Touch(SessionState s) => s.UpdatedAt = DateTimeOffset.Now;

    private static void Append(SessionState s, string type, string? runId, object? payload)
    {
        AgentEventDto ev;
        lock (s.Events)
        {
            ev = new AgentEventDto
            {
                Type = type,
                SessionId = s.Id,
                RunId = runId,
                Seq = ++s.Seq,
                Ts = DateTimeOffset.Now,
                Payload = payload
            };
            s.Events.Add(ev);
        }
        s.Gate.Release();
    }

    private static Task SafeStart(Func<Task> work)
    {
        try { return work(); }
        catch { return Task.CompletedTask; }
    }

    private sealed class SessionState
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Mode { get; set; } = "open";
        public string Status { get; set; } = "idle";
        public string PluginId { get; set; } = string.Empty;
        public string SurfaceKey { get; set; } = AgentSurfaceKeys.Default;
        public string? AgentId { get; set; }
        public string? ModelId { get; set; }
        public string? Surface { get; set; }
        public AgentEmbedContext Context { get; set; } = new();
        public DateTimeOffset UpdatedAt { get; set; }
        public bool Deleted { get; set; }
        public RunState? CurrentRun { get; set; }
        public List<AgentMessageDto> Messages { get; } = new();
        public List<AgentEventDto> Events { get; } = new();
        public long Seq;
        public SemaphoreSlim Gate { get; } = new(0);
        public string? WorkflowId { get; set; }
        public string? WorkflowStatus { get; set; }
        public string? DesignPreview { get; set; }
        public bool DesignCommitted { get; set; }
        public List<object> Memories { get; } = new();
        public List<object> Suggestions { get; } = new();
        public List<object> Snippets { get; } = new();
        public List<AgentWorkspaceTodoDto> Todos { get; } = new();
        public List<AgentWorkspaceWorkflowStepDto> WorkflowSteps { get; } = new();
    }

    private sealed class RunState
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Turn { get; set; }
        public int TurnMax { get; set; }
        public AgentPlanDto? Plan { get; set; }
        public AgentToolScope? ToolScope { get; set; }
        public AgentEmbedContext ContextSnapshot { get; set; } = new();
        public List<AgentAttachment> Attachments { get; set; } = new();
        public string? StuckReason { get; set; }
        public List<string>? AllowedActions { get; set; }
        public string? ContinueInput { get; set; }
        public List<ApprovalState> Approvals { get; } = new();
        public long? NativeRunId { get; set; }
        public int? InterveneStepIndex { get; set; }
        public string? FailureReason { get; set; }
        public bool Retrying { get; set; }
        public List<AgentPlanStepDto> ToolSteps { get; } = new();
    }

    private sealed class ApprovalState
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string? Note { get; set; }
    }
}

/// <summary>执行器读取的本轮快照。</summary>
public sealed class AgentRunExecutionSnapshot
{
    public string SessionId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string Mode { get; set; } = "open";
    public string Status { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? ContinueInput { get; set; }
    public string? AgentId { get; set; }
    public string? ModelId { get; set; }
    public string? Cwd { get; set; }
    public AgentPlanDto? Plan { get; set; }
    public AgentToolScope? ToolScope { get; set; }
    public long? NativeRunId { get; set; }
    public int? InterveneStepIndex { get; set; }
}
