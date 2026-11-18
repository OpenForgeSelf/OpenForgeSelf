using System.Text.Json;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Plugins.AIAgent.Tests;

public class AgentFacadeTests
{
    private static AgentFacadeService NewFacade() => new();

    private static CreateAgentSessionRequest SessionReq(
        string pluginId,
        string? surfaceKey = null,
        string mode = "open",
        string? title = null) => new()
    {
        Mode = mode,
        Title = title,
        Embed = new AgentEmbedOptions
        {
            Surface = "page",
            Context = new AgentEmbedContext
            {
                PluginId = pluginId,
                SurfaceKey = surfaceKey,
                Cwd = "/tmp/demo"
            }
        }
    };

    [Fact]
    public void ListSessions_FiltersByPluginIdAndSurfaceKey()
    {
        var svc = NewFacade();
        var page = svc.CreateSession(SessionReq("ai-agent", "default", title: "整页"));
        var termA = svc.CreateSession(SessionReq("smart-terminal", "tab-a", title: "终端A"));
        svc.CreateSession(SessionReq("smart-terminal", "tab-b", title: "终端B"));

        var pageList = svc.ListSessions("ai-agent", "default");
        Assert.Single(pageList);
        Assert.Equal(page.Id, pageList[0].Id);

        var tabA = svc.ListSessions("smart-terminal", "tab-a");
        Assert.Single(tabA);
        Assert.Equal(termA.Id, tabA[0].Id);
        Assert.Empty(svc.ListSessions("smart-terminal", "missing"));
        Assert.Empty(svc.ListSessions("db-admin", "default"));
    }

    [Fact]
    public void ListSessions_DefaultSurfaceKey_WhenOmitted()
    {
        var svc = NewFacade();
        svc.CreateSession(SessionReq("ai-agent", null, title: "缺省槽"));
        var list = svc.ListSessions("ai-agent", null);
        Assert.Single(list);
        Assert.Equal("default", list[0].SurfaceKey);
    }

    [Fact]
    public void CreateSession_RequiresPluginId()
    {
        var svc = NewFacade();
        Assert.Throws<ArgumentException>(() => svc.CreateSession(new CreateAgentSessionRequest()));
    }

