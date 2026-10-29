using System.Text.Json;
using ForgeSelf.Abstractions;
using Microsoft.Extensions.AI;

namespace ForgeSelf.Api.Plugins.AIAgent.MafLoop;

/// <summary>
/// 宿主工具注册表工具 → M.E.AI <see cref="AIFunction"/> 包装（PILOT-051 FR-2.4）。
/// MAF 的 function-invocation 管线经 <see cref="InvokeCoreAsync"/> 落到宿主六闸门管线
/// <see cref="IToolRegistry.ExecuteAsync"/>，并同步产出与 <see cref="ReactLoopAgent"/> 一致的
/// <c>tool/call</c>/<c>tool/result</c> 日志事件与 ToolStarted/ToolCompleted live 帧。
/// </summary>
public sealed class RegistryToolFunction : AIFunction
{
    private readonly IToolRegistry _toolExecutor;
    private readonly string _sessionId;
    private readonly JsonElement _schema;
    private readonly MafLoopBridge _bridge;
    private readonly string _callIdPrefix;

    public RegistryToolFunction(
        string name,
        string description,
        JsonElement schema,
        IToolRegistry toolExecutor,
        string sessionId,
        MafLoopBridge bridge,
        string callIdPrefix)
    {
        Name = name;
        Description = description ?? string.Empty;
        _schema = schema.ValueKind == JsonValueKind.Object ? schema : JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}");
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _sessionId = sessionId;
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _callIdPrefix = callIdPrefix;
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override string Description { get; }

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var argsJson = JsonSerializer.Serialize((IReadOnlyDictionary<string, object?>)arguments);
        var callId = $"{_callIdPrefix}-{Guid.NewGuid().ToString("N")[..8]}";

        await _bridge.RecordToolStartAsync(callId, Name, argsJson, cancellationToken).ConfigureAwait(false);

        var execution = new ToolExecution
        {
            CallId = callId,
            ToolName = Name,
            ArgsJson = argsJson,
            SessionId = _sessionId,
            Signal = cancellationToken
        };

        try
        {
            var result = await _toolExecutor.ExecuteAsync(execution, cancellationToken).ConfigureAwait(false);
            var outcome = result.Outcome ?? (result.Success ? ToolOutcome.Ok : ToolOutcome.Error);
            await _bridge.RecordToolEndAsync(callId, Name, result.Result, outcome, result.DurationMs, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                return string.IsNullOrWhiteSpace(result.Result) ? "{\"success\":true}" : result.Result;
            }

            // 失败结果照 M.E.AI 约定返回错误描述文本（由管线作为工具结果喂回模型，模型可自纠）。
            return JsonSerializer.Serialize(new { success = false, error = result.ErrorMessage ?? outcome.ToString() });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _bridge.RecordToolEndAsync(callId, Name,
                JsonSerializer.Serialize(new { success = false, error = ex.Message }), ToolOutcome.Error, 0,
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}

/// <summary>
/// 任务循环完成出口工具（<c>task_complete</c>）的 M.E.AI 声明（PILOT-051 FR-1）。
/// 声明即出口：真实执行由 <c>MafTaskLoopAgent</c> 的 <c>FunctionInvoker</c> 短路
/// （置 <c>FunctionInvocationContext.Terminate</c>），不走六闸门管线。
/// </summary>
public sealed class TaskCompleteAIFunction : AIFunction
{
    private readonly JsonElement _schema;

    public TaskCompleteAIFunction(string description, string parametersJsonSchema)
    {
        Description = description;
        try
        {
            _schema = JsonSerializer.Deserialize<JsonElement>(parametersJsonSchema);
        }
        catch
        {
            _schema = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}");
        }
    }

    /// <inheritdoc />
    public override string Name => AgentOptions.TaskCompleteToolName;

    /// <inheritdoc />
    public override string Description { get; }

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        // 正常路径由 FunctionInvoker 短路；防御性兜底。
        => ValueTask.FromResult<object?>(JsonSerializer.Serialize(new { success = true, message = "任务已完成" }));
}

/// <summary>
/// 出口工具（ExitToolNames 命中）的 M.E.AI 声明：声明即出口（B7 语义），不真实执行。
/// 计划驱动路径的 submit_plan/complete_step/request_help 等若经 MAF 运行时挂载，走本类。
/// </summary>
public sealed class ExitToolAIFunction : AIFunction
{
    private readonly JsonElement _schema;

    public ExitToolAIFunction(string name, string description, string parametersJsonSchema)
    {
        Name = name;
        Description = description;
        try
        {
            _schema = JsonSerializer.Deserialize<JsonElement>(parametersJsonSchema);
        }
        catch
        {
            _schema = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}");
        }
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override string Description { get; }

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => ValueTask.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
}
