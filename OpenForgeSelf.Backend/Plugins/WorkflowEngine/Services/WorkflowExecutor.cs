using System.Text.Json;
using System.Text.RegularExpressions;
using OpenForgeSelf.Backend.Models.UsageStats;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using CoreWorkflowExecutionStatus = OpenForgeSelf.Backend.Models.UsageStats.WorkflowExecutionStatus;
using WorkflowDefEntity = OpenForgeSelf.Backend.Plugins.WorkflowEngine.Entities.WorkflowDefinition;
using WorkflowExecEntity = OpenForgeSelf.Backend.Plugins.WorkflowEngine.Entities.WorkflowExecution;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;

public class WorkflowExecutor : IWorkflowExecutor
{
    private readonly IServiceProvider? _serviceProvider;

    private static readonly Dictionary<long, WorkflowExecution> _runningExecutions = new();
    private static readonly Dictionary<long, CancellationTokenSource> _cancellationTokenSources = new();
    private static readonly object _lock = new();

    public WorkflowExecutor(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<WorkflowExecution> ExecuteAsync(long workflowId, Dictionary<string, object?>? inputVariables = null, string? triggeredBy = null)
    {
        XTrace.Log.Info("[WorkflowExecutor] 开始执行工作流，workflowId: {0}", workflowId);

        var workflow = await GetWorkflowDefinitionAsync(workflowId);
        if (workflow == null)
        {
            throw new Exception($"工作流不存在: {workflowId}");
        }

        var execution = new WorkflowExecution
        {
            WorkflowId = workflowId,
            WorkflowName = workflow.Name,
            Status = WorkflowStatus.Running,
            StartTime = DateTime.Now,
            TriggeredBy = triggeredBy,
            Variables = new Dictionary<string, object?>(),
            StepResults = new Dictionary<string, object?>(),
            Logs = new List<ExecutionLogEntry>()
        };

        foreach (var variable in workflow.Variables)
        {
            execution.Variables[variable.Name] = variable.DefaultValue;
        }

        if (inputVariables != null)
        {
            foreach (var kvp in inputVariables)
            {
                execution.Variables[kvp.Key] = kvp.Value;
            }
        }

        execution = await SaveExecutionAsync(execution);

        await RecordWorkflowStartAsync(execution, workflow, inputVariables);

        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            _runningExecutions[execution.Id] = execution;
            _cancellationTokenSources[execution.Id] = cts;
        }

        AddLog(execution, null, null, "Info", $"工作流开始执行，共 {workflow.Steps.Count} 个步骤");

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteWorkflowInternalAsync(execution, workflow, cts.Token);
            }
            catch (OperationCanceledException)
            {
                execution.Status = WorkflowStatus.Cancelled;
                execution.ErrorMessage = "工作流已取消";
                AddLog(execution, null, null, "Warn", "工作流已被取消");
            }
            catch (Exception ex)
            {
                execution.Status = WorkflowStatus.Failed;
                execution.ErrorMessage = ex.Message;
                AddLog(execution, null, null, "Error", $"工作流执行失败: {ex.Message}");
                XTrace.Log.Error("[WorkflowExecutor] 工作流执行失败: {0}", ex.Message);
            }
            finally
            {
                execution.EndTime = DateTime.Now;
                execution.Progress = execution.Status == WorkflowStatus.Completed ? 100 : execution.Progress;

                await SaveExecutionAsync(execution);

                await RecordWorkflowEndAsync(execution, workflow.Steps.Count);

                lock (_lock)
                {
                    _runningExecutions.Remove(execution.Id);
                    _cancellationTokenSources.Remove(execution.Id);
                }

                XTrace.Log.Info("[WorkflowExecutor] 工作流执行结束，executionId: {0}, status: {1}", execution.Id, execution.Status);
            }
        }, cts.Token);

        return execution;
    }

    private async Task ExecuteWorkflowInternalAsync(WorkflowExecution execution, WorkflowDefinition workflow, CancellationToken cancellationToken)
    {
        var steps = workflow.Steps;
        if (steps.Count == 0)
        {
            execution.Status = WorkflowStatus.Completed;
            AddLog(execution, null, null, "Info", "工作流没有步骤，直接完成");
            return;
        }

        var startStepId = workflow.StartStepId ?? steps.First().Id;
        var currentStepId = startStepId;
        var executedSteps = 0;
        var totalSteps = steps.Count;

        while (!string.IsNullOrEmpty(currentStepId))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = steps.FirstOrDefault(s => s.Id == currentStepId);
            if (step == null)
            {
                AddLog(execution, currentStepId, null, "Warn", $"步骤不存在: {currentStepId}，跳过");
                break;
            }

            execution.CurrentStepId = currentStepId;
            execution.Progress = (double)executedSteps / totalSteps * 100;

            AddLog(execution, step.Id, step.Name, "Info", $"开始执行步骤: {step.Name} ({step.Type})");

            var stepSuccess = false;
            object? stepResult = null;
            var retryCount = 0;
            var maxRetries = step.ErrorHandling?.MaxRetries ?? 0;
            var retryDelayMs = step.ErrorHandling?.RetryDelayMs ?? 1000;
            var currentStep = step;

            while (retryCount <= maxRetries && !stepSuccess)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    stepResult = await ExecuteStepAsync(currentStep, execution, cancellationToken);
                    stepSuccess = true;

                    execution.StepResults![step.Id] = stepResult;

                    AddLog(execution, step.Id, step.Name, "Info", $"步骤执行成功");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    AddLog(execution, step.Id, step.Name, "Error",
                        $"步骤执行失败 (尝试 {retryCount}/{maxRetries + 1}): {ex.Message}");

                    if (retryCount <= maxRetries)
                    {
                        var aiAdvice = await GetAIAdviceAsync(currentStep, ex.Message, retryCount, execution);
                        if (aiAdvice != null)
                        {
                            if (!aiAdvice.ShouldRetry)
                            {
                                AddLog(execution, step.Id, step.Name, "Warn", $"AI建议不再重试: {aiAdvice.Reason}");
                                if (step.ErrorHandling?.ContinueOnError == true)
                                {
                                    stepSuccess = true;
                                    break;
                                }
                                throw new Exception($"步骤 '{step.Name}' 执行失败（AI判定不可重试）: {ex.Message}", ex);
                            }

                            if (!string.IsNullOrEmpty(aiAdvice.AdjustedParameters) && currentStep.Type == WorkflowStepType.ToolCall)
                            {
                                AddLog(execution, step.Id, step.Name, "Info", $"AI建议调整参数，原因: {aiAdvice.Reason}");
                                currentStep = new WorkflowStep
                                {
                                    Id = currentStep.Id,
                                    Name = currentStep.Name,
                                    Description = currentStep.Description,
                                    Type = currentStep.Type,
                                    ToolName = currentStep.ToolName,
                                    ParametersJson = aiAdvice.AdjustedParameters,
                                    ConditionExpression = currentStep.ConditionExpression,
                                    LoopConfig = currentStep.LoopConfig,
                                    ChildrenSteps = currentStep.ChildrenSteps,
                                    NextStepId = currentStep.NextStepId,
                                    ErrorHandling = currentStep.ErrorHandling
                                };
                            }

                            if (!string.IsNullOrEmpty(aiAdvice.AlternativeToolName) && currentStep.Type == WorkflowStepType.ToolCall)
                            {
                                AddLog(execution, step.Id, step.Name, "Info", $"AI建议更换工具为: {aiAdvice.AlternativeToolName}");
                                currentStep = new WorkflowStep
                                {
                                    Id = currentStep.Id,
                                    Name = currentStep.Name,
                                    Description = currentStep.Description,
                                    Type = currentStep.Type,
                                    ToolName = aiAdvice.AlternativeToolName,
                                    ParametersJson = currentStep.ParametersJson,
                                    ConditionExpression = currentStep.ConditionExpression,
                                    LoopConfig = currentStep.LoopConfig,
                                    ChildrenSteps = currentStep.ChildrenSteps,
                                    NextStepId = currentStep.NextStepId,
                                    ErrorHandling = currentStep.ErrorHandling
                                };
                            }

                            if (aiAdvice.DelayMs > 0)
                            {
                                retryDelayMs = aiAdvice.DelayMs;
                            }
                        }

                        AddLog(execution, step.Id, step.Name, "Info", $"等待 {retryDelayMs}ms 后重试...");
                        await Task.Delay(retryDelayMs, cancellationToken);
                    }
                    else if (step.ErrorHandling?.ContinueOnError == true)
                    {
                        AddLog(execution, step.Id, step.Name, "Warn", "达到最大重试次数，继续执行下一步");
                        stepSuccess = true;
                    }
                    else
                    {
                        throw new Exception($"步骤 '{step.Name}' 执行失败: {ex.Message}", ex);
                    }
                }
            }

            executedSteps++;

            if (step.Type == WorkflowStepType.Condition)
            {
                var conditionResult = stepResult as bool? ?? false;
                currentStepId = conditionResult ? step.NextStepId : GetNextStepId(steps, step.Id);
                AddLog(execution, step.Id, step.Name, "Info",
                    $"条件判断结果: {conditionResult}，下一步: {currentStepId ?? "(结束)"}");
            }
            else
            {
                currentStepId = step.NextStepId ?? GetNextStepId(steps, step.Id);
            }
        }

        execution.Status = WorkflowStatus.Completed;
        execution.Progress = 100;
        execution.ResultsJson = JsonSerializer.Serialize(execution.StepResults);

        AddLog(execution, null, null, "Info", "工作流执行完成");
    }

    private async Task<RetryAdvice?> GetAIAdviceAsync(WorkflowStep step, string errorMessage, int retryCount, WorkflowExecution execution)
    {
        if (_serviceProvider == null) return null;

        try
        {
            var aiAdvisor = _serviceProvider.GetService(typeof(IWorkflowAIAdvisor)) as IWorkflowAIAdvisor;
            if (aiAdvisor == null) return null;

            XTrace.Log.Debug("[WorkflowExecutor] 获取AI重试建议，步骤: {0}", step.Name);

            var context = new Dictionary<string, object?>
            {
                ["stepId"] = step.Id,
                ["stepName"] = step.Name,
                ["stepType"] = step.Type,
                ["retryCount"] = retryCount,
                ["variables"] = execution.Variables
            };

            var advice = await aiAdvisor.GetRetryAdviceAsync(step, errorMessage, retryCount, context);
            return advice;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[WorkflowExecutor] 获取AI建议失败: {0}", ex.Message);
            return null;
        }
    }

    private async Task<object?> ExecuteStepAsync(WorkflowStep step, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        return step.Type switch
        {
            WorkflowStepType.ToolCall => await ExecuteToolCallStepAsync(step, execution, cancellationToken),
            WorkflowStepType.Condition => ExecuteConditionStep(step, execution),
            WorkflowStepType.Loop => await ExecuteLoopStepAsync(step, execution, cancellationToken),
            WorkflowStepType.Parallel => await ExecuteParallelStepAsync(step, execution, cancellationToken),
            WorkflowStepType.Wait => await ExecuteWaitStepAsync(step, execution, cancellationToken),
            WorkflowStepType.Http => await ExecuteHttpStepAsync(step, execution, cancellationToken),
            WorkflowStepType.Script => await ExecuteScriptStepAsync(step, execution, cancellationToken),
            _ => throw new NotSupportedException($"不支持的步骤类型: {step.Type}")
        };
    }

    private async Task<object?> ExecuteToolCallStepAsync(WorkflowStep step, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        var toolName = step.ToolName ?? string.Empty;
        var parametersJson = step.ParametersJson ?? "{}";

        parametersJson = ReplaceVariables(parametersJson, execution);

        AddLog(execution, step.Id, step.Name, "Debug",
            $"调用工具: {toolName}, 参数: {parametersJson}");

        var result = new
        {
            toolName,
            parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson),
            executed = true,
            timestamp = DateTime.Now
        };

        await Task.Delay(100, cancellationToken);

        return result;
    }

    private bool ExecuteConditionStep(WorkflowStep step, WorkflowExecution execution)
    {
        var expression = step.ConditionExpression ?? string.Empty;
        expression = ReplaceVariables(expression, execution);

        AddLog(execution, step.Id, step.Name, "Debug", $"条件表达式: {expression}");

        var result = EvaluateSimpleExpression(expression);

        return result;
    }

    private async Task<object?> ExecuteLoopStepAsync(WorkflowStep step, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        var loopConfig = step.LoopConfig ?? new LoopConfig();
        var childrenSteps = step.ChildrenSteps ?? new List<WorkflowStep>();
        var results = new List<object?>();
        var iteration = 0;

        if (loopConfig.LoopType == "forEach" && !string.IsNullOrEmpty(loopConfig.ItemsExpression))
        {
            var itemsExpression = ReplaceVariables(loopConfig.ItemsExpression!, execution);
            var items = ParseItemsExpression(itemsExpression);
            var itemVarName = loopConfig.ItemVariableName ?? "item";

            AddLog(execution, step.Id, step.Name, "Info",
                $"开始 for-each 循环，共 {items.Count} 项");

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (iteration >= loopConfig.MaxIterations)
                {
                    AddLog(execution, step.Id, step.Name, "Warn",
                        $"达到最大迭代次数 {loopConfig.MaxIterations}，停止循环");
                    break;
                }

                execution.Variables![itemVarName] = item;
                iteration++;

                AddLog(execution, step.Id, step.Name, "Debug",
                    $"循环迭代 {iteration}/{items.Count}, {itemVarName} = {item}");

                var iterationResult = await ExecuteChildStepsAsync(childrenSteps, execution, cancellationToken);
                results.Add(iterationResult);
            }
        }
        else if (loopConfig.LoopType == "while" && !string.IsNullOrEmpty(loopConfig.ConditionExpression))
        {
            AddLog(execution, step.Id, step.Name, "Info", "开始 while 循环");

            while (iteration < loopConfig.MaxIterations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var conditionExpr = ReplaceVariables(loopConfig.ConditionExpression!, execution);
                var conditionResult = EvaluateSimpleExpression(conditionExpr);

                if (!conditionResult)
                {
                    AddLog(execution, step.Id, step.Name, "Info",
                        $"循环条件不满足，退出循环。共执行 {iteration} 次迭代");
                    break;
                }

                iteration++;
                AddLog(execution, step.Id, step.Name, "Debug", $"循环迭代 {iteration}");

                var iterationResult = await ExecuteChildStepsAsync(childrenSteps, execution, cancellationToken);
                results.Add(iterationResult);
            }
        }

        AddLog(execution, step.Id, step.Name, "Info", $"循环结束，共执行 {iteration} 次迭代");

        return new { iteration, results };
    }

    private async Task<object?> ExecuteChildStepsAsync(List<WorkflowStep> steps, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        if (steps.Count == 0) return null;

        object? lastResult = null;
        var currentStepId = steps.First().Id;

        while (!string.IsNullOrEmpty(currentStepId))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = steps.FirstOrDefault(s => s.Id == currentStepId);
            if (step == null) break;

            lastResult = await ExecuteStepAsync(step, execution, cancellationToken);

            currentStepId = step.NextStepId ?? GetNextStepId(steps, step.Id);
        }

        return lastResult;
    }

    private async Task<object?> ExecuteParallelStepAsync(WorkflowStep step, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        var childrenSteps = step.ChildrenSteps ?? new List<WorkflowStep>();

        AddLog(execution, step.Id, step.Name, "Info",
            $"开始并行执行 {childrenSteps.Count} 个子步骤");

        var tasks = childrenSteps.Select(async childStep =>
        {
            try
            {
                return await ExecuteStepAsync(childStep, execution, cancellationToken);
            }
            catch (Exception ex)
            {
                AddLog(execution, step.Id, step.Name, "Error",
                    $"并行子步骤 '{childStep.Name}' 执行失败: {ex.Message}");
                return (object?)null;
            }
        });

        var results = await Task.WhenAll(tasks);

        AddLog(execution, step.Id, step.Name, "Info", "并行执行完成");

        return results;
    }

    private async Task<object?> ExecuteWaitStepAsync(WorkflowStep step, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        var waitMs = 1000;
        if (!string.IsNullOrEmpty(step.ParametersJson))
        {
            try
            {
                var paramJson = ReplaceVariables(step.ParametersJson, execution);
                using var doc = JsonDocument.Parse(paramJson);
                if (doc.RootElement.TryGetProperty("waitMs", out var waitMsProp))
                {
                    waitMs = waitMsProp.GetInt32();
                }
                else if (doc.RootElement.TryGetProperty("waitSeconds", out var waitSecProp))
                {
                    waitMs = waitSecProp.GetInt32() * 1000;
                }
            }
            catch
            {
            }
        }

        AddLog(execution, step.Id, step.Name, "Info", $"等待 {waitMs}ms");

        await Task.Delay(waitMs, cancellationToken);

        AddLog(execution, step.Id, step.Name, "Info", "等待完成");

        return new { waitedMs = waitMs };
    }

    private async Task<object?> ExecuteHttpStepAsync(WorkflowStep step, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        var url = string.Empty;
        var method = "GET";
        var headers = new Dictionary<string, string>();
        var body = string.Empty;

        if (!string.IsNullOrEmpty(step.ParametersJson))
        {
            try
            {
                var paramJson = ReplaceVariables(step.ParametersJson, execution);
                using var doc = JsonDocument.Parse(paramJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("url", out var urlProp))
                    url = urlProp.GetString() ?? string.Empty;
                if (root.TryGetProperty("method", out var methodProp))
                    method = methodProp.GetString() ?? "GET";
                if (root.TryGetProperty("body", out var bodyProp))
                    body = bodyProp.GetRawText();
            }
            catch
            {
            }
        }

        AddLog(execution, step.Id, step.Name, "Info",
            $"执行 HTTP 请求: {method} {url}");

        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (!string.IsNullOrEmpty(body) && method != "GET")
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        AddLog(execution, step.Id, step.Name, "Info",
            $"HTTP 请求完成，状态码: {(int)response.StatusCode}");

        return new
        {
            statusCode = (int)response.StatusCode,
            reasonPhrase = response.ReasonPhrase,
            headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
            body = responseBody
        };
    }

    private async Task<object?> ExecuteScriptStepAsync(WorkflowStep step, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        var scriptExecutor = _serviceProvider?.GetService<IScriptExecutor>();
        var scriptService = _serviceProvider?.GetService<IScriptService>();

        if (scriptExecutor == null)
        {
            throw new InvalidOperationException("脚本执行器未注册，无法执行脚本步骤");
        }

        var successExitCodes = step.SuccessExitCodes ?? new List<int> { 0 };
        var timeoutSeconds = step.TimeoutSeconds ?? 300;

        AddLog(execution, step.Id, step.Name, "Info",
            $"准备执行脚本步骤，模式: {(step.ScriptId.HasValue ? "引用脚本" : "嵌入代码")}");

        var parameters = ResolveParameterMappings(step.ParameterMappings, execution);

        ScriptExecution scriptExecution;

        if (step.ScriptId.HasValue)
        {
            var scriptId = step.ScriptId.Value;
            AddLog(execution, step.Id, step.Name, "Debug",
                $"执行脚本 ID: {scriptId}");

            scriptExecution = await scriptExecutor.ExecuteAsync(scriptId, parameters, cancellationToken);

            if (scriptService != null)
            {
                _ = scriptService.IncrementUsageAsync(scriptId);
            }
        }
        else if (!string.IsNullOrEmpty(step.ScriptCode) && step.ScriptLanguage.HasValue)
        {
            var language = step.ScriptLanguage.Value;
            AddLog(execution, step.Id, step.Name, "Debug",
                $"执行嵌入脚本，语言: {language}");

            scriptExecution = await scriptExecutor.ExecuteCodeAsync(
                step.ScriptCode,
                language,
                parameters,
                step.WorkingDirectory,
                cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("脚本步骤配置无效：必须指定 ScriptId 或 ScriptCode + ScriptLanguage");
        }

        AddLog(execution, step.Id, step.Name, "Debug",
            $"脚本执行完成，退出码: {scriptExecution.ExitCode}, 状态: {scriptExecution.Status}");

        if (!string.IsNullOrEmpty(step.OutputVariable) && execution.Variables != null)
        {
            var output = scriptExecution.Output ?? string.Empty;
            object? parsedOutput = output;

            try
            {
                parsedOutput = JsonSerializer.Deserialize<object>(output);
            }
            catch
            {
            }

            execution.Variables[step.OutputVariable] = parsedOutput;
            AddLog(execution, step.Id, step.Name, "Debug",
                $"脚本输出已保存到变量: {step.OutputVariable}");
        }

        var exitCode = scriptExecution.ExitCode ?? -1;
        var isSuccess = successExitCodes.Contains(exitCode);

        if (!isSuccess)
        {
            var errorMsg = $"脚本执行失败，退出码: {exitCode}，错误输出: {scriptExecution.ErrorOutput}";
            AddLog(execution, step.Id, step.Name, "Error", errorMsg);
            throw new Exception(errorMsg);
        }

        AddLog(execution, step.Id, step.Name, "Info", "脚本步骤执行成功");

        return new
        {
            scriptId = step.ScriptId,
            scriptName = scriptExecution.ScriptName,
            exitCode = scriptExecution.ExitCode,
            status = scriptExecution.Status,
            output = scriptExecution.Output,
            errorOutput = scriptExecution.ErrorOutput,
            durationMs = scriptExecution.DurationMs
        };
    }

    private Dictionary<string, object?> ResolveParameterMappings(Dictionary<string, string>? parameterMappings, WorkflowExecution execution)
    {
        var parameters = new Dictionary<string, object?>();

        if (parameterMappings == null || parameterMappings.Count == 0)
        {
            return parameters;
        }

        foreach (var kvp in parameterMappings)
        {
            var paramName = kvp.Key;
            var valueExpression = kvp.Value;

            if (string.IsNullOrEmpty(valueExpression))
            {
                parameters[paramName] = null;
                continue;
            }

            var resolvedValue = ReplaceVariables(valueExpression, execution);

            object? typedValue = resolvedValue;
            if (int.TryParse(resolvedValue, out var intValue))
            {
                typedValue = intValue;
            }
            else if (double.TryParse(resolvedValue, out var doubleValue))
            {
                typedValue = doubleValue;
            }
            else if (bool.TryParse(resolvedValue, out var boolValue))
            {
                typedValue = boolValue;
            }

            parameters[paramName] = typedValue;
        }

        return parameters;
    }

    private string ReplaceVariables(string input, WorkflowExecution execution)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = input;

        result = Regex.Replace(result, @"{{\s*step\.(\w+)\.output\s*}}", match =>
        {
            var stepId = match.Groups[1].Value;
            if (execution.StepResults != null && execution.StepResults.TryGetValue(stepId, out var stepResult))
            {
                return stepResult?.ToString() ?? string.Empty;
            }
            return match.Value;
        });

        result = Regex.Replace(result, @"{{\s*var\.(\w+)\s*}}", match =>
        {
            var varName = match.Groups[1].Value;
            if (execution.Variables != null && execution.Variables.TryGetValue(varName, out var varValue))
            {
                return varValue?.ToString() ?? string.Empty;
            }
            return match.Value;
        });

        result = Regex.Replace(result, @"{{\s*(\w+)\s*}}", match =>
        {
            var varName = match.Groups[1].Value;
            if (execution.Variables != null && execution.Variables.TryGetValue(varName, out var varValue))
            {
                return varValue?.ToString() ?? string.Empty;
            }
            return match.Value;
        });

        return result;
    }

    private bool EvaluateSimpleExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return false;

        expression = expression.Trim();

        if (bool.TryParse(expression, out var boolResult))
            return boolResult;

        var eqMatch = System.Text.RegularExpressions.Regex.Match(expression,
            @"^(.+?)\s*==\s*(.+)$");
        if (eqMatch.Success)
        {
            var left = eqMatch.Groups[1].Value.Trim().Trim('\'', '"');
            var right = eqMatch.Groups[2].Value.Trim().Trim('\'', '"');
            return left == right;
        }

        var neqMatch = System.Text.RegularExpressions.Regex.Match(expression,
            @"^(.+?)\s*!=\s*(.+)$");
        if (neqMatch.Success)
        {
            var left = neqMatch.Groups[1].Value.Trim().Trim('\'', '"');
            var right = neqMatch.Groups[2].Value.Trim().Trim('\'', '"');
            return left != right;
        }

        var gtMatch = System.Text.RegularExpressions.Regex.Match(expression,
            @"^(.+?)\s*>\s*(.+)$");
        if (gtMatch.Success)
        {
            if (double.TryParse(gtMatch.Groups[1].Value.Trim(), out var left) &&
                double.TryParse(gtMatch.Groups[2].Value.Trim(), out var right))
                return left > right;
        }

        var ltMatch = System.Text.RegularExpressions.Regex.Match(expression,
            @"^(.+?)\s*<\s*(.+)$");
        if (ltMatch.Success)
        {
            if (double.TryParse(ltMatch.Groups[1].Value.Trim(), out var left) &&
                double.TryParse(ltMatch.Groups[2].Value.Trim(), out var right))
                return left < right;
        }

        if (expression.StartsWith("!", StringComparison.Ordinal))
        {
            return !EvaluateSimpleExpression(expression.Substring(1).Trim());
        }

        return false;
    }

    private List<object> ParseItemsExpression(string expression)
    {
        var items = new List<object>();

        try
        {
            using var doc = JsonDocument.Parse(expression);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    items.Add(item.GetString() ?? item.GetRawText());
                }
            }
        }
        catch
        {
            if (!string.IsNullOrEmpty(expression))
            {
                items.Add(expression);
            }
        }

        return items;
    }

    private string? GetNextStepId(List<WorkflowStep> steps, string currentStepId)
    {
        var index = steps.FindIndex(s => s.Id == currentStepId);
        if (index >= 0 && index < steps.Count - 1)
        {
            return steps[index + 1].Id;
        }
        return null;
    }

    private void AddLog(WorkflowExecution execution, string? stepId, string? stepName, string logLevel, string message)
    {
        execution.Logs.Add(new ExecutionLogEntry
        {
            Timestamp = DateTime.Now,
            StepId = stepId,
            StepName = stepName,
            LogLevel = logLevel,
            Message = message
        });

        if (logLevel == "Error")
        {
            XTrace.Log.Error("[WorkflowExecutor] {0}", message);
        }
        else if (logLevel == "Warn")
        {
            XTrace.Log.Warn("[WorkflowExecutor] {0}", message);
        }
        else
        {
            XTrace.Log.Info("[WorkflowExecutor] {0}", message);
        }
    }

    private Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(long workflowId)
    {
        var entity = WorkflowDefEntity.FindById(workflowId);
        if (entity == null) return Task.FromResult<WorkflowDefinition?>(null);

        return Task.FromResult<WorkflowDefinition?>(EntityToDefinition(entity));
    }

    private Task<WorkflowExecution> SaveExecutionAsync(WorkflowExecution execution)
    {
        if (execution.Id == 0)
        {
            var entity = ExecutionToEntity(execution);
            entity.Insert();
            execution.Id = entity.Id;
        }
        else
        {
            var entity = WorkflowExecEntity.FindById(execution.Id);
            if (entity != null)
            {
                entity.Status = (int)execution.Status;
                entity.CurrentStepId = execution.CurrentStepId;
                entity.EndTime = execution.EndTime ?? DateTime.MinValue;
                entity.ErrorMessage = execution.ErrorMessage;
                entity.Progress = execution.Progress;
                entity.LogsJson = JsonSerializer.Serialize(execution.Logs);
                entity.ResultsJson = execution.ResultsJson;
                entity.VariablesJson = execution.Variables != null ? JsonSerializer.Serialize(execution.Variables) : null;
                entity.StepResultsJson = execution.StepResults != null ? JsonSerializer.Serialize(execution.StepResults) : null;

                entity.Update();
            }
        }

        return Task.FromResult(execution);
    }

    public Task PauseAsync(long executionId)
    {
        XTrace.Log.Info("[WorkflowExecutor] 暂停工作流执行，executionId: {0}", executionId);

        lock (_lock)
        {
            if (_runningExecutions.TryGetValue(executionId, out var execution))
            {
                execution.Status = WorkflowStatus.Paused;
            }
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(long executionId)
    {
        XTrace.Log.Info("[WorkflowExecutor] 继续工作流执行，executionId: {0}", executionId);

        lock (_lock)
        {
            if (_runningExecutions.TryGetValue(executionId, out var execution))
            {
                execution.Status = WorkflowStatus.Running;
            }
        }

        return Task.CompletedTask;
    }

    public Task CancelAsync(long executionId)
    {
        XTrace.Log.Info("[WorkflowExecutor] 取消工作流执行，executionId: {0}", executionId);

        lock (_lock)
        {
            if (_cancellationTokenSources.TryGetValue(executionId, out var cts))
            {
                cts.Cancel();
            }
        }

        return Task.CompletedTask;
    }

    public Task<WorkflowExecution?> GetExecutionAsync(long executionId)
    {
        lock (_lock)
        {
            if (_runningExecutions.TryGetValue(executionId, out var execution))
            {
                return Task.FromResult<WorkflowExecution?>(execution);
            }
        }

        var entity = WorkflowExecEntity.FindById(executionId);
        if (entity != null)
        {
            return Task.FromResult<WorkflowExecution?>(EntityToExecution(entity));
        }

        return Task.FromResult<WorkflowExecution?>(null);
    }

    private static WorkflowDefinition EntityToDefinition(WorkflowDefEntity entity)
    {
        var steps = !string.IsNullOrEmpty(entity.StepsJson)
            ? JsonSerializer.Deserialize<List<WorkflowStep>>(entity.StepsJson) ?? new List<WorkflowStep>()
            : new List<WorkflowStep>();

        var variables = !string.IsNullOrEmpty(entity.VariablesJson)
            ? JsonSerializer.Deserialize<List<WorkflowVariable>>(entity.VariablesJson) ?? new List<WorkflowVariable>()
            : new List<WorkflowVariable>();

        return new WorkflowDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            Icon = entity.Icon,
            Steps = steps,
            Variables = variables,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsFavorite = entity.IsFavorite,
            UsageCount = entity.UsageCount,
            Status = (WorkflowStatus)entity.Status,
            StartStepId = entity.StartStepId,
            Metadata = !string.IsNullOrEmpty(entity.MetadataJson)
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.MetadataJson)
                : null
        };
    }

    private static WorkflowExecEntity ExecutionToEntity(WorkflowExecution execution)
    {
        return new WorkflowExecEntity
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.WorkflowName,
            Status = (int)execution.Status,
            StartTime = execution.StartTime,
            EndTime = execution.EndTime ?? DateTime.MinValue,
            CurrentStepId = execution.CurrentStepId,
            LogsJson = JsonSerializer.Serialize(execution.Logs),
            ResultsJson = execution.ResultsJson,
            ErrorMessage = execution.ErrorMessage,
            VariablesJson = execution.Variables != null ? JsonSerializer.Serialize(execution.Variables) : null,
            StepResultsJson = execution.StepResults != null ? JsonSerializer.Serialize(execution.StepResults) : null,
            Progress = execution.Progress,
            TriggeredBy = execution.TriggeredBy
        };
    }

    private static WorkflowExecution EntityToExecution(WorkflowExecEntity entity)
    {
        return new WorkflowExecution
        {
            Id = entity.Id,
            WorkflowId = entity.WorkflowId,
            WorkflowName = entity.WorkflowName,
            Status = (WorkflowStatus)entity.Status,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            CurrentStepId = entity.CurrentStepId,
            Logs = !string.IsNullOrEmpty(entity.LogsJson)
                ? JsonSerializer.Deserialize<List<ExecutionLogEntry>>(entity.LogsJson) ?? new List<ExecutionLogEntry>()
                : new List<ExecutionLogEntry>(),
            ResultsJson = entity.ResultsJson,
            ErrorMessage = entity.ErrorMessage,
            Variables = !string.IsNullOrEmpty(entity.VariablesJson)
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.VariablesJson)
                : null,
            StepResults = !string.IsNullOrEmpty(entity.StepResultsJson)
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.StepResultsJson)
                : null,
            Progress = entity.Progress,
            TriggeredBy = entity.TriggeredBy
        };
    }

    private async Task RecordWorkflowStartAsync(WorkflowExecution execution, WorkflowDefinition workflow, Dictionary<string, object?>? inputVariables)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var workflowUsageService = scope.ServiceProvider.GetService<IWorkflowUsageService>();
            if (workflowUsageService == null) return;

            var request = new RecordWorkflowExecutionRequest
            {
                WorkflowId = execution.WorkflowId,
                WorkflowName = execution.WorkflowName ?? string.Empty,
                ExecutionId = execution.Id,
                StartTime = execution.StartTime,
                Status = CoreWorkflowExecutionStatus.Success,
                StepCount = workflow.Steps.Count,
                InputVariables = inputVariables,
                TriggeredBy = execution.TriggeredBy
            };

            await workflowUsageService.RecordWorkflowExecutionAsync(request);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[WorkflowExecutor] 记录工作流开始失败: {0}", ex.Message);
        }
    }

    private async Task RecordWorkflowEndAsync(WorkflowExecution execution, int stepCount)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var workflowUsageService = scope.ServiceProvider.GetService<IWorkflowUsageService>();
            if (workflowUsageService == null) return;

            var status = execution.Status switch
            {
                WorkflowStatus.Completed => CoreWorkflowExecutionStatus.Success,
                WorkflowStatus.Failed => CoreWorkflowExecutionStatus.Failed,
                WorkflowStatus.Cancelled => CoreWorkflowExecutionStatus.Cancelled,
                _ => CoreWorkflowExecutionStatus.Failed
            };

            var endTime = execution.EndTime ?? DateTime.Now;
            var durationSeconds = (endTime - execution.StartTime).TotalSeconds;

            var toolCallCount = 0;
            if (execution.Logs != null)
            {
                toolCallCount = execution.Logs.Count(l => l.LogLevel == "Info" && l.Message != null && l.Message.Contains("调用工具"));
            }

            object? outputResult = null;
            if (!string.IsNullOrEmpty(execution.ResultsJson))
            {
                try
                {
                    outputResult = JsonSerializer.Deserialize<object>(execution.ResultsJson);
                }
                catch
                {
                    outputResult = execution.ResultsJson;
                }
            }

            await workflowUsageService.UpdateWorkflowExecutionAsync(
                execution.Id,
                status,
                endTime,
                durationSeconds,
                outputResult,
                execution.ErrorMessage,
                toolCallCount);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[WorkflowExecutor] 记录工作流结束失败: {0}", ex.Message);
        }
    }
}
