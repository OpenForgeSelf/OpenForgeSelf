using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins;
using ForgeSelf.Core;
using Moq;
using Xunit;
using AIChatMessage = ForgeSelf.Api.Models.AIChatMessage;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// B8（042 工具管线）六闸门门禁测试（架构师 §2.7 门禁清单 12 条）。
/// 管线：<c>tools/pre-execute → 单调守卫 → tools/execute → tools/post-execute → finalize → tools/result</c>。
/// 判据：A1 三态 fail-closed / A2 单调守卫 / A3 waterfall 修正（门禁 5、6 先红后绿）/ A4 model-ordered commit / A5 StreamChunk 扩展。
/// </summary>
public class ToolPipelineTests
{
    // ---- 门禁 1：pre-execute Deny → 工具体不执行，结果 Denied ----
    [Fact]
    public async Task PreExecute_Deny_SkipsBody()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var registry = NewRegistry(bus: bus);
        registry.RegisterTool(CountingTool("g1_tool", bodyLog));

        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute",
            _ => Task.FromResult<PreToolDecision?>(new PreToolDecision.Deny("g1 拒绝")));

        var result = await registry.ExecuteAsync(NewExec("c1", "g1_tool"));

        bodyLog.Should().BeEmpty();                          // 工具体调用计数 == 0
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(ToolOutcome.Denied);
        result.DenyReason.Should().Be("g1 拒绝");
        result.ErrorMessage.Should().Be("g1 拒绝");
        result.Result.Should().Contain("PERMISSION_DENIED");
    }

    // ---- 门禁 2：pre-execute Ask + 无审批服务 → fail-closed 拒绝（A1） ----
    [Fact]
    public async Task PreExecute_Ask_NoApprovalService_FailClosed()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var registry = NewRegistry(bus: bus, approval: null);   // 未注册审批服务
        registry.RegisterTool(CountingTool("g2_tool", bodyLog));

        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute",
            _ => Task.FromResult<PreToolDecision?>(new PreToolDecision.Ask("需要人工确认")));

        var result = await registry.ExecuteAsync(NewExec("c2", "g2_tool"));

        bodyLog.Should().BeEmpty();
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(ToolOutcome.Denied);
        result.ErrorMessage.Should().Contain("fail-closed");
    }

    // ---- 门禁 3：单调守卫不可撤销——pre-execute/execute 闸门即便「放行/改写」，守卫拒绝仍是最终结论（A2） ----
    [Fact]
    public async Task Guard_AlwaysWins()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var guards = new ToolGuardRegistry();
        var registry = NewRegistry(bus: bus, guards: guards);
        registry.RegisterTool(CountingTool("g3_tool", bodyLog));

        // pre-execute 显式 Allow + execute 中间件试图「放行并改写成功」——都轮不到生效
        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute",
            _ => Task.FromResult<PreToolDecision?>(new PreToolDecision.Allow()));
        bus.OnWaterfall<ToolExecution, ToolExecutionResult>("tools/execute",
            (exec, next) => next());

        guards.AddGuard(_ => "守卫拒绝");

        var result = await registry.ExecuteAsync(NewExec("c3", "g3_tool"));

        bodyLog.Should().BeEmpty();
        result.Success.Should().BeFalse();
        result.Outcome.Should().Be(ToolOutcome.Denied);
        result.DenyReason.Should().Be("守卫拒绝");
    }

    // ---- 门禁 4：守卫返回 null → 维持现状（不改变 pre-execute 的 Allow，无 allow 结果语义） ----
    [Fact]
    public async Task Guard_NoAllowResult()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var guards = new ToolGuardRegistry();
        var registry = NewRegistry(bus: bus, guards: guards);
        registry.RegisterTool(CountingTool("g4_tool", bodyLog));

        guards.AddGuard(_ => null);   // 只有一个守卫且返回 null：维持现状

        var result = await registry.ExecuteAsync(NewExec("c4", "g4_tool"));

        bodyLog.Should().ContainSingle();
        result.Success.Should().BeTrue();
        result.Outcome.Should().Be(ToolOutcome.Ok);
    }

    // ---- 门禁 5（先红后绿）：execute waterfall 中间件替换 Signal → 超时生效（A3 接线判据） ----
    [Fact]
    public async Task Execute_Waterfall_CanAddTimeout()
    {
        var bus = new EventBus();
        var registry = NewRegistry(bus: bus);
        registry.RegisterTool(CountingTool("g5_tool", new List<string>(), delayMs: 5_000));

        // execute 中间件把 Signal 换成 200ms 超时的链接令牌 → 工具体必须被及时打断
        bus.OnWaterfall<ToolExecution, ToolExecutionResult>("tools/execute", async (exec, next) =>
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(exec.Signal);
            timeoutCts.CancelAfter(200);
            exec.Signal = timeoutCts.Token;
            return await next();
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await registry.ExecuteAsync(NewExec("c5", "g5_tool"));
        stopwatch.Stop();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("工具执行被取消");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(4_000, "超时必须及时生效，而不是等工具体跑满 5 秒");
    }

    // ---- 门禁 6（先红后绿）：post-execute waterfall 返回 AcceptContent → 结果被改写（A3 接线判据） ----
    [Fact]
    public async Task PostExecute_CanRewriteResult()
    {
        var bus = new EventBus();
        var bodyLog = new List<string>();
        var registry = NewRegistry(bus: bus);
        registry.RegisterTool(CountingTool("g6_tool", bodyLog));

        bus.OnWaterfall<ToolExecution, PostToolDecision?>("tools/post-execute",
            (exec, next) => Task.FromResult<PostToolDecision?>(new PostToolDecision.AcceptContent("{\"rewritten\":true}")));

        var result = await registry.ExecuteAsync(NewExec("c6", "g6_tool"));

        bodyLog.Should().ContainSingle();
        result.Success.Should().BeTrue();
        result.Result.Should().Be("{\"rewritten\":true}");
    }

    // ---- 门禁 7：批量执行按模型返回顺序串行 commit（A4 model-ordered commit） ----
    [Fact]
    public async Task Batch_ModelOrderedCommit()
    {
        var bus = new EventBus();
        var execOrder = new List<string>();
        var registry = NewRegistry(bus: bus);
        registry.RegisterTool(CountingTool("slow_tool", execOrder, delayMs: 100));
        registry.RegisterTool(CountingTool("fast_tool_a", execOrder));
        registry.RegisterTool(CountingTool("fast_tool_b", execOrder));

        var calls = new List<ToolCallRef>
        {
            new("call-1", "slow_tool", "{}"),
            new("call-2", "fast_tool_a", "{}"),
            new("call-3", "fast_tool_b", "{}"),
        };

        var results = await registry.ExecuteBatchAsync(calls, "sess");

        results.Should().HaveCount(3);
        results.Select(r => r.CallId).Should().Equal("call-1", "call-2", "call-3");   // 结果顺序 == 模型返回顺序
        results.Select(r => r.Outcome).Should().Equal(ToolOutcome.Ok, ToolOutcome.Ok, ToolOutcome.Ok);
        execOrder.Should().Equal("slow_tool", "fast_tool_a", "fast_tool_b");          // 串行执行，不并发
    }

    // ---- 门禁 8：批量执行中取消 → 剩余 call 合成 Skipped，N call 必有 N result（A4） ----
    [Fact]
    public async Task Batch_Cancel_SkipsRemaining()
    {
        var bus = new EventBus();
        var execOrder = new List<string>();
        var cts = new CancellationTokenSource();
        var registry = NewRegistry(bus: bus);
        registry.RegisterTool(CountingTool("first_tool", execOrder));
        registry.RegisterTool(CountingTool("second_tool", execOrder));
        registry.RegisterTool(CountingTool("third_tool", execOrder));

        // 第一个工具执行完即在 execute 中间件里取消批次令牌 → 后续 call 全部 Skipped（确定性时序）
        bus.OnWaterfall<ToolExecution, ToolExecutionResult>("tools/execute", async (exec, next) =>
        {
            var result = await next();
            if (exec.ToolName == "first_tool")
            {
                cts.Cancel();
            }
            return result;
        });

        var calls = new List<ToolCallRef>
        {
            new("b-1", "first_tool", "{}"),
            new("b-2", "second_tool", "{}"),
            new("b-3", "third_tool", "{}"),
        };

        var results = await registry.ExecuteBatchAsync(calls, "sess", cts.Token);

        results.Should().HaveCount(3);                                       // result 数 == call 数
        results[0].Outcome.Should().Be(ToolOutcome.Ok);                      // 首个已执行
        results[1].Outcome.Should().Be(ToolOutcome.Skipped);                 // 剩余全 Skipped
        results[2].Outcome.Should().Be(ToolOutcome.Skipped);
        execOrder.Should().ContainSingle().Which.Should().Be("first_tool");
    }

    // ---- 门禁 9：tools/result 监听器改字段 → 主流程结果不变（冻结快照） ----
    [Fact]
    public async Task Result_Frozen_ObserverCannotMutate()
    {
        var bus = new EventBus();
        var registry = NewRegistry(bus: bus);
        registry.RegisterTool(CountingTool("g9_tool", new List<string>()));

        bus.On<ToolResultAnnouncement>("tools/result", a =>
        {
            a.Success = false;
            a.ResultJson = "篡改";
            a.Outcome = ToolOutcome.Error;
            return Task.CompletedTask;
        });

        var result = await registry.ExecuteAsync(NewExec("c9", "g9_tool"));

        result.Success.Should().BeTrue();
        result.Result.Should().Be("{\"ok\":true}");
        result.Outcome.Should().Be(ToolOutcome.Ok);
    }

    // ---- 门禁 10：finalize 每次执行恰好一次（含 deny 路径） ----
    [Fact]
    public async Task Finalize_ExactlyOnce()
    {
        var bus = new EventBus();
        var registry = new CountingFinalizeRegistry(events: bus);
        registry.RegisterTool(CountingTool("g10_tool", new List<string>()));
        registry.RegisterTool(CountingTool("g10_guarded", new List<string>()));
        registry.Guards.AddGuard(_ => "守卫拒绝");

        // 成功路径：finalize 恰好 1 次
        await registry.ExecuteAsync(NewExec("c10a", "g10_tool"));
        registry.FinalizeCalls.Should().Be(1);

        // deny 路径（守卫拒绝）：finalize 仍恰好 +1（含 deny 路径，红线 5）
        await registry.ExecuteAsync(NewExec("c10b", "g10_guarded"));
        registry.FinalizeCalls.Should().Be(2);
    }

    // ---- 门禁 11：守卫 Dispose（插件卸载 / ctx.Effect 回滚）后不再生效 ----
    [Fact]
    public async Task Dispose_GuardRemoved()
    {
        var bodyLog = new List<string>();
        var guards = new ToolGuardRegistry();
        var registry = NewRegistry(guards: guards);
        registry.RegisterTool(CountingTool("g11_tool", bodyLog));

        var handle = guards.AddGuard(_ => "插件守卫");
        handle.Dispose();   // 模拟插件卸载

        var result = await registry.ExecuteAsync(NewExec("c11", "g11_tool"));

        bodyLog.Should().ContainSingle();
        result.Success.Should().BeTrue();
    }

    // ---- 门禁 12：StreamChunk 携带 Usage/FinishReason/ToolCall（A5）+ legacy 适配不丢 tool 角色 CallId（B8-8） ----
    [Fact]
    public async Task StreamChunk_CarriesUsage()
    {
        // A5 第一半：ILlmRuntime 面的 StreamChunk 透传 Usage/FinishReason/ToolCall
        var chunks = new List<StreamChunk>
        {
            new() { Content = "", ToolCall = new ToolCallDelta("call-1", "demo_tool", "{\"a\":1}") },
            new() { Content = "", ToolCall = new ToolCallDelta(null, null, "{\"b\":2}") },
            new() { Content = "完成", Usage = new UnifiedUsage { PromptTokens = 11, CompletionTokens = 7 }, FinishReason = "stop" },
            new() { Content = "", IsFinal = true }
        };
        var runtime = new FakeLlmRuntime(chunks);

        var received = new List<StreamChunk>();
        await foreach (var c in runtime.StreamAsync(new List<Message> { new() { Role = "user", Content = "hi" } }))
        {
            received.Add(c);
        }

        received[0].ToolCall.Should().NotBeNull();
        received[0].ToolCall!.Id.Should().Be("call-1");
        received[1].ToolCall!.Arguments.Should().Be("{\"b\":2}");
        received[2].Usage.Should().NotBeNull();
        received[2].Usage!.PromptTokens.Should().Be(11);
        received[2].FinishReason.Should().Be("stop");

        // A5 第二半（B8-8 缺陷修复）：Message → legacy AIChatMessage 不再丢 CallId/ToolCalls——
        // tool 角色消息经标记文本进入模型可见内容
        var legacy = new List<AIChatMessage>();
        var aiServiceMock = new Mock<IAIService>();
        aiServiceMock
            .Setup(s => s.ChatStreamAsync(It.IsAny<List<AIChatMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<List<AIChatMessage>, CancellationToken>((ms, _) => legacy.AddRange(ms))
            .Returns(TestAsyncEnumerable(new List<string> { "ok" }));
        var adapter = new AIServiceLlmRuntime(aiServiceMock.Object);

        var messages = new List<Message>
        {
            new() { Role = "assistant", Content = "", ToolCalls = new List<ToolCallRef> { new("call-9", "demo_tool", "{}") } },
            new() { Role = "tool", Content = "执行结果", CallId = "call-9" }
        };
        await foreach (var _ in adapter.StreamAsync(messages)) { }

        legacy.Should().HaveCount(2);
        legacy[0].Content.Should().Contain("call-9").And.Contain("demo_tool");
        legacy[1].Content.Should().Contain("call-9").And.Contain("执行结果");
    }

    // ---- 测试基建 ----

    private static ToolRegistry NewRegistry(
        EventBus? bus = null,
        IToolGuardRegistry? guards = null,
        IApprovalService? approval = null)
        => new(events: bus, guards: guards, approval: approval);

    private static ToolExecution NewExec(string callId, string toolName)
        => new() { CallId = callId, ToolName = toolName, ArgsJson = "{}", SessionId = "sess" };

    /// <summary>假工具：可选延迟后记录工具体被调，可选在返回前执行副作用。</summary>
    private static IToolFunctionExtension CountingTool(
        string name, List<string> log, int delayMs = 0, Func<Task>? beforeReturn = null)
        => new FakeToolFunctionExtension
        {
            Id = $"test.{name}",
            Name = name,
            ParametersJsonSchema = "{}",
            ExecuteHandler = async _ =>
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs);
                }
                log.Add(name);
                if (beforeReturn != null)
                {
                    await beforeReturn();
                }
                return "{\"ok\":true}";
            }
        };

    /// <summary>门禁 10 用：统计 finalize 调用次数的 ToolRegistry 子类（finalize 为 protected virtual 可观测缝）。</summary>
    private sealed class CountingFinalizeRegistry : ToolRegistry
    {
        public CountingFinalizeRegistry(IEventBus? events = null) : base(events: events) { }

        public ToolGuardRegistry Guards { get; } = new();

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

    /// <summary>ILlmRuntime 假实现：逐个吐出预置分片。</summary>
    private sealed class FakeLlmRuntime(IEnumerable<StreamChunk> chunks) : ILlmRuntime
    {
        public async IAsyncEnumerable<StreamChunk> StreamAsync(
            IReadOnlyList<Message> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var chunk in chunks)
            {
                yield return chunk;
                await Task.Yield();
            }
        }
    }

    private static async IAsyncEnumerable<T> TestAsyncEnumerable<T>(
        IEnumerable<T> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }
}