    [Fact]
    public void PostMessage_ReturnsAcceptedAndDoesNotRunPlanBeforeConfirm()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent", "default", mode: "planned"));
        var (status, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "整理目录" });
        Assert.Equal(202, status);
        var accepted = Assert.IsType<AcceptedRunResponse>(body);
        Assert.False(string.IsNullOrWhiteSpace(accepted.RunId));

        var detail = svc.GetSession(session.Id)!;
        Assert.Equal("awaiting_plan", detail.Session.Status);
        Assert.Equal("awaiting_plan", detail.CurrentRun!.Status);
        Assert.NotNull(detail.CurrentRun.Plan);
        Assert.DoesNotContain(svc.ReplayEvents(session.Id, 0), e => e.Type == "step.started");

        var confirm = svc.ConfirmPlan(session.Id);
        Assert.IsType<AcceptedRunResponse>(confirm);
        detail = svc.GetSession(session.Id)!;
        Assert.Equal("running", detail.Session.Status);
        // 没有执行器时确认只切状态，不虚构步骤开始；过程事件由执行器推送。
        Assert.DoesNotContain(svc.ReplayEvents(session.Id, 0), e => e.Type == "step.started");
    }

    [Fact]
    public void PostMessage_OpenMode_Returns202AndStartsRunning()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var (status, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "你好" });
        Assert.Equal(202, status);
        Assert.IsType<AcceptedRunResponse>(body);
        Assert.Equal("running", svc.GetSession(session.Id)!.Session.Status);
    }

    [Fact]
    public void ToolScope_CanOnlyShrink()
    {
        var narrowed = AgentToolScopeRules.Narrow(
            new[] { "fs.read", "fs.write", "shell" },
            new AgentToolScope
            {
                AllowTools = new List<string> { "fs.read", "fs.write", "db.write" },
                DenyTools = new List<string> { "fs.write" }
            });
        Assert.Equal(new[] { "fs.read" }, narrowed);
    }

    [Fact]
    public void ContextAndToolScope_StoredOnRun_AndCannotMoveSurface()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("smart-terminal", "tab-a"));
        var (ok, _) = svc.PostMessage(session.Id, new PostAgentMessageRequest
        {
            Text = "看一下当前目录",
            Embed = new AgentEmbedOptions
            {
                Context = new AgentEmbedContext
                {
                    PluginId = "smart-terminal",
                    SurfaceKey = "tab-a",
                    Cwd = "/tmp/st"
                },
                ToolScope = new AgentToolScope { AllowTools = new List<string> { "st.ls" } }
            }
        });
        Assert.Equal(202, ok);
        var snap = svc.ContextSnapshot(session.Id);
        Assert.Equal("/tmp/st", snap!.Cwd);
        Assert.Equal(new[] { "st.ls" }, svc.VisibleTools(session.Id, new[] { "st.ls", "st.rm" }));
    }

    [Fact]
    public void PostMessage_RejectsSurfaceKeyChange_AfterIdle()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("smart-terminal", "tab-a", mode: "planned"));
        var (status, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "先出计划" });
        Assert.Equal(202, status);
        // 计划待确认，不算可改槽位；先取消再试
        var runId = ((AcceptedRunResponse)body).RunId;
        svc.StopRun(session.Id, runId);
        var (moved, err) = svc.PostMessage(session.Id, new PostAgentMessageRequest
        {
            Text = "换槽",
            Embed = new AgentEmbedOptions
            {
                Context = new AgentEmbedContext { PluginId = "smart-terminal", SurfaceKey = "tab-b" }
            }
        });
        Assert.Equal(400, moved);
        Assert.Contains("surfaceKey", err.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Events_ReplayAfterSeq()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent", mode: "planned"));
        svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "任务" });
        var all = svc.ReplayEvents(session.Id, 0);
        Assert.NotEmpty(all);
        var mid = all[0].Seq;
        var rest = svc.ReplayEvents(session.Id, mid);
        Assert.Equal(all.Count - 1, rest.Count);
        Assert.All(rest, e => Assert.True(e.Seq > mid));
    }

    [Fact]
    public void Stop_And_RejectApproval_DoNotExecute()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var (_, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "写文件" });
        var runId = ((AcceptedRunResponse)body).RunId;
        svc.Publish(session.Id, "approval.required", new { description = "写到敏感位置" }, runId);
        var detail = svc.GetSession(session.Id)!;
        Assert.Equal("awaiting_approval", detail.Session.Status);
        var ev = svc.ReplayEvents(session.Id, 0).Last(e => e.Type == "approval.required");
        // payload 带 approvalId
        Assert.NotNull(ev.Payload);

        svc.StopRun(session.Id, runId);
        Assert.Equal("cancelled", svc.GetSession(session.Id)!.Session.Status);
        Assert.Contains(svc.ReplayEvents(session.Id, 0), e => e.Type == "run.cancelled");
    }

    [Fact]
    public void Intervene_ContinueSkipCancel()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent", mode: "planned"));
        var (_, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "分步做" });
        var runId = ((AcceptedRunResponse)body).RunId;
        svc.ConfirmPlan(session.Id);
        svc.Publish(session.Id, "run.stuck", new { reason = "缺少路径" }, runId);
        Assert.Equal("stuck", svc.GetSession(session.Id)!.Session.Status);

        var cont = svc.Intervene(session.Id, runId, new InterveneRunRequest { Action = "continue", Input = "/tmp" });
        Assert.IsType<AcceptedRunResponse>(cont);
        Assert.Equal("running", svc.GetSession(session.Id)!.Session.Status);

        svc.Publish(session.Id, "run.stuck", new { reason = "仍缺" }, runId);
        var skip = svc.Intervene(session.Id, runId, new InterveneRunRequest { Action = "skip", StepId = "s1" });
        Assert.IsType<AcceptedRunResponse>(skip);

        svc.Intervene(session.Id, runId, new InterveneRunRequest { Action = "cancel" });
        Assert.Equal("cancelled", svc.GetSession(session.Id)!.Session.Status);
    }

    [Fact]
    public void Workspace_HasElevenPanels()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var detail = svc.GetSession(session.Id)!;
        Assert.Equal(11, detail.Workspace.Count);
        var ids = detail.Workspace.Select(w => w.Id).ToArray();
        foreach (var id in new[] { "plan", "records", "workflow", "memory", "design", "suggest", "project", "todo", "skills", "tools", "snippets" })
            Assert.Contains(id, ids);
    }

    [Fact]
    public void CreateSession_AcceptsPluginIdOnEmbedRoot()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(new CreateAgentSessionRequest
        {
            Embed = new AgentEmbedOptions { PluginId = "ai-agent", SurfaceKey = "panel-1" }
        });
        Assert.Equal("ai-agent", session.PluginId);
        Assert.Equal("panel-1", session.SurfaceKey);
    }

    [Fact]
    public void ToolScope_AllowDenyAliases_Shrink()
    {
        var narrowed = AgentToolScopeRules.Narrow(
            new[] { "fs.read", "fs.write", "shell" },
            new AgentToolScope
            {
                Allow = new List<string> { "fs.read", "fs.write", "db.write" },
                Deny = new List<string> { "fs.write" }
            });
        Assert.Equal(new[] { "fs.read" }, narrowed);
    }

    [Fact]
    public void RevisePlan_AcceptsNoteAsText()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent", mode: "planned"));
        svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "整理目录" });
        var plan = Assert.IsType<AgentPlanDto>(svc.RevisePlan(session.Id, new RevisePlanRequest { Note = "改成先备份" }));
        Assert.Equal("改成先备份", plan.Goal);
    }

    [Fact]
    public void PlanProposed_StepsExposeTitleAndDetail()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent", mode: "planned"));
        svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "整理目录" });
        var ev = svc.ReplayEvents(session.Id, 0).Single(e => e.Type == "plan.proposed");
        var plan = Assert.IsType<AgentPlanDto>(ev.Payload);
        Assert.Equal(plan.Steps[0].Name, plan.Steps[0].Title);
        Assert.Equal(plan.Steps[0].Objective, plan.Steps[0].Detail);
    }

    [Fact]
    public void WorkspaceUpdated_PayloadHasItems()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent", mode: "planned"));
        svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "任务" });
        var ev = svc.ReplayEvents(session.Id, 0).Last(e => e.Type == "workspace.updated");
        var itemsProp = ev.Payload!.GetType().GetProperty("items");
        Assert.NotNull(itemsProp);
        var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(itemsProp!.GetValue(ev.Payload));
        var list = items.Cast<AgentWorkspaceStatusDto>().ToList();
        Assert.Equal(11, list.Count);
        Assert.False(string.IsNullOrWhiteSpace(list[0].StatusText));
    }

    [Fact]
    public void Publish_MessageEvents_UseStableMessageId()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var (_, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "你好" });
        var runId = ((AcceptedRunResponse)body).RunId;
        svc.Publish(session.Id, "message.delta", new { text = "你" }, runId);
        svc.Publish(session.Id, "message.delta", new { text = "好" }, runId);
        svc.Publish(session.Id, "message.completed", new { text = "你好" }, runId);
        var evs = svc.ReplayEvents(session.Id, 0).Where(e => e.Type.StartsWith("message.")).ToList();
        Assert.NotEmpty(evs);
        Assert.All(evs, e =>
        {
            var mid = e.Payload!.GetType().GetProperty("messageId")!.GetValue(e.Payload)!.ToString();
            Assert.Equal("asst-" + runId, mid);
        });
    }

    [Fact]
    public void ListAgents_ReturnsRegisteredAgents()
    {
        var catalog = new StubAgentCatalog();
        catalog.Items.Add(new AgentDefinition
        {
            Id = "agent.generalist",
            Name = "通用助手",
            Description = "通用任务"
        });
        var list = new AgentFacadeService(agents: catalog).ListAgents();
        Assert.Equal("agent.generalist", Assert.Single(list).Id);
        Assert.Equal("通用助手", list[0].Name);
    }

    [Fact]
    public void Publish_StuckReason_IsPlainText()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var (_, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "你好" });
        var runId = ((AcceptedRunResponse)body).RunId;
        svc.Publish(session.Id, "run.stuck", new { reason = "未调用 complete_step", stepIndex = 0 }, runId);
        var stuck = svc.GetSession(session.Id)!.CurrentRun!.StuckReason;
        Assert.Equal("未调用 complete_step", stuck);
        Assert.DoesNotContain("AnonymousType", stuck);
        Assert.DoesNotContain("<>", stuck);
    }

    [Fact]
    public void Publish_ApprovalRequired_ReusesPayloadId()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var (_, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "写文件" });
        var runId = ((AcceptedRunResponse)body).RunId;
        svc.Publish(session.Id, "approval.required", new { approvalId = "42", description = "写到敏感位置", action = "write_file" }, runId);
        var ev = svc.ReplayEvents(session.Id, 0).Last(e => e.Type == "approval.required");
        var id = ev.Payload!.GetType().GetProperty("approvalId")!.GetValue(ev.Payload)!.ToString();
        Assert.Equal("42", id);
        var decided = svc.DecideApproval(session.Id, "42", new DecideApprovalRequest { Approved = false, Note = "先不写" });
        Assert.NotNull(decided);
        Assert.DoesNotContain("error", decided!.GetType().GetProperties().Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("rejected", decided.GetType().GetProperty("Status")!.GetValue(decided)!.ToString());
    }

    [Fact]
    public void AiAgent_DefaultTools_AreListedWhenToolScopeOmitted()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var names = svc.VisibleTools(session.Id, null);
        Assert.Contains("list_files", names);
        Assert.Contains("write_file", names);
        var tools = svc.GetSession(session.Id)!.Workspace.Single(w => w.Id == "tools");
        Assert.NotEqual("未限定", tools.Status);
        Assert.Contains("可见", tools.Status);

        var toolsJson = JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "tools"));
        var toolNames = toolsJson.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("list_files", toolNames);
        Assert.Contains("write_file", toolNames);
        Assert.Contains(toolsJson.GetProperty("mcp").EnumerateArray(), m => m.GetProperty("status").GetString() == "未连接");
    }

    [Fact]
    public void Cancel_ClearsStuckCardAndRecords()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent", mode: "planned"));
        var (_, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "分步做" });
        var runId = ((AcceptedRunResponse)body).RunId;
        svc.ConfirmPlan(session.Id);
        svc.Publish(session.Id, "run.stuck", new { reason = "缺少路径" }, runId);
        svc.Intervene(session.Id, runId, new InterveneRunRequest { Action = "cancel" });

        var detail = svc.GetSession(session.Id)!;
        Assert.Equal("cancelled", detail.Session.Status);
        Assert.Equal("cancelled", detail.CurrentRun!.Status);
        Assert.Null(detail.CurrentRun.StuckReason);
        Assert.Null(detail.CurrentRun.AllowedActions);
        var records = detail.Workspace.Single(w => w.Id == "records");
        Assert.Equal("已取消", records.Status);
        Assert.All(detail.CurrentRun.Plan!.Steps, step => Assert.Equal("cancelled", step.Status));

        svc.Publish(session.Id, "run.stuck", new { reason = "迟到的卡住" }, runId);
        var after = svc.GetSession(session.Id)!;
        Assert.Equal("cancelled", after.Session.Status);
        Assert.Equal("cancelled", after.CurrentRun!.Status);
        Assert.Null(after.CurrentRun.StuckReason);
        Assert.Equal(1, svc.ReplayEvents(session.Id, 0).Count(e => e.Type == "run.stuck"));
        Assert.Contains(svc.ReplayEvents(session.Id, 0), e => e.Type == "run.cancelled");
    }

    [Fact]
    public void AgentFail_ThenRetry_LeavesFailedState()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var (status, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "请执行 agent-fail" });
        Assert.Equal(202, status);
        var runId = ((AcceptedRunResponse)body).RunId;
        var failed = svc.GetSession(session.Id)!;
        Assert.Equal("failed", failed.Session.Status);
        Assert.Equal("failed", failed.CurrentRun!.Status);
        Assert.Contains("agent-fail", failed.CurrentRun.FailureReason);
        Assert.Contains("retry", failed.CurrentRun.AllowedActions!);
        Assert.Contains(svc.ReplayEvents(session.Id, 0), e => e.Type == "run.failed");

        var retried = svc.Intervene(session.Id, runId, new InterveneRunRequest { Action = "retry" });
        Assert.IsType<AcceptedRunResponse>(retried);
        var running = svc.GetSession(session.Id)!;
        Assert.Equal("running", running.Session.Status);
        Assert.Null(running.CurrentRun!.FailureReason);
    }

    [Fact]
    public void WorkspaceKeywords_SeedWalkablePanels()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(new CreateAgentSessionRequest
        {
            Mode = "open",
            Embed = new AgentEmbedOptions
            {
                Surface = "page",
                Context = new AgentEmbedContext { PluginId = "ai-agent", SurfaceKey = "default" }
            }
        });
        var project = svc.GetSession(session.Id)!.Workspace.Single(w => w.Id == "project");
        Assert.Equal("未绑定", project.Status);

        svc.PostMessage(session.Id, new PostAgentMessageRequest
        {
            Text = "请执行 agent-workflow agent-design agent-suggest agent-project agent-todo"
        });
        var detail = svc.GetSession(session.Id)!;
        Assert.Equal("running", detail.Workspace.Single(w => w.Id == "workflow").Status);
        Assert.Equal("有预览", detail.Workspace.Single(w => w.Id == "design").Status);
        Assert.Equal("3", detail.Workspace.Single(w => w.Id == "suggest").Status);
        Assert.NotEqual("未绑定", detail.Workspace.Single(w => w.Id == "project").Status);
        Assert.Equal("1", detail.Workspace.Single(w => w.Id == "todo").Status);

        var workflow = JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "workflow"));
        Assert.Equal("wf-itest", workflow.GetProperty("workflowId").GetString());
        Assert.Contains(workflow.GetProperty("steps").EnumerateArray(), st => st.GetProperty("name").GetString() == "写入结果");
        var design = JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "design"));
        Assert.Contains("Button", design.GetProperty("preview").GetString());
        Assert.False(design.GetProperty("committed").GetBoolean());
        var suggest = JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "suggest"));
        var actions = suggest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()).ToList();
        Assert.Contains("open_workflow", actions);
        Assert.Contains("write_memory", actions);
        Assert.Contains("dispatch_todo", actions);
        var dispatched = svc.DispatchTodo(session.Id, "todo-itest");
        Assert.NotNull(dispatched);
        var todoAfter = JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "todo"));
        var todos = todoAfter.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(todos);
        Assert.Equal("todo-itest", todos[0].GetProperty("id").GetString());
        Assert.Equal("整理联调纪要", todos[0].GetProperty("title").GetString());
        Assert.True(todos[0].GetProperty("dispatched").GetBoolean());
        Assert.Equal("ai-agent", todos[0].GetProperty("assignee").GetString());
        var committed = svc.CommitDesign(session.Id, new WorkspaceWritebackRequest { Content = "ok" });
        Assert.NotNull(committed);
        Assert.True(JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "design")).GetProperty("committed").GetBoolean());
    }


    [Fact]
    public void RejectApproval_MarksCancelledAndDoesNotComplete()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        var (_, body) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "写文件" });
        var runId = ((AcceptedRunResponse)body).RunId;
        svc.Publish(session.Id, "approval.required", new { approvalId = "7", action = "write_file" }, runId);
        var decided = svc.DecideApproval(session.Id, "7", new DecideApprovalRequest { Approved = false, Note = "取消" });
        Assert.Equal("rejected", decided!.GetType().GetProperty("Status")!.GetValue(decided)!.ToString());
        var after = svc.GetSession(session.Id)!;
        Assert.Equal("cancelled", after.Session.Status);
        Assert.Equal("cancelled", after.CurrentRun!.Status);
        Assert.DoesNotContain(svc.ReplayEvents(session.Id, 0), e => e.Type == "run.completed");
        svc.Publish(session.Id, "run.completed", new { text = "工具已执行完毕。" }, runId);
        var late = svc.GetSession(session.Id)!;
        Assert.Equal("cancelled", late.Session.Status);
        Assert.Equal("cancelled", late.CurrentRun!.Status);
    }

    [Fact]
    public void PauseAndCancelWorkflow_UpdateMatchingSteps()
    {
        var svc = NewFacade();
        var session = svc.CreateSession(SessionReq("ai-agent"));
        svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "请执行 agent-workflow" });
        var runId = svc.GetSession(session.Id)!.CurrentRun!.Id;
        svc.Publish(session.Id, "run.completed", new { text = "工具已执行完毕。" }, runId);
        Assert.Equal("running", svc.GetSession(session.Id)!.Session.Status);

        var paused = JsonSerializer.SerializeToElement(svc.PauseWorkflow(session.Id, "wf-itest"));
        Assert.Equal("paused", paused.GetProperty("status").GetString());
        Assert.Equal("completed", paused.GetProperty("steps")[0].GetProperty("status").GetString());
        Assert.Equal("paused", paused.GetProperty("steps")[1].GetProperty("status").GetString());
        Assert.Equal("paused", svc.GetSession(session.Id)!.Workspace.Single(w => w.Id == "workflow").Status);
        Assert.Equal("running", svc.GetSession(session.Id)!.Session.Status);
        var wfPanel = JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "workflow"));
        Assert.Equal("paused", wfPanel.GetProperty("workflowStatus").GetString());

        var cancelled = JsonSerializer.SerializeToElement(svc.CancelWorkflow(session.Id, "wf-itest"));
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal("completed", cancelled.GetProperty("steps")[0].GetProperty("status").GetString());
        Assert.Equal("cancelled", cancelled.GetProperty("steps")[1].GetProperty("status").GetString());
        Assert.Equal("cancelled", svc.GetSession(session.Id)!.Workspace.Single(w => w.Id == "workflow").Status);
        Assert.Equal("cancelled", svc.GetSession(session.Id)!.Session.Status);
        Assert.Equal("cancelled", svc.GetSession(session.Id)!.CurrentRun!.Status);
    }

    [Fact]
    public async Task AgentRunning_HoldsInProgressThenCompletes()
    {
        var prev = AgentFacadeService.RunningHoldDuration;
        AgentFacadeService.RunningHoldDuration = TimeSpan.FromMilliseconds(40);
        try
        {
            var svc = NewFacade();
            var session = svc.CreateSession(SessionReq("ai-agent"));
            var (status, _) = svc.PostMessage(session.Id, new PostAgentMessageRequest { Text = "请执行 agent-running" });
            Assert.Equal(202, status);
            var running = svc.GetSession(session.Id)!;
            Assert.Equal("running", running.Session.Status);
            Assert.StartsWith("进行中", running.Workspace.Single(w => w.Id == "records").Status);
            Assert.True((running.CurrentRun?.Turn ?? 0) >= 1);
            var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var rec = JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "records"), jsonOpts);
            Assert.True(rec.GetProperty("round").GetInt32() >= 1);
            Assert.Equal(20, rec.GetProperty("turnMax").GetInt32());
            var steps = rec.GetProperty("steps");
            Assert.True(steps.GetArrayLength() >= 1);
            Assert.Contains(steps.EnumerateArray(), st => st.GetProperty("status").GetString() == "running");
            await Task.Delay(200);
            var done = svc.GetSession(session.Id)!;
            Assert.Equal("completed", done.Session.Status);
            Assert.Equal("已完成", done.Workspace.Single(w => w.Id == "records").Status);
            var doneRec = JsonSerializer.SerializeToElement(svc.GetWorkspace(session.Id, "records"), jsonOpts);
            Assert.DoesNotContain(doneRec.GetProperty("steps").EnumerateArray(), st => st.GetProperty("status").GetString() is "running" or "pending");
        }
        finally
        {
            AgentFacadeService.RunningHoldDuration = prev;
        }
    }


}

internal sealed class StubAgentCatalog : IAgentRegistryService
{
    public List<AgentDefinition> Items { get; } = new();
    public void RegisterAgent(AgentDefinition agent) => Items.Add(agent);
    public void UnregisterAgent(string agentId) => Items.RemoveAll(a => a.Id == agentId);
    public AgentDefinition? GetAgent(string agentId) => Items.FirstOrDefault(a => a.Id == agentId);
    public List<AgentDefinition> GetAllAgents() => Items.ToList();
    public List<AgentDefinition> GetAgentsByType(AgentType type) => Items.Where(a => a.Type == type).ToList();
    public List<AgentDefinition> FindAgentsByCapability(string capability) => Items.ToList();
    public AgentDefinition? GetBestAgentForTask(string taskDescription, List<string> requiredCapabilities) => Items.FirstOrDefault();
    public AgentDefinition CreateAgent(AgentDefinition agent) { Items.Add(agent); return agent; }
    public AgentDefinition? UpdateAgent(string agentId, AgentDefinition agent) => agent;
    public bool DeleteAgent(string agentId) => Items.RemoveAll(a => a.Id == agentId) > 0;
}
