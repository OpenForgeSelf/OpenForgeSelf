using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// B5（041）门禁测试：ReactLoopAgent turn/step 状态机（架构师 §2-B5 清单 8 条）。
/// </summary>
/// <remarks>
/// 判据要点：一切持久事实必须能从会话日志 <c>Replay</c> 重建（帧只是 live 视图）；
/// 因此断言一律打在 <b>日志事件序</b> 上，而不是帧上。
/// </remarks>
[Collection("XCode")]
public class ReactLoopAgentTests
{
    // ---- 1. 最短路径：无工具，1 turn 1 step ----
    [Fact]
    public async Task SingleTurn_NoTools_Completes()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("你好，有什么可以帮你？", finish: "stop"));

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "第一轮正常消息");

        var frames = await fixture.RunAsync(agent);

        // 日志事件序逐条比对（旧 for(i<10) 不落 turn/step，本测试在旧实现下必红）。
        // B6 切片 2b：消费确认经 IInboxConfirmation 能力接口——内存测试替身按 MessageId 移除（无日志事件），
        // 故事件序与 B5 相同；PersistentInbox 路径的确认会落 agent/inbox/spliced（见 PersistentInboxTests）。
        Assert.Equal(
            new[]
            {
                "turn/start", "step/start", "user/message", "request/header",
                "assistant/message", "step/end", "turn/end"
            },
            fixture.EventTypes());

        Assert.Equal(new[] { "user", "assistant" }, fixture.DerivedRoles());
        Assert.Contains(frames, f => f is TurnCompleted);
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    // ---- 2. 含工具调用：跑 2 个 step，tool/call + tool/result 成对且顺序与模型返回一致 ----
    [Fact]
    public async Task TwoSteps_WithTool()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"));
        fixture.Provider.Enqueue(Script.Text("现在时间是 12:00", finish: "stop"));

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "现在几点？");
        await fixture.RunAsync(agent);

        var types = fixture.EventTypes();
        Assert.Equal(2, types.Count(t => t == "step/start"));
        Assert.Equal(2, types.Count(t => t == "step/end"));
        Assert.Single(types.Where(t => t == "tool/call"));
        Assert.Single(types.Where(t => t == "tool/result"));

        // tool/call 必须先于 tool/result，且二者都在本 step 的 assistant/message 之后
        var callIdx = types.IndexOf("tool/call");
        var resultIdx = types.IndexOf("tool/result");
        var firstAssistantIdx = types.IndexOf("assistant/message");
        Assert.True(firstAssistantIdx < callIdx, "助手产出（含工具调用）必须先落日志，再执行工具");
        Assert.True(callIdx < resultIdx, "tool/call 必须早于配对的 tool/result");

        // 工具确实被执行，且结果进了模型上下文（下一轮请求能看见 tool 角色消息）
        Assert.Equal(new[] { "get_current_time" }, fixture.Tools.Executions.Select(e => e.Name).ToArray());
        Assert.Contains(fixture.Provider.Requests[1].Messages, m => m.Role == "tool");
    }

    // ---- 3. max-tokens 粘滞：任一步 finish=length → 整个 turn 以 MaxTokens 收束 ----
    [Fact]
    public async Task MaxTokens_Sticky()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"));
        fixture.Provider.Enqueue(Script.Text("时间还没说完就被截断", finish: "length"));

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "继续说");
        var frames = await fixture.RunAsync(agent);

        var turnEnd = fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>().Single();
        Assert.Equal(TurnEndReason.MaxTokens, turnEnd.Reason);

        var lastStepEnd = fixture.Store.Replay(fixture.SessionId).OfType<StepEndEvent>().Last();
        Assert.Equal(StepEndReason.MaxTokens, lastStepEnd.Reason);
        Assert.Contains(frames, f => f is TurnCompleted { Reason: TurnEndReason.MaxTokens });
    }

    // ---- 4. 请求中取消：无半截 assistant/message，turn/end = Aborted ----
    [Fact]
    public async Task Cancel_DuringRequest_NoPartialCommit()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("这一段会被取消", finish: null, throwAfter: new OperationCanceledException()));

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "讲个长故事");
        var frames = await fixture.RunAsync(agent);

        // 用户消息要么完整落一条，要么不落；绝不落半截
        Assert.Single(fixture.Store.Replay(fixture.SessionId).OfType<UserMessageEvent>());
        Assert.Equal("讲个长故事", fixture.Store.Replay(fixture.SessionId).OfType<UserMessageEvent>().Single().Content);

        // 半截助手产出只落 attempt，不落 assistant/message（不进模型历史）
        Assert.Empty(fixture.Store.Replay(fixture.SessionId).OfType<AssistantMessageEvent>());
        Assert.Contains(fixture.Store.Replay(fixture.SessionId).OfType<AssistantAttemptEvent>(),
            a => a.ErrorKind == "aborted");

        var turnEnd = fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>().Single();
        Assert.Equal(TurnEndReason.Aborted, turnEnd.Reason);
        Assert.Contains(frames, f => f is TurnCompleted { Reason: TurnEndReason.Aborted });
    }

    // ---- 5. 首次请求失败返重试：user/message 只 1 条、step/start 不重复 ----
    [Fact]
    public async Task RequestError_Retry_NoReassembly()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Fail(new InvalidOperationException("上游 500")));
        fixture.Provider.Enqueue(Script.Text("重试后成功", finish: "stop"));

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "会失败一次的请求");
        await fixture.RunAsync(agent);

        var events = fixture.Store.Replay(fixture.SessionId);
        Assert.Single(events.OfType<UserMessageEvent>());
        Assert.Single(events.OfType<StepStartEvent>());
        Assert.Single(events.OfType<AssistantAttemptEvent>());   // 失败留痕一次
        Assert.Single(events.OfType<AssistantMessageEvent>());   // 成功只落一条
        Assert.Equal("重试后成功", events.OfType<AssistantMessageEvent>().Single().Content);
        Assert.Equal(2, fixture.Provider.Requests.Count);
    }

    // ---- 6. MaxSteps 兜底：连续 tool_call 超限 → 强制收束且 turn/end 存在 ----
    [Fact]
    public async Task MaxSteps_Enforced()
    {
        var fixture = new Fixture(maxStepsPerTurn: 2);
        fixture.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"), repeatLast: true);

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "无限调用工具");
        await fixture.RunAsync(agent);

        var types = fixture.EventTypes();
        Assert.Equal(2, types.Count(t => t == "step/start"));
        Assert.Single(types.Where(t => t == "turn/end"));
        Assert.Equal(types[^1], "turn/end");
    }

    // ---- 7. 注册表注销：DisposeAsync 后 TryGetAsync 返回 null ----
    [Fact]
    public async Task Dispose_Unregisters()
    {
        var registry = new AgentRuntimeRegistry((sessionId, options) =>
        {
            var fixture = new Fixture();
            fixture.Provider.Enqueue(Script.Text("你好", finish: "stop"), repeatLast: true);
            return fixture.CreateAgent(sessionId);
        });

        var agent = await registry.GetOrCreateAsync("s-dispose", new AgentOptions());
        Assert.NotNull(await registry.TryGetAsync("s-dispose"));
        Assert.Equal("s-dispose", agent.SessionId);

        Assert.True(await registry.DisposeAsync("s-dispose"));
        Assert.Null(await registry.TryGetAsync("s-dispose"));
        Assert.False(await registry.DisposeAsync("s-dispose"));
    }

    // ---- 8. 旧契约已删（grep 断言：生产代码 0 命中） ----
    [Fact]
    public void IAgentLoop_Removed()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        var productionDirs = new[]
        {
            Path.Combine(repoRoot, "ForgeSelf.Abstractions"),
            Path.Combine(repoRoot, "ForgeSelf.Core"),
            Path.Combine(repoRoot, "ForgeSelf.Api"),
            Path.Combine(repoRoot, "Plugins"),
        };

        // 只禁「真正的旧契约类型引用」：注释里提到旧名（说明为何删除）不算命中；
        // AgentRunRequest 不在此列（AgentHub 有同名但语义不同的自有 DTO，禁了会误伤）。
        var banned = new[] { "IAgentLoop", "InMemoryAgentLoop", "TurnEvent" };
        var hits = new List<string>();

        foreach (var dir in productionDirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue; // 注释行（含 /// 与 //）不算引用
                    }

                    if (banned.Any(b => lines[i].Contains(b, StringComparison.Ordinal)))
                    {
                        hits.Add($"{Path.GetRelativePath(repoRoot, file)}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        Assert.True(hits.Count == 0,
            "旧契约 IAgentLoop/TurnEvent/InMemoryAgentLoop/AgentRunRequest 仍在生产代码中出现: "
            + string.Join(" | ", hits));
    }

    // ---- 附加：turn-stopping 裁决语义（无监听即收束；监听器返回 false → 再跑一步） ----
    [Fact]
    public async Task TurnStopping_NoListener_Converges_ListenerFalse_TakesAnotherStep()
    {
        // (a) 无监听器 → 收束（1 step）
        var noListener = new Fixture();
        noListener.Provider.Enqueue(Script.Text("完成", finish: "stop"), repeatLast: true);
        var agentA = noListener.CreateAgent();
        agentA.Inbox.Followup(noListener.SessionId, "一轮就够");
        await noListener.RunAsync(agentA);
        Assert.Equal(1, noListener.EventTypes().Count(t => t == "step/start"));

        // (b) 监听器第一次返回 false（不收束）→ 再跑一步；其后返回 true（收束）→ 共 2 step
        var withListener = new Fixture();
        withListener.Provider.Enqueue(Script.Text("再来一步", finish: "stop"), repeatLast: true);
        var calls = 0;
        using var _ = withListener.Events.OnSerial<TurnStoppingContext, bool?>(
            "agent/turn-stopping", _ => Task.FromResult<bool?>(calls++ == 0 ? false : true));

        var agentB = withListener.CreateAgent();
        agentB.Inbox.Followup(withListener.SessionId, "需要多跑一步");
        await withListener.RunAsync(agentB);
        Assert.Equal(2, withListener.EventTypes().Count(t => t == "step/start"));
    }
}

