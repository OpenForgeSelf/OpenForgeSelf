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

    public AgentFacadeService(IAgentRunExecutor? executor = null, IContext? ctx = null)
    {
        _executor = executor;
        _ctx = ctx;
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

        if (s.Mode == "planned")
        {
            run.Plan = BuildDraftPlan(run.Text);
            s.Status = "awaiting_plan";
            run.Status = "awaiting_plan";
            Touch(s);
            Append(s, "session.status", run.Id, new { status = s.Status });
            Append(s, "plan.proposed", run.Id, run.Plan);
            Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
            return (202, new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id });
        }

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
        _ = SafeStart(() => _executor?.StartPlanAsync(s.Id, run.Id, CancellationToken.None) ?? Task.CompletedTask);
        return new AcceptedRunResponse { SessionId = s.Id, RunId = run.Id };
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
        if (!request.Approved)
        {
            // 未批准不得执行：保持不进入工具执行，回到可继续或取消。
            s.Status = "running";
            run.Status = "running";
        }
        else
        {
            s.Status = "running";
            run.Status = "running";
        }
        Touch(s);
        Append(s, "approval.resolved", run.Id, new { approvalId, approved = request.Approved, note = request.Note });
        Append(s, "session.status", run.Id, new { status = s.Status });
        Append(s, "workspace.updated", run.Id, WorkspacePayload(s));
        return item;
    }

    public object? StopRun(string sessionId, string runId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        var run = s.CurrentRun;
        if (run == null || run.Id != runId) return new { error = "执行不存在" };
        if (s.Status is "completed" or "cancelled" or "failed")
            return new { error = "当前状态不可停止" };
        run.Status = "cancelled";
        s.Status = "cancelled";
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
        Touch(s);
        Append(s, "workflow.updated", s.CurrentRun?.Id, new { workflowId, status = "paused" });
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return new { workflowId, status = "paused" };
    }

    public object? CancelWorkflow(string sessionId, string workflowId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        s.WorkflowStatus = "cancelled";
        s.WorkflowId = workflowId;
        Touch(s);
        Append(s, "workflow.updated", s.CurrentRun?.Id, new { workflowId, status = "cancelled" });
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return new { workflowId, status = "cancelled" };
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
            "records" => new { panel = id, run = ToRunDto(s.CurrentRun), status = items.First(i => i.Id == "records").Status },
            "tools" => new { panel = id, tools = VisibleTools(s), status = items.First(i => i.Id == "tools").Status },
            "memory" => new { panel = id, items = s.Memories, status = items.First(i => i.Id == "memory").Status },
            "suggest" => new { panel = id, items = s.Suggestions, status = items.First(i => i.Id == "suggest").Status },
            "snippets" => new { panel = id, items = s.Snippets, status = items.First(i => i.Id == "snippets").Status },
            "todo" => new { panel = id, items = s.Todos, status = items.First(i => i.Id == "todo").Status },
            "design" => new { panel = id, preview = s.DesignPreview, committed = s.DesignCommitted, status = items.First(i => i.Id == "design").Status },
            "workflow" => new { panel = id, workflowId = s.WorkflowId, status = items.First(i => i.Id == "workflow").Status },
            "project" => new { panel = id, context = s.CurrentRun?.ContextSnapshot ?? s.Context, status = items.First(i => i.Id == "project").Status },
            "skills" => new { panel = id, attachments = s.CurrentRun?.Attachments.Where(a => a.Kind == "skill").ToList(), status = items.First(i => i.Id == "skills").Status },
            _ => new { error = "未知面板" }
        };
    }

    public object? WriteMemory(string sessionId, WorkspaceWritebackRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        if (string.IsNullOrWhiteSpace(request.Content))
            return new { error = "写回内容不能为空" };
        var item = new { id = Guid.NewGuid().ToString("N"), content = request.Content.Trim(), at = DateTimeOffset.Now };
        s.Memories.Add(item);
        Touch(s);
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return item;
    }

    public object? CommitDesign(string sessionId, WorkspaceWritebackRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        s.DesignCommitted = true;
        s.DesignPreview = request.Content ?? s.DesignPreview;
        Touch(s);
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return new { committed = true };
    }

    public object? DispatchTodo(string sessionId, string todoId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.Deleted) return null;
        s.Todos.Add(new { id = todoId, dispatched = true, at = DateTimeOffset.Now });
        Touch(s);
        Append(s, "workspace.updated", s.CurrentRun?.Id, WorkspacePayload(s));
        return new { todoId, dispatched = true };
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
        var registry = _ctx?.Get<IAgentRegistryService>();
        if (registry == null) return Array.Empty<CatalogAgentDto>();
        try
        {
            return registry.GetAllAgents()
                .Select(a => new CatalogAgentDto { Id = a.Id, Name = a.Name, Description = a.Description })
                .ToList();
        }
        catch
        {
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
            var id = Guid.NewGuid().ToString("N");
            s.CurrentRun.Approvals.Add(new ApprovalState { Id = id, Status = "pending" });
            s.Status = "awaiting_approval";
            s.CurrentRun.Status = "awaiting_approval";
            payload = MergeApprovalId(payload, id);
        }
        if (type == "run.stuck" && s.CurrentRun != null)
        {
            s.Status = "stuck";
            s.CurrentRun.Status = "stuck";
            s.CurrentRun.StuckReason = AgentFacadeEventMapper.ExtractText(payload) ?? payload?.ToString();
            s.CurrentRun.AllowedActions = new List<string> { "continue", "skip", "override", "retry", "cancel" };
            payload = MergeStuck(payload, runId, s.CurrentRun.AllowedActions);
        }
        if (type == "run.failed" && s.CurrentRun != null)
        {
            s.Status = "failed";
            s.CurrentRun.Status = "failed";
        }
        if (type == "run.completed" && s.CurrentRun != null)
        {
            s.Status = "completed";
            s.CurrentRun.Status = "completed";
        }
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
            NativeRunId = run.NativeRunId
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
        return AgentToolScopeRules.Narrow(whitelist, s.CurrentRun?.ToolScope);
    }

    public AgentEmbedContext? ContextSnapshot(string sessionId) =>
        !_sessions.TryGetValue(sessionId, out var s) || s.Deleted ? null : (s.CurrentRun?.ContextSnapshot ?? s.Context);

    private IReadOnlyList<string> VisibleTools(SessionState s) =>
        AgentToolScopeRules.Narrow(s.CurrentRun?.ToolScope?.ResolvedAllow(), s.CurrentRun?.ToolScope);

    private static object WorkspacePayload(SessionState s) => new { items = BuildWorkspace(s) };

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

    private static List<AgentWorkspaceStatusDto> BuildWorkspace(SessionState s)
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
        var project = string.IsNullOrWhiteSpace((run?.ContextSnapshot ?? s.Context).Cwd)
                      && string.IsNullOrWhiteSpace((run?.ContextSnapshot ?? s.Context).ProjectId)
            ? "未绑定"
            : ((run?.ContextSnapshot ?? s.Context).Title
               ?? (run?.ContextSnapshot ?? s.Context).Cwd
               ?? (run?.ContextSnapshot ?? s.Context).ProjectId
               ?? "已绑定");
        return new List<AgentWorkspaceStatusDto>
        {
            Item("plan", "计划", "先看步骤，确认后再执行", plan),
            Item("records", "执行记录", "本轮步骤、工具结果与轮次", records),
            Item("workflow", "工作流", "关联工作流的执行与控制", s.WorkflowStatus ?? "未启动"),
            Item("memory", "记忆", "本轮检索到的条目，可写回", s.Memories.Count == 0 ? "0" : s.Memories.Count.ToString()),
            Item("design", "设计", "组件与令牌预览，默认不写库", string.IsNullOrEmpty(s.DesignPreview) ? "无预览" : "有预览"),
            Item("suggest", "建议", "按最近习惯推荐下一步", s.Suggestions.Count == 0 ? "暂无" : s.Suggestions.Count.ToString()),
            Item("project", "项目", "当前工作目录、档案与命令", project),
            Item("todo", "待办", "可下发给智能体的任务", s.Todos.Count == 0 ? "0" : s.Todos.Count.ToString()),
            Item("skills", "技能", "本智能体已启用的技能", "已启用"),
            Item("tools", "工具", "本轮可见的宿主与 MCP 工具", VisibleToolsLabel(s)),
            Item("snippets", "代码片段", "本轮产出或收藏的片段", s.Snippets.Count == 0 ? "暂无" : s.Snippets.Count.ToString()),
        };
    }

    private static string VisibleToolsLabel(SessionState s)
    {
        var tools = AgentToolScopeRules.Narrow(s.CurrentRun?.ToolScope?.ResolvedAllow(), s.CurrentRun?.ToolScope);
        return tools.Count == 0 ? "未限定" : $"{tools.Count} 个可见";
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
            StuckReason = run.StuckReason,
            AllowedActions = run.AllowedActions
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
        public List<object> Todos { get; } = new();
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
}
