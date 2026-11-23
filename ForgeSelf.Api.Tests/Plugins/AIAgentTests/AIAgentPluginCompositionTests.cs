using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgentTests;

/// <summary>
/// 组合根装配守卫（PC-040 守卫重建 · 灾后按意图重实现 2026-11-23）：
/// ① 组合根对 AgentFacadeRunExecutor 的构造必须全显式命名实参（可选参省参即 PC-040 形态：
///    漏传 planGenerator → 规划静默降级、仅真实装配路径暴露）；
/// ② 规划回合行为锁：真规划回写 / 规划器不可用回退草稿并标注 / 确认后回写拒收。
/// </summary>
public class AIAgentPluginCompositionTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx）");
    }

    [Fact]
    public void CompositionRoot_ConstructsExecutor_WithExplicitOptionalArgs()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "Plugins", "AIAgent", "AIAgentPlugin.cs"));
        var line = src.Split('\n').FirstOrDefault(l => l.Contains("new AgentFacadeRunExecutor("));
        line.Should().NotBeNull("组合根应构造 AgentFacadeRunExecutor");
        line!.Should().Contain("registry:", "可选参必须显式命名（PC-040：省参即漏装配形态）");
        line.Should().Contain("planGenerator:", "漏传 planGenerator = 规划静默降级（PC-040 病根）");
    }

    [Fact]
    public async Task StartPlanningAsync_WithGenerator_PublishesRealPlan()
    {
        var facade = new AgentFacadeService(executor: null);
        var session = facade.CreateSession(new CreateAgentSessionRequest
        {
            Title = "planning-guard",
            Mode = "planned",
            Embed = new AgentEmbedOptions { PluginId = "AIAgent" }
        });
        var post = facade.PostMessage(session.Id, new PostAgentMessageRequest { Text = "整理下载目录并出报告" });
        var runId = ((AcceptedRunResponse)post.Body).RunId;

        var executor = new AgentFacadeRunExecutor(facade, ctx: null, planGenerator: new FakePlanGenerator());
        await executor.StartPlanningAsync(session.Id, runId, CancellationToken.None);

        var plan = facade.GetSession(session.Id)!.CurrentRun!.Plan!;
        plan.Goal.Should().Be("真计划");
        plan.Steps.Should().HaveCount(2);
        plan.Steps[0].Name.Should().Be("探索");
        plan.Note.Should().BeNull("真规划不带回退标注");
    }

    [Fact]
    public async Task StartPlanningAsync_WithoutGenerator_FallsBackToDraftWithNote()
    {
        var facade = new AgentFacadeService(executor: null);
        var session = facade.CreateSession(new CreateAgentSessionRequest
        {
            Title = "planning-guard-fallback",
            Mode = "planned",
            Embed = new AgentEmbedOptions { PluginId = "AIAgent" }
        });
        var post = facade.PostMessage(session.Id, new PostAgentMessageRequest { Text = "做个部署" });
        var runId = ((AcceptedRunResponse)post.Body).RunId;

        var executor = new AgentFacadeRunExecutor(facade, ctx: null, planGenerator: null);
        await executor.StartPlanningAsync(session.Id, runId, CancellationToken.None);

        var plan = facade.GetSession(session.Id)!.CurrentRun!.Plan!;
        plan.Note.Should().Be("规划失败，已用回退计划", "静默降级禁令：规划器不可用必须显式标注");
        plan.Steps.Should().HaveCount(2, "回退计划=会话草稿（准备/执行）");
    }

    [Fact]
    public void UpdatePlannedPlan_RejectedAfterConfirm()
    {
        var facade = new AgentFacadeService(executor: null);
        var session = facade.CreateSession(new CreateAgentSessionRequest
        {
            Title = "planning-guard-late",
            Mode = "planned",
            Embed = new AgentEmbedOptions { PluginId = "AIAgent" }
        });
        var post = facade.PostMessage(session.Id, new PostAgentMessageRequest { Text = "确认竞态" });
        var runId = ((AcceptedRunResponse)post.Body).RunId;
        facade.ConfirmPlan(session.Id).Should().NotBeNull();

        var late = facade.UpdatePlannedPlan(session.Id, runId, new AgentPlanDto { Goal = "迟到计划" });
        late.Should().BeFalse("用户已确认进入执行，迟到的规划产出不得覆盖进行中轮次");
        facade.GetSession(session.Id)!.CurrentRun!.Plan!.Goal.Should().NotBe("迟到计划");
    }

    private sealed class FakePlanGenerator : IPlanGeneratorService
    {
        public Task<AgentPlan> GeneratePlanAsync(AgentRun run, long workflowId, string? chatModelId, CancellationToken ct)
            => Task.FromResult(new AgentPlan
            {
                Goal = "真计划",
                Steps =
                {
                    new AgentPlanStep { Id = "p1", Name = "探索", Objective = "列出文件" },
                    new AgentPlanStep { Id = "p2", Name = "交付", Objective = "写报告" }
                }
            });
    }
}