/// <summary>测试脚手架：内存会话日志 + 脚本化提供方 + 假工具注册表。</summary>
internal sealed class Fixture
{
    public string SessionId { get; } = "s-" + Guid.NewGuid().ToString("N")[..8];

    public InMemorySessionStore Store { get; } = new();

    public ScriptedProvider Provider { get; } = new();

    public FakeToolRegistry Tools { get; } = new();

    public EventBus Events { get; } = new();

    /// <summary>宿主收件箱替身（B6：ReactLoopAgent 经 AgentTurnRuntime.Inbox 接宿主收件箱）。</summary>
    public InMemoryInbox Inbox { get; } = new();

    private readonly int _maxStepsPerTurn;

    public Fixture(int maxStepsPerTurn = 10)
    {
        _maxStepsPerTurn = maxStepsPerTurn;
    }

    public IAgent CreateAgent(string? sessionId = null)
        => new ReactLoopAgent(sessionId ?? SessionId, new AgentOptions { MaxStepsPerTurn = _maxStepsPerTurn },
            new AgentTurnRuntime
            {
                Store = Store,
                Provider = Provider,
                ModelId = "fake-model",
                ToolExecutor = Tools,
                SystemPrompt = null,
                ToolDefinitions = null,
                Events = Events,
                Inbox = Inbox
            });

