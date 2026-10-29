using System.Diagnostics;
using System.Runtime.CompilerServices;
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
/// PILOT-051 门禁测试：MafTaskLoopAgent（MAF 驱动的任务循环）。
/// </summary>
/// <remarks>
/// 判据与 <see cref="ReactLoopAgentTests"/> 同一纪律：一切持久事实打在<b>会话日志事件序</b>上；
/// live 帧只断言存在性。脚本化模型（<see cref="ScriptedProvider"/>）与假工具注册表复用 B5 夹具。
/// </remarks>
[Collection("XCode")]
public class MafTaskLoopAgentTests
{
    // ---- AC1a：纯问答（整回合零工具调用）→ 照旧收束，不触发续跑 ----
    [Fact]
    public async Task PureQA_TextOnly_CompletesNormally()
    {
        var fx = new MafFixture();
        fx.Provider.Enqueue(Script.Text("你好，有什么可以帮你？", finish: "stop"));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "你好");
        await fx.RunAsync(agent);

        Assert.Equal(
            new[]
            {
                "turn/start", "step/start", "user/message", "request/header",
                "assistant/message", "step/end", "turn/end"
            },
            fx.EventTypes());

        // 纯问答不强制工具调用
        Assert.Null(fx.Provider.Requests.Single().ToolChoice);
        // 任务循环协议已注入系统提示
        var header = fx.Store.Replay(fx.SessionId).OfType<RequestHeaderEvent>().Single();
        Assert.Contains("任务循环协议", header.RenderedSystemPrompt);
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    // ---- AC1b（核心投诉场景）：工具调用后模型只回文本 → 不停，续跑到 task_complete ----
    [Fact]
    public async Task ToolCallThenInterimText_ContinuesUntilTaskComplete()
    {
        var fx = new MafFixture();
        fx.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"));
        fx.Provider.Enqueue(Script.Text("现在是 12:00", finish: "stop"));
        fx.Provider.Enqueue(Script.ToolCall("exit-1", "task_complete", "{\"summary\":\"时间已告知，任务完成\"}"));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "现在几点？告诉我后就完成任务");
        var frames = await fx.RunAsync(agent);

        var types = fx.EventTypes();

        // 至少 2 个 step：step0 工具+中间文本 → 续跑 step1 task_complete 出口
        Assert.True(types.Count(t => t == "step/start") >= 2, "模型纯文本未出口必须续跑，不得结束回合");
        Assert.Equal(2, types.Count(t => t == "tool/call"));        // get_current_time + task_complete
        Assert.Equal(2, types.Count(t => t == "tool/result"));

        // 最后一步以 ExitTool 收束，回合正常完成
        var lastStepEnd = types.Last(t => t == "step/end");
        var stepEnds = fx.Store.Replay(fx.SessionId).OfType<StepEndEvent>().ToList();
        Assert.Equal(StepEndReason.ExitTool, stepEnds.Last().Reason);
        Assert.Contains(frames, f => f is TurnCompleted { Reason: TurnEndReason.Completed });
        Assert.Contains(frames, f => f is ExitToolInvoked { ToolName: "task_complete" });

        // task_complete 走出口短路：日志有合成 tool/result(Ok)，且未进六闸门执行器
        Assert.Equal(new[] { "get_current_time" }, fx.Tools.Executions.Select(e => e.Name).ToArray());
        var taskCompleteResult = fx.Store.Replay(fx.SessionId).OfType<ToolResultEvent>()
            .Single(r => r.ToolName == "task_complete");
        Assert.Equal(ToolOutcome.Ok, taskCompleteResult.Outcome);

