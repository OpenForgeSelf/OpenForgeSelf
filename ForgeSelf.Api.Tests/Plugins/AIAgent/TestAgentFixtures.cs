using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.MafLoop;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// 共享引擎测试脚手架（B5 起唯一运行时 = MafTaskLoopAgent）：
/// 内存会话日志 + 脚本化提供方 + 假工具注册表 + 收件箱替身。
/// 原 ReactLoopAgentTests 内嵌脚手架迁此承载，供引擎门禁 / 收件箱门禁 / 出口挂起 / 编排器系测试共用。
/// </summary>
internal sealed class Fixture
{
    public string SessionId { get; } = "s-" + Guid.NewGuid().ToString("N")[..8];

    public InMemorySessionStore Store { get; } = new();

    public ScriptedProvider Provider { get; } = new();

    public FakeToolRegistry Tools { get; } = new();

    public EventBus Events { get; } = new();

    /// <summary>宿主收件箱替身（B6：引擎经 AgentTurnRuntime.Inbox 接宿主收件箱）。</summary>
    public InMemoryInbox Inbox { get; } = new();

    private readonly int _maxStepsPerTurn;

    public Fixture(int maxStepsPerTurn = 10)
    {
        _maxStepsPerTurn = maxStepsPerTurn;
    }

    /// <summary>构造被测引擎（B5：ReactLoopAgent 已删除，唯一运行时 = MafTaskLoopAgent）。</summary>
    /// <remarks>
    /// MAF 引擎的工具声明是显式通道（ToolDefinitions），与旧 ReactLoop「从注册表自解析」不同：
    /// 默认声明 get_current_time（与 FakeToolRegistry 注册面一致），出口工具经 ExtraTools/内置 task_complete 挂载。
    /// </remarks>
    public IAgent CreateAgent(string? sessionId = null, AgentOptions? options = null, IAIProvider? provider = null)
        => new MafTaskLoopAgent(
            sessionId ?? SessionId,
            options ?? new AgentOptions { MaxStepsPerTurn = _maxStepsPerTurn },
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
                        Parameters = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"type\":\"object\"}")
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

        // 入口回夹（耗尽后重复最后一段，RepeatLast 语义不变），但推进是无条件的：
        // 「回合间追加脚本」的对抗用例（QA 失败→重试模式）依赖第二次 Enqueue 能被下一请求取到，
        // 旧实现「仅在存在后继时推进」会在单脚本阶段把索引留在 0，导致后续 Enqueue 被回放掩盖。
        if (_index >= _scripts.Count)
        {
            _index = _scripts.Count - 1;
        }

        var script = _scripts[_index];
        _index++;
        return script;
    }
}

/// <summary>
/// 挂起测试替身：首次请求先吐一个增量（制造「有进展」）后永久挂起（无 finishReason、无后续产出），
/// 触发请求级无进展看门狗；恢复后的请求返回正常文本收束。
/// </summary>
internal sealed class HangThenTextProvider : IAIProvider
{
    private bool _hung;

    /// <summary>每次请求收到的统一请求（调试观察用）。</summary>
    public List<UnifiedChatRequest> Requests { get; } = new();

    public string ProviderName => "fake-hang";
    public AIProviderType ProviderType => AIProviderType.Custom;
    public List<string> SupportedModels => new() { "fake-model" };
    public bool IsDefault => true;

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("本测试替身只支持流式");

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(
        UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        if (!_hung)
        {
            _hung = true;
            yield return new UnifiedStreamChunk { DeltaContent = "开始…" };
            // 永久挂起直到取消（看门狗触发 OCE；非用户取消 → Suspended）
            await Task.Delay(Timeout.Infinite, cancellationToken);
            yield break;
        }

        yield return new UnifiedStreamChunk { DeltaContent = "已恢复" };
        yield return new UnifiedStreamChunk { FinishReason = "stop" };
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ModelInfo>());
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

/// <summary>出口工具替身（声明即出口：经 ExtraTools 通道成为模型可见声明，不真实执行）。</summary>
internal sealed class FakeExitTool : IToolFunctionExtension
{
    public FakeExitTool(string name, string parametersJsonSchema)
    {
        Name = name;
        ParametersJsonSchema = parametersJsonSchema;
    }

    public string Id => Name;

    public string Name { get; }

    public string PluginId => "ai-agent";

    public string Description => $"出口工具 {Name}（声明即出口，不执行）";

    public string ParametersJsonSchema { get; }

    public Task<string> ExecuteAsync(string parameters)
        => throw new InvalidOperationException("出口工具是声明不是执行，不得执行");
}