    public async Task<List<TurnFrame>> RunAsync(IAgent agent, CancellationToken ct = default)
    {
        var frames = new List<TurnFrame>();
        await foreach (var frame in agent.RunAsync(ct))
        {
            frames.Add(frame);
        }
        return frames;
    }

    public List<string> EventTypes()
        => Store.Replay(SessionId).Select(e => e.Type).ToList();

    public List<string> DerivedRoles()
        => Store.DeriveMessages(SessionId).Select(m => m.Role).ToList();
}

/// <summary>一段可脚本化的模型响应。</summary>
internal sealed record Script(List<UnifiedStreamChunk> Chunks, Exception? ThrowAfter = null)
{
    public static Script Text(string content, string? finish = "stop", Exception? throwAfter = null)
    {
        var chunks = new List<UnifiedStreamChunk>();
        foreach (var piece in Split(content))
        {
            chunks.Add(new UnifiedStreamChunk { DeltaContent = piece });
        }
        if (finish != null)
        {
            chunks.Add(new UnifiedStreamChunk { FinishReason = finish });
        }
        return new Script(chunks, throwAfter);
    }

    public static Script ToolCall(string callId, string toolName, string argsJson, string? finish = "tool_calls")
    {
        var chunks = new List<UnifiedStreamChunk>
        {
            new() { DeltaToolCall = new UnifiedToolCall { Id = callId, Name = toolName, Arguments = argsJson } }
        };
        if (finish != null)
        {
            chunks.Add(new UnifiedStreamChunk { FinishReason = finish });
        }
        return new Script(chunks);
    }