        // 续跑步强制工具调用（ToolChoice=required）
        Assert.Equal("required", fx.Provider.Requests[2].ToolChoice);
        // 模型在第二步请求中能看到工具结果（tool 角色消息）
        Assert.Contains(fx.Provider.Requests[1].Messages, m => m.Role == "tool");
    }

    // ---- AC1c：模型在同一个 MAF Run 内直接调 task_complete → 一步收束（MAF Terminate 生效） ----
    [Fact]
    public async Task TaskCompleteWithinSameRun_EndsTurnImmediately()
    {
        var fx = new MafFixture();
        fx.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"));
        fx.Provider.Enqueue(Script.ToolCall("exit-1", "task_complete", "{\"summary\":\"完成\"}"));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "查时间然后完成任务");
        await fx.RunAsync(agent);

        var types = fx.EventTypes();
        Assert.Equal(1, types.Count(t => t == "step/start"));
        Assert.Equal(StepEndReason.ExitTool, fx.Store.Replay(fx.SessionId).OfType<StepEndEvent>().Single().Reason);
        Assert.Equal(2, types.Count(t => t == "tool/call"));
    }

    // ---- AC1d：续跑有界：反复纯文本 → 触达 MaxStepsPerTurn 强制收束，不死循环 ----
    [Fact]
    public async Task Continuation_BoundedByMaxStepsPerTurn()
    {
        var fx = new MafFixture(maxStepsPerTurn: 3);
        fx.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"));
        fx.Provider.Enqueue(Script.Text("还没做完", finish: "stop"), repeatLast: true);

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "开始任务");
        var frames = await fx.RunAsync(agent);

        Assert.Equal(3, fx.EventTypes().Count(t => t == "step/start"));
        Assert.DoesNotContain(frames, f => f is TurnFailed);
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    // ---- AC1e：上游空响应（无正文、无工具调用）→ 判失败，不得当成功收束 ----
    [Fact]
    public async Task EmptyUpstreamResponse_FailsInsteadOfCompleting()
    {
        var fx = new MafFixture();
        fx.Provider.Enqueue(Script.Text(string.Empty, finish: "stop"));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "随便说点什么");
        var frames = await fx.RunAsync(agent);

        Assert.Equal(StepEndReason.Failed, fx.Store.Replay(fx.SessionId).OfType<StepEndEvent>().Single().Reason);
        var failed = Assert.Single(frames.OfType<TurnFailed>());
        Assert.Contains("未产生任何模型产出", failed.Error);
    }

    // ---- AC1f：请求中取消：无半截 assistant/message，turn/end = Aborted ----
    [Fact]
    public async Task Cancel_DuringStream_NoPartialCommit()
    {
        var fx = new MafFixture();
        var hanging = new HangingProvider();
        var agent = fx.CreateAgent(provider: hanging);
        agent.Inbox.Followup(fx.SessionId, "讲个长故事");

        var frames = new List<TurnFrame>();
        var runTask = Task.Run(async () =>
        {
            await foreach (var frame in agent.RunAsync())
            {
                frames.Add(frame);
            }
        });

        // 等到首个 AssistantDelta（流已开始）再取消
        var sw = Stopwatch.StartNew();
        while (!frames.OfType<AssistantDelta>().Any() && sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(10);
        }
        Assert.True(frames.OfType<AssistantDelta>().Any(), "取消前应已收到流式增量");
        await agent.CancelAsync(new AgentCancelCause.User());
        await runTask;

        // 无半截提交：本回合没有 assistant/message 落盘
        Assert.DoesNotContain(fx.Store.Replay(fx.SessionId), e => e.Type == "assistant/message");
        Assert.Equal(TurnEndReason.Aborted, fx.Store.Replay(fx.SessionId).OfType<TurnEndEvent>().Single().Reason);
        Assert.Contains(frames, f => f is TurnCompleted { Reason: TurnEndReason.Aborted });
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    // ---- turn-stopping 拦截点：监听器要求再跑一步 → 继续（与 ReactLoopAgent 同语义） ----
    [Fact]
    public async Task TurnStopping_ListenerFalse_TakesAnotherStep()
    {
        var fx = new MafFixture();
        fx.Provider.Enqueue(Script.Text("第一步完成", finish: "stop"), repeatLast: true);
        var calls = 0;
        using var _ = fx.Events.OnSerial<TurnStoppingContext, bool?>(
            "agent/turn-stopping", _ => Task.FromResult<bool?>(calls++ == 0 ? false : true));

        var agent = fx.CreateAgent();
        agent.Inbox.Followup(fx.SessionId, "需要多跑一步");
        await fx.RunAsync(agent);

        Assert.Equal(2, fx.EventTypes().Count(t => t == "step/start"));
    }
}

