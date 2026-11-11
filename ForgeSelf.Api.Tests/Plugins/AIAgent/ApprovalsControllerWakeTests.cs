using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Controllers;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// P2② 审批唤醒（B4）：ApprovalsController.Decide 决策写库后必须自动唤醒该会话 Agent 续跑。
/// 缺陷：Decide 仅 DecideAsync + 日志，无人触发第二轮 → 用户批准/拒绝后回合永远挂着。
/// 机制：Agent 存在且非运行 ⇒ Inbox.Followup 唤醒输入 + 后台 RunAsync（回合启动即
/// ConsumeContinuationsAsync 取已决策审批续跑；批准解包 ApprovalRequired / 拒绝喂回失败结果）。
/// </summary>
[Collection("XCode")]
public class ApprovalsControllerWakeTests
{
    /// <summary>最小 IContext：字典注册表（铁律 12c 同构——宿主契约运行期解析）。</summary>
    private sealed class FakeContext : IContext
    {
        private readonly Dictionary<Type, object> _services = new();

        public FakeContext With<TService>(TService instance) where TService : class
        {
            _services[typeof(TService)] = instance;
            return this;
        }

        public T? Get<T>() where T : class => _services.TryGetValue(typeof(T), out var v) ? (T)v : null;
        public object? GetService(Type serviceType) => _services.TryGetValue(serviceType, out var v) ? v : null;
        public void Register<TService>(TService instance) where TService : class => _services[typeof(TService)] = instance;
        public void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new() => _services[typeof(TService)] = new TImpl();
        public void RegisterLocal<TService>(TService instance) where TService : class { }
        public IDisposable Effect(Func<IDisposable> sideEffect) => sideEffect();
        public IEventBus Events => throw new NotSupportedException();
        public IContext Derive() => throw new NotSupportedException();
    }

    /// <summary>Status=Running 的桩：验证运行中不注入唤醒（并发守卫不让路）。</summary>
    private sealed class RunningAgentStub : IAgent
    {
        public string SessionId { get; init; } = "s-running";
        public AgentOptions Options => new();
        public IInbox Inbox => throw new NotSupportedException("桩不提供收件箱");
        public AgentStatus Status => AgentStatus.Running;
        public IAsyncEnumerable<TurnFrame> RunAsync(CancellationToken ct = default) => throw new NotSupportedException("桩不提供回合");
        public Task CancelAsync(AgentCancelCause cause, bool keepInbox = false) => Task.CompletedTask;
        public Task WhenIdleAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>拉起一次真实回合：工具调用 → 未批准挂起 → 落 1 条 Pending。</summary>
    private static async Task<(MafApprovalFixture Fx, IAgent Agent, long PendingId)> SuspendOnceAsync()
    {
        var fx = new MafApprovalFixture();
        fx.Provider.Enqueue(Script.ToolCall("c1", "write_file", """{"path":"a.txt","content":"wake"}"""));
        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "写文件");
        await fx.RunAsync(agent);

