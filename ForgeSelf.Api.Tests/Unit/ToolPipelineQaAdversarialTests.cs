using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins;
using ForgeSelf.Core;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// B8（042 工具管线）QA 对抗探针——针对工程师门禁 12 条未覆盖的对抗面（严过关 QA，Edward）。
/// 与 ToolPipelineTests 互补，不重复：本文件只测工程师未锁定的路径。
/// 覆盖面：
/// 1. 守卫回调抛异常 → fail-closed（CheckGuards catch 分支无既有锁定）；
/// 2. Ask 审批「拒绝/批准/抛异常」三路（工程师只测了无审批服务的 fail-closed 一路）；
/// 3. 批处理中间 call 抛非 OCE 异常 → 内层收口保证 N call N result；
/// 4. 超时孤儿任务后续抛异常 → 观察器兜底不外溢；
/// 5. post-execute AcceptValue 改写值到达 tools/result 快照且监听器改写无效（改写链路+冻结组合）；
/// 6. tools/result 监听器抛异常 → 观测隔离且 finalize 恰好一次（红线 5）；
/// 7. 批处理取消后已完成的 call 保持真实结果。
/// </summary>
public class ToolPipelineQaAdversarialTests
{
    // ================= 1. 守卫异常 fail-closed =================

    [Fact]
    public async Task Qa_GuardThrows_FailClosed_Denies()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var guards = new ToolGuardRegistry();
        var registry = NewRegistry(bus: bus, guards: guards);
        registry.RegisterTool(CountingTool("qa1_tool", bodyLog));

        guards.AddGuard(_ => throw new InvalidOperationException("守卫内部炸了"));

        var result = await registry.ExecuteAsync(NewExec("qa1", "qa1_tool"));