/// <summary>MAF 任务循环测试脚手架：内存会话日志 + 脚本化提供方 + 假工具注册表（复用 B5 替身）。</summary>
internal sealed class MafFixture
{
    public string SessionId { get; } = "s-" + Guid.NewGuid().ToString("N")[..8];

    public InMemorySessionStore Store { get; } = new();

    public ScriptedProvider Provider { get; } = new();

    public FakeToolRegistry Tools { get; } = new();

    public EventBus Events { get; } = new();

    public InMemoryInbox Inbox { get; } = new();

    private readonly int _maxStepsPerTurn;

    public MafFixture(int maxStepsPerTurn = 10)
    {
        _maxStepsPerTurn = maxStepsPerTurn;
    }

    public MafTaskLoopAgent CreateAgent(string? sessionId = null, AgentOptions? options = null, IAIProvider? provider = null)
        => new(sessionId ?? SessionId,
            options ?? new AgentOptions { MaxStepsPerTurn = _maxStepsPerTurn, TaskLoopMode = true },
            new AgentTurnRuntime
            {
                Store = Store,
                Provider = provider ?? Provider,
                ModelId = "fake-model",
                ToolExecutor = Tools,
                SystemPrompt = null,
                ToolDefinitions = new List<UnifiedToolDefinition>
                {
                    new()
                    {
                        Name = "get_current_time",
                        Description = "获取当前时间",
                        Parameters = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}")
                    }
                },
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
}

/// <summary>挂起提供方：发一个增量后无限等待取消（取消语义测试专用）。</summary>
internal sealed class HangingProvider : IAIProvider
{
    public string ProviderName => "fake";
    public AIProviderType ProviderType => AIProviderType.Custom;
    public List<string> SupportedModels => new() { "fake-model" };
    public bool IsDefault => true;

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("本替身只支持流式");

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(
        UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new UnifiedStreamChunk { DeltaContent = "部分" };
        await Task.Delay(Timeout.Infinite, cancellationToken);
        yield break; // 不可达：取消后 Task.Delay 抛出
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ModelInfo>());
}

/// <summary>
/// PILOT-051 AC4：ForgeChatClientAdapter 请求/响应双向翻译单元测试。
/// </summary>
[Collection("XCode")]
public class ForgeChatClientAdapterTests
{
    // ---- 请求侧：M.E.AI 消息/选项 → UnifiedChatRequest ----
    [Fact]
    public async Task MapsRequest_MessagesSystemToolsToolMode()
    {
        var provider = new RecordingProvider();
        var adapter = new ForgeChatClientAdapter(provider, "upstream-model");

        var options = new ChatOptions
        {
            ModelId = "override-model",
            Instructions = "你是测试助手",
            ToolMode = ChatToolMode.RequireAny,
            Temperature = 0.3f,
            Tools = new List<AITool>
            {
                AIFunctionFactory.Create(() => "ok", "echo_tool", "回显工具")
            }
        };
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "系统级提示"),
            new(ChatRole.User, "用户问题"),
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent("c1", "echo_tool", new Dictionary<string, object?> { ["x"] = "1" })
            }),
            new(ChatRole.Tool, new List<AIContent> { new FunctionResultContent("c1", "{\"ok\":true}") })
        };

        await foreach (var _ in adapter.GetStreamingResponseAsync(messages, options))
        {
        }

        var request = Assert.Single(provider.Requests);
        Assert.Equal("override-model", request.Model);
        Assert.Equal("required", request.ToolChoice);
        Assert.Equal(0.3f, request.Temperature);
        Assert.Contains("你是测试助手", request.SystemPrompt);
        Assert.Contains("系统级提示", request.SystemPrompt);

        Assert.Equal(new[] { "user", "assistant", "tool" }, request.Messages.Select(m => m.Role).ToArray());
        var assistant = request.Messages.Single(m => m.Role == "assistant");
        var call = Assert.Single(assistant.ToolCalls!);
        Assert.Equal("c1", call.Id);
        Assert.Equal("echo_tool", call.Name);
        Assert.Contains("x", call.Arguments);

        var tool = request.Messages.Single(m => m.Role == "tool");
        Assert.Equal("c1", tool.ToolCallId);
        Assert.Contains("ok", tool.Content);

        Assert.NotNull(request.Tools);
        Assert.Equal("echo_tool", Assert.Single(request.Tools!).Name);
    }

    // ---- 响应侧：UnifiedStreamChunk → ChatResponseUpdate ----
    [Fact]
    public async Task MapsStreamChunks_TextToolCallUsageFinish()
    {
        var chunks = new List<UnifiedStreamChunk>
        {
            new() { Id = "resp-1", DeltaContent = "你好" },
            new() { DeltaToolCall = new UnifiedToolCall { Id = "c9", Name = "read_file", Arguments = "{\"path\":\"a.txt\"}" } },
            new() { Usage = new UnifiedUsage { PromptTokens = 11, CompletionTokens = 7 } },
            new() { FinishReason = "tool_calls" }
        };
        var provider = new ScriptedStreamProvider(chunks);
        var adapter = new ForgeChatClientAdapter(provider, "fake-model");

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in adapter.GetStreamingResponseAsync(new List<ChatMessage> { new(ChatRole.User, "hi") }))
        {
            updates.Add(update);
        }

        var text = updates.SelectMany(u => u.Contents).OfType<TextContent>().Single();
        Assert.Equal("你好", text.Text);

        var fcc = updates.SelectMany(u => u.Contents).OfType<FunctionCallContent>().Single();
        Assert.Equal("c9", fcc.CallId);
        Assert.Equal("read_file", fcc.Name);
        Assert.Equal("a.txt", ((JsonElement)fcc.Arguments!["path"]).GetString());

        var usage = updates.SelectMany(u => u.Contents).OfType<UsageContent>().Single();
        Assert.Equal(11, usage.Details.InputTokenCount);
        Assert.Equal(7, usage.Details.OutputTokenCount);

        Assert.Contains(updates, u => u.FinishReason == ChatFinishReason.ToolCalls);
        Assert.Contains(updates, u => u.ResponseId == "resp-1");
    }

    // ---- PC-015 回归：OpenAI 流式工具调用分片必须聚合为完整调用（修复前逐片成调用毒化会话） ----
    [Fact]
    public async Task StreamingToolCallFragments_MergeIntoSingleCompleteCall()
    {
        var chunks = new List<UnifiedStreamChunk>
        {
            new() { DeltaContent = "先列目录" },
            // 首片：带 Id + Name + 参数前缀
            new() { DeltaToolCall = new UnifiedToolCall { Id = "c1", Name = "list_files", Arguments = "{\"pa" } },
            // 续片：只有参数增量（OpenAI 协议：Id/Name 只在首片出现）
            new() { DeltaToolCall = new UnifiedToolCall { Arguments = "th\":\"docs\"}" } },
            new() { Usage = new UnifiedUsage { PromptTokens = 5, CompletionTokens = 3 } },
            new() { FinishReason = "tool_calls" }
        };
        var provider = new ScriptedStreamProvider(chunks);
        var adapter = new ForgeChatClientAdapter(provider, "fake-model");

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in adapter.GetStreamingResponseAsync(new List<ChatMessage> { new(ChatRole.User, "hi") }))
        {
            updates.Add(update);
        }

        var calls = updates.SelectMany(u => u.Contents).OfType<FunctionCallContent>().ToList();
        Assert.Single(calls);                                   // 修复前：3 条（含 2 条空名）
        Assert.Equal("c1", calls[0].CallId);
        Assert.Equal("list_files", calls[0].Name);
        Assert.Equal("docs", ((JsonElement)calls[0].Arguments!["path"]).GetString());
    }

    [Fact]
    public async Task StreamingToolCallFragments_ParallelCallsMergedInOrder()
    {
        var chunks = new List<UnifiedStreamChunk>
        {
            new() { DeltaToolCall = new UnifiedToolCall { Id = "c1", Name = "read_file", Arguments = "{\"path\":\"a" } },
            new() { DeltaToolCall = new UnifiedToolCall { Arguments = ".txt\"}" } },
            new() { DeltaToolCall = new UnifiedToolCall { Id = "c2", Name = "get_current_time", Arguments = "{\"tz\":\"C" } },
            new() { DeltaToolCall = new UnifiedToolCall { Arguments = "N\"}" } },
            new() { FinishReason = "tool_calls" }
        };
        var provider = new ScriptedStreamProvider(chunks);
        var adapter = new ForgeChatClientAdapter(provider, "fake-model");

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in adapter.GetStreamingResponseAsync(new List<ChatMessage> { new(ChatRole.User, "hi") }))
        {
            updates.Add(update);
        }

        var calls = updates.SelectMany(u => u.Contents).OfType<FunctionCallContent>().ToList();
        Assert.Equal(2, calls.Count);                           // 修复前：4 条（两条空名）
        Assert.Equal("read_file", calls[0].Name);
        Assert.Equal("a.txt", ((JsonElement)calls[0].Arguments!["path"]).GetString());
        Assert.Equal("get_current_time", calls[1].Name);
        Assert.Equal("CN", ((JsonElement)calls[1].Arguments!["tz"]).GetString());
    }

    [Fact]
    public async Task MapsRequest_SkipsEmptyNameToolCallsFromHistory()
    {
        // PC-015 自愈：旧缺陷落库的空名调用（空壳）不允许再进模型请求
        var provider = new RecordingProvider();
        var adapter = new ForgeChatClientAdapter(provider, "upstream-model");
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "继续"),
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent("bad", string.Empty, new Dictionary<string, object?>()),
                new FunctionCallContent("c1", "echo_tool", new Dictionary<string, object?> { ["x"] = "1" })
            })
        };

        await foreach (var _ in adapter.GetStreamingResponseAsync(messages))
        {
        }

        var request = Assert.Single(provider.Requests);
        var assistant = request.Messages.Single(m => m.Role == "assistant");
        var call = Assert.Single(assistant.ToolCalls!);          // 修复前：2 条（含空名）
        Assert.Equal("echo_tool", call.Name);
    }

    // ---- 出口工具 AIFunction 声明：schema 必须带 required summary ----
    [Fact]
    public void TaskCompleteAIFunction_ExposesSchema()
    {
        var fn = new TaskCompleteAIFunction("任务完成出口", "{\"type\":\"object\",\"properties\":{\"summary\":{\"type\":\"string\"}},\"required\":[\"summary\"]}");
        Assert.Equal(AgentOptions.TaskCompleteToolName, fn.Name);
        Assert.Equal("task_complete", fn.Name);
        Assert.Equal(JsonValueKind.Object, fn.JsonSchema.ValueKind);
        Assert.Contains("summary", fn.JsonSchema.GetProperty("required").EnumerateArray().First().GetString());
    }
}

/// <summary>只记录请求的提供方替身（请求侧翻译测试用）。</summary>
internal sealed class RecordingProvider : IAIProvider
{
    public List<UnifiedChatRequest> Requests { get; } = new();

    public string ProviderName => "fake";
    public AIProviderType ProviderType => AIProviderType.Custom;
    public List<string> SupportedModels => new() { "fake-model" };
    public bool IsDefault => true;

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(
        UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        await Task.Yield();
        yield return new UnifiedStreamChunk { FinishReason = "stop" };
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ModelInfo>());
}

/// <summary>按固定分片序列回放的提供方替身（响应侧翻译测试用）。</summary>
internal sealed class ScriptedStreamProvider : IAIProvider
{
    private readonly List<UnifiedStreamChunk> _chunks;

    public ScriptedStreamProvider(List<UnifiedStreamChunk> chunks) => _chunks = chunks;

    public string ProviderName => "fake";
    public AIProviderType ProviderType => AIProviderType.Custom;
    public List<string> SupportedModels => new() { "fake-model" };
    public bool IsDefault => true;

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(
        UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var chunk in _chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ModelInfo>());
}
