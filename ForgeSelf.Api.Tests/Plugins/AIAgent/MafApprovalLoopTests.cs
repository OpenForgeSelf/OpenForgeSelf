using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.MafLoop;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.AI;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// P0 MAF 审批接线：高风险工具包 ApprovalRequiredAIFunction；未批准不执行；
/// 泵落 ToolApprovalRequestContent 挂起；批准后原参数续跑；拒绝喂回。
/// </summary>
[Collection("XCode")]
public class MafApprovalLoopTests
{
    [Fact]
    public void HighRiskTools_AreWrappedAsApprovalRequiredAIFunction()
    {
        AIFunction inner = new RegistryToolFunction(
            "write_file", "写文件",
            JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}"),
            new FakeToolRegistry(), "s1",
            new MafLoopBridge { Store = new InMemorySessionStore(), SessionId = "s1" },
            "t1");

        AITool wrapped = HighRiskToolPolicy.RequiresApproval("write_file")
            ? new ApprovalRequiredAIFunction(inner)
            : inner;

        Assert.IsType<ApprovalRequiredAIFunction>(wrapped);
        Assert.Equal("write_file", ((ApprovalRequiredAIFunction)wrapped).Name);

        AIFunction read = new RegistryToolFunction(
            "read_file", "读文件",
            JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}"),
            new FakeToolRegistry(), "s1",
            new MafLoopBridge { Store = new InMemorySessionStore(), SessionId = "s1" },
            "t1");
        AITool readTool = HighRiskToolPolicy.RequiresApproval("read_file")
            ? new ApprovalRequiredAIFunction(read)
            : read;
        Assert.IsType<RegistryToolFunction>(readTool);
    }

    [Fact]
    public async Task UnapprovedHighRiskTool_DoesNotExecute_AndSuspendsTurn()
    {
        var fx = new MafApprovalFixture();
        fx.Provider.Enqueue(Script.ToolCall("c1", "write_file", """{"path":"a.txt","content":"secret"}"""));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "写一个文件");
        var frames = await fx.RunAsync(agent);

        // 未批准：工具不得进入六闸门执行器
        Assert.Empty(fx.Tools.Executions);
        Assert.Contains(frames, f => f is TurnCompleted { Reason: TurnEndReason.Suspended });
        Assert.Contains(fx.EventTypes(), t => t == "tool/approval-request");

        var pending = await fx.Approvals.ListBySessionAsync(fx.SessionId);
        Assert.Single(pending);
        Assert.Equal(nameof(ApprovalStatus.Pending), pending[0].Status);
        Assert.Equal("write_file", pending[0].ToolName);
        Assert.Contains("secret", pending[0].ArgsJson);
    }

    [Fact]
    public async Task Approve_ThenContinue_ExecutesWithOriginalArgs()
    {
        var fx = new MafApprovalFixture();
        fx.Provider.Enqueue(Script.ToolCall("c1", "write_file", """{"path":"a.txt","content":"orig-args"}"""));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "写文件");
        await fx.RunAsync(agent);
        Assert.Empty(fx.Tools.Executions);

        var pending = Assert.Single(await fx.Approvals.ListBySessionAsync(fx.SessionId));
        await fx.Approvals.DecideAsync(pending.Id, approved: true, note: null);

        // 批准后续跑：模型可继续；注入的 ToolApprovalResponseContent 会触发原参数执行
        fx.Provider.Enqueue(Script.ToolCall("exit-1", "task_complete", """{"summary":"写完了"}"""));
        agent.Inbox.Followup(fx.SessionId, "继续");
        await fx.RunAsync(agent);

        var writes = fx.Tools.Executions.Where(e => e.Name == "write_file").ToList();
        Assert.NotEmpty(writes);
        Assert.All(writes, w => Assert.Contains("orig-args", w.Args));
    }

    [Fact]
    public async Task Deny_ThenContinue_DoesNotExecuteTool()
    {
        var fx = new MafApprovalFixture();
        fx.Provider.Enqueue(Script.ToolCall("c1", "run_terminal_command", """{"command":"rm -rf /"}"""));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "跑危险命令");
        await fx.RunAsync(agent);

        var pending = Assert.Single(await fx.Approvals.ListBySessionAsync(fx.SessionId));
        await fx.Approvals.DecideAsync(pending.Id, approved: false, note: "拒绝");

        fx.Provider.Enqueue(Script.ToolCall("exit-1", "task_complete", """{"summary":"已取消"}"""));
        agent.Inbox.Followup(fx.SessionId, "继续");
        await fx.RunAsync(agent);

        Assert.DoesNotContain(fx.Tools.Executions, e => e.Name == "run_terminal_command");
    }
}

/// <summary>带 PersistentApprovalService 的 MAF 夹具（高风险工具定义）。</summary>
internal sealed class MafApprovalFixture
{
    public string SessionId { get; } = "s-appr-" + Guid.NewGuid().ToString("N")[..8];
    public InMemorySessionStore Store { get; } = new();
    public ScriptedProvider Provider { get; } = new();
    public FakeToolRegistry Tools { get; } = new();
    public EventBus Events { get; } = new();
    public InMemoryInbox Inbox { get; } = new();
    public PersistentApprovalService Approvals { get; } = new();

    public MafApprovalFixture()
    {
        _ = new XCodeTestFixture();
    }

    public MafTaskLoopAgent CreateAgent()
        => new(SessionId,
            new AgentOptions { MaxStepsPerTurn = 10, TaskLoopMode = true },
            new AgentTurnRuntime
            {
                Store = Store,
                Provider = Provider,
                ModelId = "fake-model",
                ToolExecutor = Tools,
                SystemPrompt = null,
                ToolDefinitions = new List<UnifiedToolDefinition>
                {
                    new()
                    {
                        Name = "write_file",
                        Description = "写文件",
                        Parameters = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}")
                    },
                    new()
                    {
                        Name = "run_terminal_command",
                        Description = "终端",
                        Parameters = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}")
                    },
                    new()
                    {
                        Name = "get_current_time",
                        Description = "时间",
                        Parameters = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}")
                    },
                },
                Events = Events,
                Inbox = Inbox,
                ApprovalService = Approvals
            });

    public async Task<List<TurnFrame>> RunAsync(IAgent agent, CancellationToken ct = default)
    {
        var frames = new List<TurnFrame>();
        await foreach (var frame in agent.RunAsync(ct))
            frames.Add(frame);
        return frames;
    }

    public List<string> EventTypes()
        => Store.Replay(SessionId).Select(e => e.Type).ToList();
}