        // fail-closed 判据：工具体未执行 + 最终拒绝（守卫异常绝不能变成放行）
        bodyLog.Should().BeEmpty();
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(ToolOutcome.Denied);
        result.DenyReason.Should().Contain("守卫异常");
    }

    // ================= 2. Ask 审批三路 =================

    [Fact]
    public async Task Qa_Ask_ApprovalReturnsFalse_Denied_FinalizeOnce()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var registry = new CountingRegistry(events: bus, approval: new FakeApprovalService(_ => false));
        registry.RegisterTool(CountingTool("qa2_tool", bodyLog));

        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute",
            _ => Task.FromResult<PreToolDecision?>(new PreToolDecision.Ask("敏感操作")));

        var result = await registry.ExecuteAsync(NewExec("qa2", "qa2_tool"));

        bodyLog.Should().BeEmpty();
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(ToolOutcome.Denied);
        registry.FinalizeCalls.Should().Be(1, "审批拒绝走早退路径，finalize 仍恰好一次");
    }

    [Fact]
    public async Task Qa_Ask_Approved_Executes()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var registry = NewRegistry(bus: bus, approval: new FakeApprovalService(_ => true));
        registry.RegisterTool(CountingTool("qa3_tool", bodyLog));

        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute",
            _ => Task.FromResult<PreToolDecision?>(new PreToolDecision.Ask("已获批")));

        var result = await registry.ExecuteAsync(NewExec("qa3", "qa3_tool"));

        // 批准路径：工具体真执行、结果真返回（工程师门禁只测了 fail-closed 反面）
        bodyLog.Should().ContainSingle();
        result.Success.Should().BeTrue();
        result.Outcome.Should().Be(ToolOutcome.Ok);
    }

    [Fact]
    public async Task Qa_Ask_ApprovalServiceThrows_FailClosed_BodyNotExecuted()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var registry = NewRegistry(bus: bus, approval: new FakeApprovalService(_ => throw new InvalidOperationException("审批服务崩溃")));
        registry.RegisterTool(CountingTool("qa4_tool", bodyLog));

        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute",
            _ => Task.FromResult<PreToolDecision?>(new PreToolDecision.Ask("审批服务异常场景")));

        var result = await registry.ExecuteAsync(NewExec("qa4", "qa4_tool"));

        // 审批服务抛异常 → 绝不能放行（fail-closed 红线 4 的极端面）
        bodyLog.Should().BeEmpty();
        result.Success.Should().BeFalse();
    }

    // ================= 3. 批处理中间 call 抛非 OCE 异常 =================

    [Fact]
    public async Task Qa_Batch_MiddleCallBodyThrows_AllResultsPresent()
    {
        var bus = new EventBus();
        var okLog = new List<string>();
        var registry = NewRegistry(bus: bus);

        registry.RegisterTool(CountingTool("qa5_ok", okLog));
        registry.RegisterTool(new FakeToolFunctionExtension
        {
            Id = "test.qa5_boom",
            Name = "qa5_boom",
            ParametersJsonSchema = "{}",
            ExecuteHandler = _ => throw new InvalidOperationException("工具体内部崩溃")
        });
        registry.RegisterTool(CountingTool("qa5_tail", okLog));

        var calls = new List<ToolCallRef>
        {
            new("c-a", "qa5_ok", "{}"),
            new("c-b", "qa5_boom", "{}"),
            new("c-c", "qa5_tail", "{}"),
        };

        var results = await registry.ExecuteBatchAsync(calls, "sess");

        // N call 必有 N result：中间 call 的非 OCE 异常必须被 ExecuteAsync 内层 catch 收口为
        // Error 结果，绝不能穿透 ExecuteBatchAsync 让后续 call 丢失（A4 model-ordered commit）
        results.Should().HaveCount(3);
        results[0].CallId.Should().Be("c-a");
        results[0].Success.Should().BeTrue();
        results[1].CallId.Should().Be("c-b");
        results[1].Success.Should().BeFalse();
        results[2].CallId.Should().Be("c-c");
        results[2].Success.Should().BeTrue();
        okLog.Should().HaveCount(2, "首尾两个工具体都应真实执行");
    }

    // ================= 4. 超时孤儿任务后续抛异常 =================

    [Fact]
    public async Task Qa_TimeoutOrphan_BodyThrowsLater_ObserverAbsorbs()
    {
        var bus = new EventBus();
        var registry = NewRegistry(bus: bus);
        registry.RegisterTool(new FakeToolFunctionExtension
        {
            Id = "test.qa6_slowboom",
            Name = "qa6_slowboom",
            ParametersJsonSchema = "{}",
            ExecuteHandler = async _ =>
            {
                await Task.Delay(5_000);
                throw new InvalidOperationException("孤儿任务迟到炸裂");
            }
        });

        // execute 中间件替换 Signal 为 200ms 超时（同门禁 5 接线）
        bus.OnWaterfall<ToolExecution, ToolExecutionResult>("tools/execute", async (exec, next) =>
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(exec.Signal);
            timeoutCts.CancelAfter(200);
            exec.Signal = timeoutCts.Token;
            return await next();
        });

        var result = await registry.ExecuteAsync(NewExec("qa6", "qa6_slowboom"));

        // 信号竞速获胜 → 返回取消结果；孤儿任务 5s 后在后台抛异常，
        // 只能被 ObserveOrphanAsync 吞掉——本测试若因未观察异常崩溃即红。
        result.Success.Should().BeFalse();
        await Task.Delay(400); // 证明管线早已返回，不等孤儿
        result.Success.Should().BeFalse();
    }

    // ================= 5. post-execute 改写 → 快照冻结组合 =================

    [Fact]
    public async Task Qa_PostAcceptValue_RewriteReachesAnnouncement_AndFrozen()
    {
        var bus = new EventBus();
        var registry = NewRegistry(bus: bus);
        registry.RegisterTool(CountingTool("qa7_tool", new List<string>()));

        // 先捕获快照字段值（引用会被本监听器自己篡改，事后读引用是测试自误）
        string? announcedJson = null;
        var announcedSuccess = false;
        bus.On<ToolResultAnnouncement>("tools/result", a =>
        {
            announcedJson = a.ResultJson;
            announcedSuccess = a.Success;
            // 冻结对抗：监听器改快照字段，绝不能影响主流程返回值
            a.ResultJson = "{\"hacked\":true}";
            a.Success = false;
            return Task.CompletedTask;
        });

        // post-execute 改写成功值
        bus.OnWaterfall<ToolExecution, PostToolDecision?>("tools/post-execute",
            (_, next) => Task.FromResult<PostToolDecision?>(new PostToolDecision.AcceptValue("{\"rewritten\":42}")));

        var result = await registry.ExecuteAsync(NewExec("qa7", "qa7_tool"));

        // 改写值到达广播快照（监听器内部捕获的原始值）
        announcedJson.Should().Be("{\"rewritten\":42}");
        announcedSuccess.Should().BeTrue();
        // 监听器对快照的改写不影响返回值（冻结快照 + 改写链路组合，工程师门禁 9 未覆盖此组合）
        result.Result.Should().Be("{\"rewritten\":42}");
        result.Success.Should().BeTrue();
    }

    // ================= 6. tools/result 监听器抛异常 → 观测隔离 =================

    [Fact]
    public async Task Qa_ToolsResultListenerThrows_Isolated_FinalizeOnce()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var registry = new CountingRegistry(events: bus);
        registry.RegisterTool(CountingTool("qa8_tool", bodyLog));

        bus.On<ToolResultAnnouncement>("tools/result",
            _ => throw new InvalidOperationException("观测者炸了"));

        var result = await registry.ExecuteAsync(NewExec("qa8", "qa8_tool"));

        // 红线 5：观测失败被隔离，不影响主流程返回、finalize 仍恰好一次
        bodyLog.Should().ContainSingle();
        result.Success.Should().BeTrue();
        result.Result.Should().Be("{\"ok\":true}");
        registry.FinalizeCalls.Should().Be(1);
    }

    // ================= 7. 批处理取消：已完成的保持真实结果 =================

    [Fact]
    public async Task Qa_Batch_CancelAfterFirst_CompletedStaysReal_SecondSkipped()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var registry = NewRegistry(bus: bus);
        // 注意：RegisterTool 按 Name TryAdd 先到先得——只注册带取消副作用的版本，
        // 不能先用 CountingTool 占名（否则取消版被静默丢弃，ct 永不触发——QA 首轮自误已修正）。
        using var cts = new CancellationTokenSource();
        // 第一个工具体执行完顺手取消整批（模拟批间用户停止）
        var first = new FakeToolFunctionExtension
        {
            Id = "test.qa9_first",
            Name = "qa9_first",
            ParametersJsonSchema = "{}",
            ExecuteHandler = _ =>
            {
                bodyLog.Add("qa9_first");
                cts.Cancel();
                return Task.FromResult("{\"done\":1}");
            }
        };
        registry.RegisterTool(first);

        var calls = new List<ToolCallRef>
        {
            new("c-1", "qa9_first", "{}"),
            new("c-2", "qa9_second", "{}"),
        };

        var results = await registry.ExecuteBatchAsync(calls, "sess", cts.Token);

        results.Should().HaveCount(2);
        // 已完成的 call 保持真实成功结果，不被取消污染（宁真实不丢失）
        results[0].CallId.Should().Be("c-1");
        results[0].Success.Should().BeTrue();
        results[0].Outcome.Should().Be(ToolOutcome.Ok);
        // 第二个 call 合成 Skipped，绝不静默丢弃
        results[1].CallId.Should().Be("c-2");
        results[1].Outcome.Should().Be(ToolOutcome.Skipped);
        bodyLog.Should().ContainSingle("第二个工具体绝不能再执行");
    }

    // ================= 辅助 =================

    private static ToolRegistry NewRegistry(
        EventBus? bus = null,
        IToolGuardRegistry? guards = null,
        IApprovalService? approval = null)
        => new(events: bus, guards: guards, approval: approval);

    private static ToolExecution NewExec(string callId, string toolName)
        => new() { CallId = callId, ToolName = toolName, ArgsJson = "{}", SessionId = "sess" };

    private static IToolFunctionExtension CountingTool(string name, List<string> log)
        => new FakeToolFunctionExtension
        {
            Id = $"test.{name}",
            Name = name,
            ParametersJsonSchema = "{}",
            ExecuteHandler = _ =>
            {
                log.Add(name);
                return Task.FromResult("{\"ok\":true}");
            }
        };

    /// <summary>统计 finalize 调用次数的观测子类（finalize 为 protected virtual 可观测缝）。</summary>
    private sealed class CountingRegistry : ToolRegistry
    {
        public CountingRegistry(IEventBus? events = null, IApprovalService? approval = null)
            : base(events: events, approval: approval) { }

        public int FinalizeCalls { get; private set; }

        protected override async Task<ToolExecutionResult> FinalizeAsync(
            ToolExecution execution,
            ToolExecutionResult result,
            System.Diagnostics.Stopwatch stopwatch,
            bool success,
            string resultJson,
            string? errorMessage,
            ToolOutcome outcome,
            string? denyReason,
            Dictionary<string, object>? usageMetadata)
        {
            FinalizeCalls++;
            await Task.Yield();
            return await base.FinalizeAsync(execution, result, stopwatch, success, resultJson,
                errorMessage, outcome, denyReason, usageMetadata);
        }
    }

    /// <summary>可编程审批服务替身。</summary>
    private sealed class FakeApprovalService(Func<string?, bool> onAsk) : IApprovalService
    {
        public Task<bool> AskAsync(string sessionId, string toolName, string argsJson, string? reason, CancellationToken ct)
            => onAsk(reason) ? Task.FromResult(true) : Task.FromResult(false);
    }
}
