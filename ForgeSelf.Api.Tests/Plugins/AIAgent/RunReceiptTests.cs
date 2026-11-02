using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.MafLoop;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// B2③ 门禁：开放任务回合的结果收据（run/receipt）与出口验收（run/verify）轨迹。
/// 判据照 MafTaskLoopAgentTests 纪律：一切持久事实打在会话日志事件序上。
/// </summary>
/// <remarks>
/// 覆盖：① 工具回合（含 write_file）→ run/receipt 冻结在 turn/end 之前（文件清单/工具计数/摘要）；
/// ② 纯问答回合不产收据（现状行为不变）；③ ExitCriteria 配置 → run/verify 落盘（Passed/Failed）；
/// ④ 校验器缺席 = fail-safe Failed（BR-2 不假绿）。
/// </remarks>
[Collection("XCode")]
public class RunReceiptTests : IClassFixture<XCodeTestFixture>
{
    public RunReceiptTests(XCodeTestFixture fixture) => _ = fixture;

    private static readonly string DemoPath = @"D:\tmp\b2-receipt-demo.txt";

    /// <summary>标准两段脚本：create_file 建 demo 文件（非高风险名单，免审批直执行）→ task_complete 出口。</summary>
    private static (Script CreateFile, Script Complete) WriteFileThenCompleteScript()
    {
        return (
            Script.ToolCall("c1", "create_file", JsonSerializer.Serialize(new { path = DemoPath, content = "demo" })),
            Script.ToolCall("exit-1", "task_complete", "{\"summary\":\"文件已写入，任务完成\"}"));
    }

    private static void EnqueueWriteFileScript(MafFixture fx)
    {
        var (writeFile, complete) = WriteFileThenCompleteScript();
        fx.Provider.Enqueue(writeFile);
        fx.Provider.Enqueue(complete);
    }

    // AC-5a：工具回合冻结收据——位置在 turn/end 之前，内容带文件清单与工具计数
    [Fact]
    public async Task ToolTurn_FreezesReceiptBeforeTurnEnd()
    {
        var fx = new MafFixture();
        EnqueueWriteFileScript(fx);

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "写个 demo 文件然后完成任务");
        await fx.RunAsync(agent);

        var events = fx.Store.Replay(fx.SessionId);
        var receipt = Assert.Single(events.OfType<RunReceiptEvent>());
        var turnEndIndex = events.ToList().FindIndex(e => e is TurnEndEvent);
        var receiptIndex = events.ToList().IndexOf(receipt);
        Assert.True(receiptIndex < turnEndIndex && receiptIndex > 0, "run/receipt 必须落在 turn/end 之前");

        using var doc = JsonDocument.Parse(receipt.ReceiptJson);
        var root = doc.RootElement;
        Assert.Equal("Completed", root.GetProperty("status").GetString());
        Assert.Equal("文件已写入，任务完成", root.GetProperty("summary").GetString());

        var files = root.GetProperty("filesChanged");
        Assert.Equal(1, files.GetArrayLength());
        Assert.Equal(DemoPath, files[0].GetProperty("path").GetString());
        Assert.Equal("A", files[0].GetProperty("change").GetString());

        Assert.Equal(2, root.GetProperty("toolCallCount").GetInt32());   // create_file + task_complete
        Assert.Equal(2, root.GetProperty("toolOkCount").GetInt32());
    }

    // AC-5b：纯问答回合（零工具调用）不产收据、不落 verify——现状行为不变
    [Fact]
    public async Task PureQATurn_NoReceiptNoVerify()
    {
        var fx = new MafFixture();
        fx.Provider.Enqueue(Script.Text("你好，有什么可以帮你？", finish: "stop"));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "你好");
        await fx.RunAsync(agent);

        var events = fx.Store.Replay(fx.SessionId);
        Assert.DoesNotContain(events, e => e is RunReceiptEvent);
        Assert.DoesNotContain(events, e => e is RunVerifyEvent);
    }

    // AC-5c：ExitCriteria 全过 → run/verify Passed（run/receipt 之后、turn/end 之前）
    [Fact]
    public async Task ExitCriteria_Passed_RunVerifyEventRecorded()
    {
        var fx = new MafFixture();
        EnqueueWriteFileScript(fx);
        fx.RunVerify = new RunVerifyService(null);

        var agent = fx.CreateAgent(options: new AgentOptions
        {
            ExitCriteria =
            [
                new RunExitCriterion("本次必须有文件改动", "files_changed_not_empty"),
                new RunExitCriterion("任务完成", "output_contains")
            ]
        });
        agent.Inbox.Followup(fx.SessionId, "写文件并完成任务");
        await fx.RunAsync(agent);

        var events = fx.Store.Replay(fx.SessionId).ToList();
        var verify = Assert.Single(events.OfType<RunVerifyEvent>());
        var receipt = Assert.Single(events.OfType<RunReceiptEvent>());
        var turnEndIndex = events.FindIndex(e => e is TurnEndEvent);

        Assert.Equal(RunVerifyStatus.Passed.ToString(), verify.Status);
        Assert.All(verify.Items, i => Assert.True(i.Passed));
        Assert.True(events.IndexOf(verify) > events.IndexOf(receipt), "先收据后验收");
        Assert.True(events.IndexOf(verify) < turnEndIndex, "run/verify 必须落在 turn/end 之前");
    }

    // AC-5d：ExitCriteria 未过 → run/verify Failed（逐条明细可读），回合照常收束（开放任务无 Run 状态机）
    [Fact]
    public async Task ExitCriteria_Failed_RunVerifyFailedWithDetails()
    {
        var fx = new MafFixture();
        EnqueueWriteFileScript(fx);
        fx.RunVerify = new RunVerifyService(null);

        var agent = fx.CreateAgent(options: new AgentOptions
        {
            ExitCriteria =
            [
                new RunExitCriterion("必须出现『验收通过』字样", "output_contains")
            ]
        });
        agent.Inbox.Followup(fx.SessionId, "写文件并完成任务");
        await fx.RunAsync(agent);

        var verify = Assert.Single(fx.Store.Replay(fx.SessionId).OfType<RunVerifyEvent>());
        Assert.Equal(RunVerifyStatus.Failed.ToString(), verify.Status);
        var item = Assert.Single(verify.Items);
        Assert.False(item.Passed);
        Assert.Contains("产出不包含", item.Detail);
    }

    // AC-5e：判据已配置但校验器缺席 → fail-safe Failed（BR-2：不验证不放行，绝不假绿）
    [Fact]
    public async Task ExitCriteria_VerifierAbsent_FailsSafe()
    {
        var fx = new MafFixture();
        EnqueueWriteFileScript(fx);
        // fx.RunVerify 保持 null：模拟宿主未接线校验器

        var agent = fx.CreateAgent(options: new AgentOptions
        {
            ExitCriteria = [new RunExitCriterion("本次必须有文件改动", "files_changed_not_empty")]
        });
        agent.Inbox.Followup(fx.SessionId, "写文件并完成任务");
        await fx.RunAsync(agent);

        var verify = Assert.Single(fx.Store.Replay(fx.SessionId).OfType<RunVerifyEvent>());
        Assert.Equal(RunVerifyStatus.Failed.ToString(), verify.Status);
        var item = Assert.Single(verify.Items);
        Assert.False(item.Passed);
        Assert.Contains("校验器缺席", item.Text); // fail-safe 结论打在判据文本上，接线原因在 Detail
        Assert.Contains("校验器未接线", item.Detail);
    }
}