    public static Script Fail(Exception ex) => new(new List<UnifiedStreamChunk>(), ex);

    /// <summary>把文本切成若干增量块（模拟流式逐字返回）。</summary>
    private static IEnumerable<string> Split(string content)
    {
        if (content.Length == 0)
        {
            yield break;
        }

        for (var i = 0; i < content.Length; i += 4)
        {
            yield return content.Substring(i, Math.Min(4, content.Length - i));
        }
    }
}

/// <summary>脚本化 AI 提供方：按脚本顺序响应每次请求，队列空时重复最后一段脚本。</summary>
internal sealed class ScriptedProvider : IAIProvider
{
    private readonly List<Script> _scripts = new();
    private int _index;

    /// <summary>每次请求收到的统一请求（用于断言「模型实际看到了什么」）。</summary>
    public List<UnifiedChatRequest> Requests { get; } = new();

    public string ProviderName => "fake";
    public AIProviderType ProviderType => AIProviderType.Custom;
    public List<string> SupportedModels => new() { "fake-model" };
    public bool IsDefault => true;

    public void Enqueue(Script script, bool repeatLast = false)
    {
        _scripts.Add(script);
        RepeatLast = repeatLast;
    }

    /// <summary>队列耗尽后是否重复最后一段脚本（用于「无限工具调用」类测试）。</summary>
    public bool RepeatLast { get; private set; }

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("本测试替身只支持流式");

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(
        UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        var script = NextScript();
        foreach (var chunk in script.Chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return chunk;
        }

        if (script.ThrowAfter != null)
        {
            throw script.ThrowAfter;
        }
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ModelInfo>());

    private Script NextScript()
    {
        if (_scripts.Count == 0)
        {
            return new Script(new List<UnifiedStreamChunk>());
        }

        var script = _scripts[_index];
        if (_index < _scripts.Count - 1 || RepeatLast)
        {
            _index = Math.Min(_index + 1, _scripts.Count - 1);
        }
        return script;
    }
}

/// <summary>假工具注册表：记录调用并返回固定成功结果。</summary>
internal sealed class FakeToolRegistry : IToolRegistry
{
    public List<(string Name, string Args)> Executions { get; } = new();

    public void RegisterTool(IToolFunctionExtension tool) { }

    public void UnregisterTool(string toolId) { }

    public IToolFunctionExtension? GetTool(string toolName) => null;

    public IEnumerable<IToolFunctionExtension> GetAllTools() => Array.Empty<IToolFunctionExtension>();

    public List<AIToolDefinition> GetToolDefinitions() => new();

    public Task<string> ExecuteToolAsync(string toolName, string parameters)
        => Task.FromResult("{\"success\":true}");

    public ToolValidationResult ValidateParameters(string toolName, string parameters)
        => new() { IsValid = true };

    // B9 退役：旧 ExecuteToolWithResultAsync/ExecuteToolWithTimeoutAsync 已删，替身只保留六闸门执行面。
    public Task<ToolExecutionResult> ExecuteAsync(ToolExecution execution, CancellationToken cancellationToken = default)
        => Task.FromResult(Execute(execution.ToolName, execution.ArgsJson, execution.CallId));

    public Task<IReadOnlyList<ToolExecutionResult>> ExecuteBatchAsync(
        IReadOnlyList<ToolCallRef> calls, string sessionId, CancellationToken cancellationToken = default)
    {
        var results = new List<ToolExecutionResult>();
        foreach (var call in calls)
        {
            results.Add(Execute(call.ToolName, call.ArgsJson, call.CallId));
        }
        return Task.FromResult<IReadOnlyList<ToolExecutionResult>>(results);
    }

    private ToolExecutionResult Execute(string toolName, string parameters, string? callId = null)
    {
        Executions.Add((toolName, parameters));
        return new ToolExecutionResult
        {
            CallId = callId,
            Success = true,
            Result = "{\"success\":true,\"tool\":\"" + toolName + "\"}",
            ToolName = toolName,
            DurationMs = 1,
            Outcome = ToolOutcome.Ok
        };
    }
}