        var pending = Assert.Single(await fx.Approvals.ListBySessionAsync(fx.SessionId));
        Assert.Equal(nameof(ApprovalStatus.Pending), pending.Status);
        return (fx, agent, pending.Id);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string because)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(50);
        }
        Assert.True(condition(), $"等待超时（{because}）");
    }

    [Fact]
    public async Task Decide_Approved_WakesIdleAgent_BackgroundRunExecutesOriginalArgs()
    {
        var (fx, agent, pendingId) = await SuspendOnceAsync();
        var registry = new AgentRuntimeRegistry((_, _) => agent);
        await registry.GetOrCreateAsync(fx.SessionId); // 生产语义：Agent 创建时必经注册表登记，TryGetAsync 才能探到
        var ctx = new FakeContext()
            .With<IApprovalService>(fx.Approvals)
            .With<IAgentRegistry>(registry);
        fx.Provider.Enqueue(Script.ToolCall("exit-1", "task_complete", """{"summary":"写完了"}"""));

        var controller = new ApprovalsController(ctx);
        var result = await controller.Decide(
            pendingId,
            new ApprovalsController.DecideBody { Approved = true },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.IsType<ApprovalsController.ApprovalApiItem>(ok.Value);
        Assert.True(item.Resumed, "决策成功且代理空闲时应自动唤醒续跑");
        Assert.Equal(nameof(ApprovalStatus.Approved), item.Status);

        // 火后不管的后台回合必须真实执行原参数工具（轮询等待收束）
        await WaitUntilAsync(
            () => fx.Tools.Executions.Any(e => e.Name == "write_file"),
            TimeSpan.FromSeconds(10),
            "决策后后台回合应执行 write_file");
        var writes = fx.Tools.Executions.Where(e => e.Name == "write_file").ToList();
        Assert.All(writes, w => Assert.Contains("wake", w.Args));
    }

    [Fact]
    public async Task Decide_Denied_WakesIdleAgent_BackgroundRunDoesNotExecuteTool()
    {
        var (fx, agent, pendingId) = await SuspendOnceAsync();
        var registry = new AgentRuntimeRegistry((_, _) => agent);
        await registry.GetOrCreateAsync(fx.SessionId); // 生产语义：Agent 创建时必经注册表登记，TryGetAsync 才能探到
        var ctx = new FakeContext()
            .With<IApprovalService>(fx.Approvals)
            .With<IAgentRegistry>(registry);
        fx.Provider.Enqueue(Script.ToolCall("exit-1", "task_complete", """{"summary":"已取消"}"""));

        var controller = new ApprovalsController(ctx);
        var result = await controller.Decide(
            pendingId,
            new ApprovalsController.DecideBody { Approved = false, Note = "拒绝" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.IsType<ApprovalsController.ApprovalApiItem>(ok.Value);
        Assert.True(item.Resumed, "拒绝决策同样应唤醒续跑（喂回失败结果，模型改道）");
        Assert.Equal(nameof(ApprovalStatus.Denied), item.Status);

        await WaitUntilAsync(
            () => fx.EventTypes().Contains("step/end"),
            TimeSpan.FromSeconds(10),
            "决策后后台回合应收束");
        Assert.DoesNotContain(fx.Tools.Executions, e => e.Name == "write_file");
    }

    [Fact]
    public async Task Decide_NoAgentForSession_DoesNotWake_ExplainsInResumeNote()
    {
        var fx = new MafApprovalFixture();
        var pending = await fx.Approvals.CreatePendingAsync(new ApprovalCreateRequest
        {
            SessionId = fx.SessionId,
            ToolName = "write_file",
            ArgsJson = "{}",
        });

        var registry = new AgentRuntimeRegistry((_, _) => throw new InvalidOperationException("会话无 Agent 时不应创建"));
        var ctx = new FakeContext()
            .With<IApprovalService>(fx.Approvals)
            .With<IAgentRegistry>(registry);

        var controller = new ApprovalsController(ctx);
        var result = await controller.Decide(
            pending.Id,
            new ApprovalsController.DecideBody { Approved = true },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.IsType<ApprovalsController.ApprovalApiItem>(ok.Value);
        Assert.False(item.Resumed, "会话从未发起对话时不应唤醒");
        Assert.False(string.IsNullOrWhiteSpace(item.ResumeNote), "未唤醒必须给出原因说明");
        Assert.Equal(nameof(ApprovalStatus.Approved), item.Status);
    }

    [Fact]
    public async Task Decide_AgentRunning_DoesNotWake_ExplainsInResumeNote()
    {
        var (fx, _, pendingId) = await SuspendOnceAsync();
        var stub = new RunningAgentStub { SessionId = fx.SessionId };
        var registry = new AgentRuntimeRegistry((_, _) => stub);
        var ctx = new FakeContext()
            .With<IApprovalService>(fx.Approvals)
            .With<IAgentRegistry>(registry);

        var controller = new ApprovalsController(ctx);
        var result = await controller.Decide(
            pendingId,
            new ApprovalsController.DecideBody { Approved = true },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.IsType<ApprovalsController.ApprovalApiItem>(ok.Value);
        Assert.False(item.Resumed, "代理运行中不得并发注入回合（RunAsync 并发守卫）");
        Assert.False(string.IsNullOrWhiteSpace(item.ResumeNote), "未唤醒必须给出原因说明");
    }
}
