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
