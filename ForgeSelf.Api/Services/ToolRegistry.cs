using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 宿主工具注册表（B8/042 六闸门管线版）。
/// </summary>
/// <remarks>
/// <para>
/// 执行管线（<see cref="ExecuteAsync"/>）：<c>tools/pre-execute(Serial 三态) → 单调守卫 →
/// tools/execute(waterfall) → tools/post-execute(waterfall) → finalize(恰好一次) → tools/result(EmitAsync 冻结快照)</c>。
/// 每条路径（成功/拒绝/早退/取消/异常）都经 <see cref="FinalizeAsync"/> 收口——finalize 恰好一次（红线 5）。
/// </para>
/// <para>
/// B9（退役收尾）：旧 <c>ExecuteToolWithResultAsync</c> / <c>ExecuteToolWithTimeoutAsync</c> 已随
/// 调用方迁移删除；<see cref="ToolCallContext"/> 旧监听契约同步退役（tools/* 拦截点统一以
/// <see cref="ToolExecution"/> 为 payload）。<see cref="ExecuteToolAsync"/> 保留为旧 string 门面。
/// </para>
/// </remarks>
public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, IToolFunctionExtension> _tools = new();
    private readonly IServiceProvider? _serviceProvider;
    private readonly IEventBus _events;
    private readonly IToolGuardRegistry? _guards;
    private readonly IApprovalService? _approval;

    public ToolRegistry(
        IServiceProvider? serviceProvider = null,
        IEventBus? events = null,
        IToolGuardRegistry? guards = null,
        IApprovalService? approval = null)
    {
        _serviceProvider = serviceProvider;
        _events = events ?? new EventBus();
        _guards = guards;
        _approval = approval;
    }

    public void RegisterTool(IToolFunctionExtension tool)
    {
        if (tool == null)
        {
            XTrace.Log.Error("[ToolRegistry] 注册工具失败：工具实例为空");
            return;
        }

        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            XTrace.Log.Error("[ToolRegistry] 注册工具失败：工具名称为空");
            return;
        }

        if (_tools.TryAdd(tool.Name, tool))
        {
            XTrace.Log.Info("[ToolRegistry] 注册工具成功: {0} (插件: {1}) - {2}", tool.Name, tool.PluginId, tool.Description);
        }
        else
        {
            XTrace.Log.Warn("[ToolRegistry] 工具已存在，注册失败: {0}", tool.Name);
        }
    }

    public void UnregisterTool(string toolId)
    {
        if (string.IsNullOrEmpty(toolId))
        {
            return;
        }

        var toolToRemove = _tools.Values.FirstOrDefault(t => t.Id == toolId);
        if (toolToRemove != null)
        {
            _tools.TryRemove(toolToRemove.Name, out _);
            XTrace.Log.Info("[ToolRegistry] 注销工具成功: {0}", toolToRemove.Name);
        }
    }

    public IToolFunctionExtension? GetTool(string toolName)
    {
        if (string.IsNullOrEmpty(toolName))
        {
            return null;
        }

        _tools.TryGetValue(toolName, out var tool);
        return tool;
    }

    public IEnumerable<IToolFunctionExtension> GetAllTools()
    {
        return _tools.Values;
    }

    public List<AIToolDefinition> GetToolDefinitions()
    {
        var definitions = new List<AIToolDefinition>();

        foreach (var tool in _tools.Values)
        {
            try
            {
                var parameters = JsonDocument.Parse(tool.ParametersJsonSchema).RootElement.Clone();

                definitions.Add(new AIToolDefinition
                {
                    Type = "function",
                    Function = new AIFunctionDefinition
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Parameters = parameters
                    }
                });
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[ToolRegistry] 解析工具参数Schema失败 [{0}]: {1}", tool.Name, ex.Message);
            }
        }

        XTrace.Log.Debug("[ToolRegistry] 获取工具定义，共 {0} 个工具", definitions.Count);
        return definitions;
    }

    public ToolValidationResult ValidateParameters(string toolName, string parameters)
    {
        var result = new ToolValidationResult();

        try
        {
            var tool = GetTool(toolName);
            if (tool == null)
            {
                result.IsValid = false;
                result.Errors.Add($"工具 '{toolName}' 不存在");
                return result;
            }

            if (string.IsNullOrWhiteSpace(parameters))
            {
                parameters = "{}";
            }

            using var paramsDoc = JsonDocument.Parse(parameters);
            var paramsRoot = paramsDoc.RootElement;

            if (paramsRoot.ValueKind != JsonValueKind.Object)
            {
                result.IsValid = false;
                result.Errors.Add("参数必须是JSON对象");
                return result;
            }

            using var schemaDoc = JsonDocument.Parse(tool.ParametersJsonSchema);
            var schemaRoot = schemaDoc.RootElement;

            if (schemaRoot.TryGetProperty("required", out var requiredProp))
            {
                foreach (var reqProp in requiredProp.EnumerateArray())
                {
                    var propName = reqProp.GetString();
                    if (!string.IsNullOrEmpty(propName) && !paramsRoot.TryGetProperty(propName, out _))
                    {
                        result.Errors.Add($"缺少必填参数: {propName}");
                    }
                }
            }

            if (schemaRoot.TryGetProperty("properties", out var propertiesProp))
            {
                foreach (var prop in propertiesProp.EnumerateObject())
                {
                    var propName = prop.Name;
                    var propSchema = prop.Value;

                    if (paramsRoot.TryGetProperty(propName, out var paramValue))
                    {
                        var typeValidation = ValidateParameterType(propName, paramValue, propSchema);
                        if (!typeValidation.IsValid)
                        {
                            result.Errors.AddRange(typeValidation.Errors);
                        }

                        if (propSchema.TryGetProperty("enum", out var enumProp))
                        {
                            var enumValues = enumProp.EnumerateArray().Select(e => e.GetString()).ToList();
                            var paramStr = paramValue.GetString();
                            if (!string.IsNullOrEmpty(paramStr) && !enumValues.Contains(paramStr))
                            {
                                result.Errors.Add($"参数 '{propName}' 的值 '{paramStr}' 不在允许的枚举值中: [{string.Join(", ", enumValues)}]");
                            }
                        }
                    }
                }
            }

            result.IsValid = result.Errors.Count == 0;
            XTrace.Log.Debug("[ToolRegistry] 参数验证结果 [{0}]: {1}", toolName, result.IsValid ? "通过" : "失败");
        }
        catch (JsonException ex)
        {
            result.IsValid = false;
            result.Errors.Add($"参数JSON解析失败: {ex.Message}");
            XTrace.Log.Warn("[ToolRegistry] 参数验证异常 [{0}]: {1}", toolName, ex.Message);
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"参数验证异常: {ex.Message}");
            XTrace.Log.Error("[ToolRegistry] 参数验证异常 [{0}]: {1}", toolName, ex.Message);
        }

        return result;
    }

    private static ToolValidationResult ValidateParameterType(string propName, JsonElement value, JsonElement schema)
    {
        var result = new ToolValidationResult { IsValid = true };

        if (!schema.TryGetProperty("type", out var typeProp))
        {
            return result;
        }

        var expectedType = typeProp.GetString();
        if (string.IsNullOrEmpty(expectedType))
        {
            return result;
        }

        bool isValid = expectedType switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "number" => value.ValueKind == JsonValueKind.Number,
            "integer" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False,
            "array" => value.ValueKind == JsonValueKind.Array,
            "object" => value.ValueKind == JsonValueKind.Object,
            _ => true
        };

        if (!isValid)
        {
            result.IsValid = false;
            result.Errors.Add($"参数 '{propName}' 类型错误，期望 {expectedType}，实际 {value.ValueKind}");
        }

        return result;
    }

    /// <summary>
    /// 旧 string 门面（B9 保留）：委托 <see cref="ExecuteAsync"/> 走六闸门管线，返回 Result 原文。
    /// </summary>
    public async Task<string> ExecuteToolAsync(string toolName, string parameters)
    {
        var result = await ExecuteAsync(new ToolExecution
        {
            CallId = string.Empty,
            ToolName = toolName,
            ArgsJson = parameters,
            SessionId = string.Empty
        });
        return result.Result;
    }

    /// <summary>
    /// 六闸门管线单工具执行（B8）。
    /// </summary>
    /// <inheritdoc cref="ToolRegistry" path="/remarks" />
    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecution execution, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        execution.Signal = ct;

        var stopwatch = Stopwatch.StartNew();
        var result = new ToolExecutionResult
        {
            ToolName = execution.ToolName,
            CallId = execution.CallId
        };

        try
        {
            XTrace.Log.Info("[ToolRegistry] ========================================");
            XTrace.Log.Info("[ToolRegistry] 开始执行工具: {0} (CallId: {1})", execution.ToolName, execution.CallId);
            XTrace.Log.Debug("[ToolRegistry] 工具参数: {0}", execution.ArgsJson);

            // 前置事实（供闸门 1 观察者与早退路径共用）：工具存在性 + 参数校验
            var tool = GetTool(execution.ToolName);
            var validation = tool == null ? null : ValidateParameters(execution.ToolName, execution.ArgsJson);

            // ---- 闸门 1：tools/pre-execute —— 三态决策（无监听器 = null = Allow，A1） ----
            var pre = await _events.SerialAsync<ToolExecution, PreToolDecision?>("tools/pre-execute", execution);
            execution.PreDecision = pre;

            if (pre is PreToolDecision.Deny denied)
            {
                XTrace.Log.Warn("[ToolRegistry] 工具执行被拒绝(pre-execute) [{0}]: {1}", execution.ToolName, denied.Reason);
                return await FinalizeAsync(execution, result, stopwatch,
                    success: false,
                    resultJson: JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.PermissionDenied, denied.Reason, "该操作被权限策略拒绝")),
                    errorMessage: denied.Reason,
                    outcome: ToolOutcome.Denied,
                    denyReason: denied.Reason,
                    usageMetadata: new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.PermissionDenied });
            }

            if (pre is PreToolDecision.Ask ask)
            {
                // A1：Ask 必经审批——无审批服务 / 审批超时 / 取消 / 明确拒绝 → fail-closed（红线 4：AskAsync 必带 ct）
                var approved = _approval != null
                    && await _approval.AskAsync(execution.SessionId, execution.ToolName, execution.ArgsJson, ask.Reason, ct);
                if (!approved)
                {
                    var failReason = _approval == null
                        ? $"需要审批但宿主未注册审批服务，fail-closed 拒绝（{ask.Reason ?? "无审批原因"}）"
                        : $"审批未通过或已超时（{ask.Reason ?? "无审批原因"}）";
                    XTrace.Log.Warn("[ToolRegistry] 工具执行被拒绝(Ask fail-closed) [{0}]: {1}", execution.ToolName, failReason);
                    return await FinalizeAsync(execution, result, stopwatch,
                        success: false,
                        resultJson: JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.PermissionDenied, failReason, "该操作未获审批通过")),
                        errorMessage: failReason,
                        outcome: ToolOutcome.Denied,
                        denyReason: failReason,
                        usageMetadata: new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.PermissionDenied });
                }
            }

            // ---- 早退路径 1：工具不存在（保持原 TOOL_NOT_FOUND 语义；pre-execute 已触发供观察） ----
            if (tool == null)
            {
                var errorMsg = $"工具 '{execution.ToolName}' 不存在";
                XTrace.Log.Warn("[ToolRegistry] {0}", errorMsg);
                return await FinalizeAsync(execution, result, stopwatch,
                    success: false,
                    resultJson: JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.ToolNotFound, errorMsg, "请检查工具名称是否正确，或确认插件是否已启用")),
                    errorMessage: errorMsg,
                    outcome: ToolOutcome.Error,
                    denyReason: null,
                    usageMetadata: new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.ToolNotFound });
            }

            // ---- 早退路径 2：参数校验失败（保持原 INVALID_PARAMETERS 语义） ----
            if (validation is { IsValid: false })
            {
                var errorMsg = $"参数验证失败: {string.Join("; ", validation.Errors)}";
                XTrace.Log.Warn("[ToolRegistry] {0}", errorMsg);
                return await FinalizeAsync(execution, result, stopwatch,
                    success: false,
                    resultJson: JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.InvalidParameters, errorMsg, "请检查参数是否符合要求")),
                    errorMessage: errorMsg,
                    outcome: ToolOutcome.Error,
                    denyReason: null,
                    usageMetadata: new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.InvalidParameters, ["errors"] = validation.Errors });
            }

            // ---- 闸门 2：单调守卫（A2：只减不增，无 allow 结果；任一非 null → 最终拒绝，后续闸门无法撤销） ----
            var guardReason = CheckGuards(execution);
            if (guardReason != null)
            {
                XTrace.Log.Warn("[ToolRegistry] 工具执行被守卫拒绝 [{0}]: {1}", execution.ToolName, guardReason);
                return await FinalizeAsync(execution, result, stopwatch,
                    success: false,
                    resultJson: JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.PermissionDenied, guardReason, "该操作被守卫策略拒绝")),
                    errorMessage: guardReason,
                    outcome: ToolOutcome.Denied,
                    denyReason: guardReason,
                    usageMetadata: new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.PermissionDenied });
            }

            // ---- 闸门 3：tools/execute —— waterfall（B8-1/B8-3 修正）：中间件可短路或替换 Signal 后调 next() ----
            // 红线 1：WaterfallAsync(name, payload, fallback)，fallback 必填、无 ct——fallback 以 execution.Signal 调工具体。
            var executed = await _events.WaterfallAsync<ToolExecution, ToolExecutionResult>(
                "tools/execute",
                execution,
                () => InvokeToolBodyAsync(execution));

            // 执行视图回填（供闸门 4 中间件读取）
            execution.Success = executed.Success;
            execution.ResultJson = executed.Result;
            execution.ErrorMessage = executed.ErrorMessage;
            execution.DurationMs = stopwatch.ElapsedMilliseconds;

            // ---- 闸门 4：tools/post-execute —— waterfall（B8-1 修正）：接受 / 改写 / 阻断 ----
            var post = await _events.WaterfallAsync<ToolExecution, PostToolDecision?>(
                "tools/post-execute",
                execution,
                () => Task.FromResult<PostToolDecision?>(null));

            switch (post)
            {
                case PostToolDecision.AcceptContent acceptContent:
                    executed.Success = true;
                    executed.ErrorMessage = null;
                    executed.Result = acceptContent.Content;
                    break;

                case PostToolDecision.AcceptValue acceptValue:
                    executed.Result = acceptValue.ValueJson;
                    break;

                case PostToolDecision.Block block:
                    executed.Success = false;
                    executed.ErrorMessage = block.Feedback;
                    executed.Result = JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.ExecutionFailed, block.Feedback, "工具结果被 post-execute 阻断"));
                    break;
            }

            XTrace.Log.Info("[ToolRegistry] 工具执行完成: {0} (耗时: {1}ms, 成功: {2})", execution.ToolName, stopwatch.ElapsedMilliseconds, executed.Success);
            XTrace.Log.Debug("[ToolRegistry] 工具返回结果: {0}", executed.Result);
            XTrace.Log.Info("[ToolRegistry] ========================================");

            // ---- 闸门 5+6：finalize（恰好一次）+ tools/result（冻结快照广播） ----
            return await FinalizeAsync(execution, executed, stopwatch,
                success: executed.Success,
                resultJson: executed.Result,
                errorMessage: executed.ErrorMessage,
                outcome: executed.Outcome ?? (executed.Success ? ToolOutcome.Ok : ToolOutcome.Error),
                denyReason: executed.DenyReason,
                usageMetadata: executed.Success
                    ? null
                    : new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.ExecutionFailed, ["errorMessage"] = executed.ErrorMessage ?? string.Empty });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            XTrace.Log.Warn("[ToolRegistry] 工具执行被取消 [{0}]", execution.ToolName);
            return await FinalizeAsync(execution, result, stopwatch,
                success: false,
                resultJson: JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.Timeout, "工具执行被取消或超时", "请稍后重试或检查工具是否正常")),
                errorMessage: "工具执行被取消",
                outcome: ToolOutcome.Error,
                denyReason: null,
                usageMetadata: new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.Timeout });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            XTrace.Log.Error("[ToolRegistry] 工具执行异常 [{0}]: {1}", execution.ToolName, ex.Message);
            XTrace.Log.Debug("[ToolRegistry] 异常堆栈: {0}", ex.StackTrace);
            XTrace.Log.Info("[ToolRegistry] ========================================");

            return await FinalizeAsync(execution, result, stopwatch,
                success: false,
                resultJson: JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.ExecutionFailed, ex.Message, "请检查工具配置或联系管理员")),
                errorMessage: ex.Message,
                outcome: ToolOutcome.Error,
                denyReason: null,
                usageMetadata: new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.ExecutionFailed, ["errorMessage"] = ex.Message });
        }
    }

    /// <summary>
    /// 六闸门管线批量执行（B8/A4 model-ordered commit）：按模型返回顺序<b>串行</b>逐个执行，
    /// 取消后剩余 call 合成 <see cref="ToolOutcome.Skipped"/>——N 个 call 必有 N 个 result。
    /// </summary>
    public async Task<IReadOnlyList<ToolExecutionResult>> ExecuteBatchAsync(
        IReadOnlyList<ToolCallRef> calls, string sessionId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(calls);

        var results = new List<ToolExecutionResult>(calls.Count);
        foreach (var call in calls)
        {
            // model-ordered commit：每个 call 执行前检查取消；已取消 → 合成 Skipped（绝不静默丢弃）
            if (ct.IsCancellationRequested)
            {
                results.Add(BuildSkippedResult(call, "批次已取消"));
                continue;
            }

            ToolExecutionResult callResult;
            try
            {
                callResult = await ExecuteAsync(new ToolExecution
                {
                    CallId = call.CallId,
                    ToolName = call.ToolName,
                    ArgsJson = call.ArgsJson,
                    SessionId = sessionId
                }, ct);
            }
            catch (OperationCanceledException)
            {
                // 双保险：取消窗口内漏网 → 合成 Skipped，保持 N call N result
                callResult = BuildSkippedResult(call, "批次已取消");
            }

            results.Add(callResult);
        }

        return results;
    }

    private static ToolExecutionResult BuildSkippedResult(ToolCallRef call, string reason)
        => new()
        {
            CallId = call.CallId,
            ToolName = call.ToolName,
            Success = false,
            Result = JsonSerializer.Serialize(ToolCallResponse.Fail(ToolErrorCode.InternalError, reason, "该调用未执行")),
            ErrorMessage = reason,
            Outcome = ToolOutcome.Skipped
        };

    /// <summary>闸门 3 的 fallback：真实调用工具体（B8-3：fallback 必填、无 ct，信号取 <see cref="ToolExecution.Signal"/>）。</summary>
    private async Task<ToolExecutionResult> InvokeToolBodyAsync(ToolExecution execution)
    {
        var tool = GetTool(execution.ToolName)!;
        var stopwatch = Stopwatch.StartNew();
        var result = new ToolExecutionResult
        {
            ToolName = execution.ToolName,
            CallId = execution.CallId
        };

        // IToolFunctionExtension.ExecuteAsync(string) 无 CancellationToken 参数（旧接口），取消无法真传进工具体；
        // 以 WhenAny 对 execution.Signal 竞速（execute 中间件替换后的超时 Signal 同样生效——门禁 5）：
        // Signal 先触发 → 抛 OCE 由 ExecuteAsync 收口为取消结果；工具体转后台继续并由观察器兜住异常。
        var toolTask = tool.ExecuteAsync(execution.ArgsJson);
        var completed = await Task.WhenAny(toolTask, Task.Delay(Timeout.InfiniteTimeSpan, execution.Signal));
        if (completed != toolTask)
        {
            _ = ObserveOrphanAsync(toolTask, execution.ToolName);
            execution.Signal.ThrowIfCancellationRequested();
        }

        var raw = await toolTask;
        stopwatch.Stop();
        result.Success = true;
        result.Result = raw;
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    /// <summary>后台孤儿任务异常观察器（不等待、只兜底记录）。</summary>
    private static async Task ObserveOrphanAsync(Task<string> toolTask, string toolName)
    {
        try
        {
            await toolTask;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ToolRegistry] 工具超时/取消后后台任务异常结束 [{0}]: {1}", toolName, ex.Message);
        }
    }

    /// <summary>闸门 2：单调守卫检查。任一守卫非 null → 拒绝理由；守卫异常按 fail-closed 处理。</summary>
    private string? CheckGuards(ToolExecution execution)
    {
        if (_guards == null)
        {
            return null;
        }

        foreach (var guard in _guards.All())
        {
            string? reason;
            try
            {
                reason = guard(execution);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[ToolRegistry] 工具守卫异常（fail-closed）[{0}]: {1}", execution.ToolName, ex.Message);
                return $"工具守卫异常：{ex.Message}";
            }

            if (reason != null)
            {
                return reason;
            }
        }

        return null;
    }

    /// <summary>
    /// 闸门 5+6：finalize（组装最终结果，每次执行<b>恰好一次</b>，含 deny/早退/取消/异常路径）+
    /// tools/result（EmitAsync 广播冻结快照，观测失败被隔离——try/catch 吞掉，红线 5）。
    /// virtual 仅为测试可观测缝（门禁 10 计数），生产勿覆写。
    /// </summary>
    protected virtual async Task<ToolExecutionResult> FinalizeAsync(
        ToolExecution execution,
        ToolExecutionResult result,
        Stopwatch stopwatch,
        bool success,
        string resultJson,
        string? errorMessage,
        ToolOutcome outcome,
        string? denyReason,
        Dictionary<string, object>? usageMetadata)
    {
        // —— 闸门 5：finalize 恰好一次 ——
        stopwatch.Stop();
        result.Success = success;
        result.Result = resultJson;
        result.ErrorMessage = errorMessage;
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        result.Outcome = outcome;
        result.DenyReason = denyReason;
        result.CallId = execution.CallId;

        execution.DurationMs = result.DurationMs;

        // 使用统计（既有行为保持：成功不带元数据，失败带 errorCode）
        await RecordToolUsageAsync(execution.ToolName, success, result.DurationMs, usageMetadata);

        // —— 闸门 6：tools/result 冻结快照广播（监听器改字段无法影响主流程——门禁 9；观测失败被隔离） ——
        try
        {
            await _events.EmitAsync("tools/result", new ToolResultAnnouncement
            {
                CallId = execution.CallId,
                ToolName = execution.ToolName,
                SessionId = execution.SessionId,
                Success = success,
                ResultJson = resultJson,
                ErrorMessage = errorMessage,
                Outcome = outcome,
                DurationMs = result.DurationMs
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ToolRegistry] tools/result 观测失败（已隔离）: {0}", ex.Message);
        }

        return result;
    }

    private async Task RecordToolUsageAsync(string toolName, bool success, long durationMs, Dictionary<string, object>? additionalMetadata)
    {
        try
        {
            if (_serviceProvider == null) return;

            var tool = GetTool(toolName);
            if (tool == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService == null) return;

            var metadata = new Dictionary<string, object>
            {
                ["success"] = success,
                ["toolName"] = toolName
            };

            if (additionalMetadata != null)
            {
                foreach (var kvp in additionalMetadata)
                {
                    metadata[kvp.Key] = kvp.Value;
                }
            }

            await usageStatsService.RecordUsageAsync(
                tool.PluginId,
                tool.Id,
                "tool_call",
                durationMs,
                metadata);

            XTrace.Log.Debug("[ToolRegistry] 工具使用统计记录成功: {0}", toolName);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ToolRegistry] 记录工具使用统计失败: {0}", ex.Message);
        }
    }
}
