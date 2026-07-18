using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.AIAgent.Models;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Services;

public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, IToolFunctionExtension> _tools = new();
    private readonly IServiceProvider? _serviceProvider;

    public ToolRegistry(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
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

    public async Task<string> ExecuteToolAsync(string toolName, string parameters)
    {
        var result = await ExecuteToolWithResultAsync(toolName, parameters);
        return result.Result;
    }

    public async Task<ToolExecutionResult> ExecuteToolWithResultAsync(string toolName, string parameters, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ToolExecutionResult
        {
            ToolName = toolName
        };

        try
        {
            XTrace.Log.Info("[ToolRegistry] ========================================");
            XTrace.Log.Info("[ToolRegistry] 开始执行工具: {0}", toolName);
            XTrace.Log.Debug("[ToolRegistry] 工具参数: {0}", parameters);

            var tool = GetTool(toolName);
            if (tool == null)
            {
                var errorMsg = $"工具 '{toolName}' 不存在";
                XTrace.Log.Warn("[ToolRegistry] {0}", errorMsg);
                result.Success = false;
                result.ErrorMessage = errorMsg;
                var response = ToolCallResponse.Fail(ToolErrorCode.ToolNotFound, errorMsg, "请检查工具名称是否正确，或确认插件是否已启用");
                result.Result = JsonSerializer.Serialize(response);
                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                await RecordToolUsageAsync(toolName, false, result.DurationMs, new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.ToolNotFound });
                return result;
            }

            var validationResult = ValidateParameters(toolName, parameters);
            if (!validationResult.IsValid)
            {
                var errorMsg = $"参数验证失败: {string.Join("; ", validationResult.Errors)}";
                XTrace.Log.Warn("[ToolRegistry] {0}", errorMsg);
                result.Success = false;
                result.ErrorMessage = errorMsg;
                var response = ToolCallResponse.Fail(ToolErrorCode.InvalidParameters, errorMsg, "请检查参数是否符合要求");
                result.Result = JsonSerializer.Serialize(response);
                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                await RecordToolUsageAsync(toolName, false, result.DurationMs, new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.InvalidParameters, ["errors"] = validationResult.Errors });
                return result;
            }

            var toolResult = await tool.ExecuteAsync(parameters);

            result.Success = true;
            result.Result = toolResult;

            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;

            await RecordToolUsageAsync(toolName, true, result.DurationMs, null);

            XTrace.Log.Info("[ToolRegistry] 工具执行成功: {0} (耗时: {1}ms)", toolName, result.DurationMs);
            XTrace.Log.Debug("[ToolRegistry] 工具返回结果: {0}", toolResult);
            XTrace.Log.Info("[ToolRegistry] ========================================");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.ErrorMessage = "工具执行被取消";
            var response = ToolCallResponse.Fail(ToolErrorCode.Timeout, "工具执行被取消或超时", "请稍后重试或检查工具是否正常");
            result.Result = JsonSerializer.Serialize(response);

            XTrace.Log.Warn("[ToolRegistry] 工具执行被取消 [{0}]", toolName);
            await RecordToolUsageAsync(toolName, false, result.DurationMs, new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.Timeout });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            var response = ToolCallResponse.Fail(ToolErrorCode.ExecutionFailed, ex.Message, "请检查工具配置或联系管理员");
            result.Result = JsonSerializer.Serialize(response);

            XTrace.Log.Error("[ToolRegistry] 工具执行异常 [{0}]: {1}", toolName, ex.Message);
            XTrace.Log.Debug("[ToolRegistry] 异常堆栈: {0}", ex.StackTrace);
            XTrace.Log.Info("[ToolRegistry] ========================================");

            await RecordToolUsageAsync(toolName, false, result.DurationMs, new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.ExecutionFailed, ["errorMessage"] = ex.Message });
        }

        return result;
    }

    public async Task<ToolExecutionResult> ExecuteToolWithTimeoutAsync(string toolName, string parameters, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        try
        {
            return await ExecuteToolWithResultAsync(toolName, parameters, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ToolExecutionResult
            {
                ToolName = toolName,
                Success = false,
                ErrorMessage = $"工具执行超时（{timeoutSeconds}秒）"
            };

            var response = ToolCallResponse.Fail(ToolErrorCode.Timeout, $"工具执行超时，已在{timeoutSeconds}秒后终止", "请优化工具执行逻辑或增加超时时间配置");
            result.Result = JsonSerializer.Serialize(response);
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;

            XTrace.Log.Warn("[ToolRegistry] 工具执行超时 [{0}]: {1}秒", toolName, timeoutSeconds);
            await RecordToolUsageAsync(toolName, false, result.DurationMs, new Dictionary<string, object> { ["errorCode"] = ToolErrorCode.Timeout, ["timeoutSeconds"] = timeoutSeconds });

            return result;
        }
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
