using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public interface IAgentCoordinatorService
{
    Task<CoordinatorPlan> CreatePlanAsync(string userRequest, CancellationToken cancellationToken = default);
    Task<AgentExecutionResult> ExecutePlanAsync(CoordinatorPlan plan, CancellationToken cancellationToken = default);
    Task<string> HandleRequestAsync(string userRequest, CancellationToken cancellationToken = default);
    List<AgentDefinition> GetAvailableAgents();
}

public class AgentCoordinatorService : IAgentCoordinatorService
{
    private readonly IAgentRegistryService _agentRegistry;
    private readonly IAgentExecutorService _agentExecutor;
    private readonly IAIAgentService _aiAgentService;

    public AgentCoordinatorService(
        IAgentRegistryService agentRegistry,
        IAgentExecutorService agentExecutor,
        IAIAgentService aiAgentService)
    {
        _agentRegistry = agentRegistry;
        _agentExecutor = agentExecutor;
        _aiAgentService = aiAgentService;
    }

    public async Task<CoordinatorPlan> CreatePlanAsync(string userRequest, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[AgentCoordinator] 开始创建执行计划，请求长度: {0}", userRequest.Length);

            var availableAgents = _agentRegistry.GetAllAgents();
            XTrace.Log.Debug("[AgentCoordinator] 可用 Agent 数量: {0}", availableAgents.Count);

            var isSimpleRequest = IsSimpleRequest(userRequest);
            if (isSimpleRequest)
            {
                XTrace.Log.Info("[AgentCoordinator] 检测为简单请求，使用通用 Agent 直接处理");
                return CreateSimplePlan(userRequest);
            }

            var taskType = AnalyzeTaskType(userRequest);
            var requiredCapabilities = AnalyzeRequiredCapabilities(userRequest, taskType);

            var plan = await GeneratePlanWithAIAsync(userRequest, taskType, requiredCapabilities, availableAgents, cancellationToken);

            stopwatch.Stop();
            XTrace.Log.Info("[AgentCoordinator] 执行计划创建完成，任务数: {0}，耗时: {1}ms",
                plan.Tasks.Count, stopwatch.ElapsedMilliseconds);

            return plan;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AgentCoordinator] 创建执行计划失败: {0}", ex.Message);
            return CreateFallbackPlan(userRequest);
        }
    }

    public async Task<AgentExecutionResult> ExecutePlanAsync(CoordinatorPlan plan, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[AgentCoordinator] 开始执行计划，PlanId: {0}，任务数: {1}",
                plan.PlanId, plan.Tasks.Count);

            var results = new Dictionary<string, AgentExecutionResult>();
            var completedTasks = new HashSet<string>();

            foreach (var task in plan.Tasks)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (task.ParentTaskId != null && !completedTasks.Contains(task.ParentTaskId))
                {
                    XTrace.Log.Warn("[AgentCoordinator] 父任务未完成，跳过子任务: {0}", task.TaskId);
                    continue;
                }

                XTrace.Log.Info("[AgentCoordinator] 执行任务: {0} ({1})", task.TaskId, task.Description);

                var result = await _agentExecutor.ExecuteTaskAsync(task, cancellationToken);
                results[task.TaskId] = result;

                if (result.Success)
                {
                    completedTasks.Add(task.TaskId);
                    task.Status = AgentTaskStatus.Completed;
                    task.Output = result.Output;
                }
                else
                {
                    task.Status = AgentTaskStatus.Failed;
                    task.ErrorMessage = result.ErrorMessage;
                    XTrace.Log.Error("[AgentCoordinator] 任务执行失败: {0}, 错误: {1}",
                        task.TaskId, result.ErrorMessage);
                }
            }

            var finalOutput = await AggregateResultsAsync(plan, results, cancellationToken);

            stopwatch.Stop();
            XTrace.Log.Info("[AgentCoordinator] 计划执行完成，成功任务: {0}/{1}，耗时: {2}ms",
                completedTasks.Count, plan.Tasks.Count, stopwatch.ElapsedMilliseconds);

            return new AgentExecutionResult
            {
                Success = completedTasks.Count > 0,
                AgentId = "agent.coordinator",
                AgentName = "协调者",
                TaskId = plan.PlanId,
                Output = finalOutput,
                Iterations = plan.Tasks.Count,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            XTrace.Log.Error("[AgentCoordinator] 执行计划失败: {0}", ex.Message);
            return new AgentExecutionResult
            {
                Success = false,
                AgentId = "agent.coordinator",
                AgentName = "协调者",
                TaskId = plan.PlanId,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<string> HandleRequestAsync(string userRequest, CancellationToken cancellationToken = default)
    {
        var plan = await CreatePlanAsync(userRequest, cancellationToken);
        var result = await ExecutePlanAsync(plan, cancellationToken);
        return result.Output ?? result.ErrorMessage ?? "处理失败";
    }

    public List<AgentDefinition> GetAvailableAgents()
    {
        return _agentRegistry.GetAllAgents();
    }

    private static bool IsSimpleRequest(string request)
    {
        if (request.Length < 50) return true;

        var simplePatterns = new[]
        {
            "你好", "hello", "hi", "谢谢", "再见",
            "现在几点", "今天天气", "计算",
            "是什么", "为什么", "怎么做"
        };

        var lower = request.ToLower();
        return simplePatterns.Any(p => lower.Contains(p));
    }

    private static AgentType AnalyzeTaskType(string request)
    {
        var lower = request.ToLower();

        if (lower.Contains("代码") || lower.Contains("程序") || lower.Contains("bug") ||
            lower.Contains("开发") || lower.Contains("编程") || lower.Contains("函数") ||
            lower.Contains("代码") || lower.Contains("script") || lower.Contains("code"))
            return AgentType.Programmer;

        if (lower.Contains("研究") || lower.Contains("分析") || lower.Contains("调研") ||
            lower.Contains("查一下") || lower.Contains("资料") || lower.Contains("信息"))
            return AgentType.Researcher;

        if (lower.Contains("写") || lower.Contains("文章") || lower.Contains("文案") ||
            lower.Contains("报告") || lower.Contains("写作") || lower.Contains("创作"))
            return AgentType.Writer;

        if (lower.Contains("数据") || lower.Contains("统计") || lower.Contains("报表") ||
            lower.Contains("趋势") || lower.Contains("可视化"))
            return AgentType.Analyst;

        if (lower.Contains("评估") || lower.Contains("审查") || lower.Contains("点评") ||
            lower.Contains("改进建议") || lower.Contains("优化建议"))
            return AgentType.Critic;

        return AgentType.Generalist;
    }

    private static List<string> AnalyzeRequiredCapabilities(string request, AgentType taskType)
    {
        var capabilities = new List<string>();
        var lower = request.ToLower();

        switch (taskType)
        {
            case AgentType.Researcher:
                capabilities.AddRange(new[] { "research", "information-retrieval", "analysis" });
                break;
            case AgentType.Writer:
                capabilities.AddRange(new[] { "writing", "content-creation", "editing" });
                break;
            case AgentType.Programmer:
                capabilities.AddRange(new[] { "coding", "debugging", "architecture" });
                break;
            case AgentType.Analyst:
                capabilities.AddRange(new[] { "data-analysis", "reporting", "insight-discovery" });
                break;
            case AgentType.Critic:
                capabilities.AddRange(new[] { "review", "quality-assurance", "feedback" });
                break;
            default:
                capabilities.AddRange(new[] { "general-chat", "question-answering" });
                break;
        }

        if (lower.Contains("详细") || lower.Contains("深入"))
            capabilities.Add("detailed-analysis");

        if (lower.Contains("快速") || lower.Contains("简单"))
            capabilities.Add("quick-response");

        return capabilities.Distinct().ToList();
    }

    private static CoordinatorPlan CreateSimplePlan(string userRequest)
    {
        var taskId = $"task-{Guid.NewGuid():N}";
        return new CoordinatorPlan
        {
            PlanId = $"plan-{Guid.NewGuid():N}",
            OriginalRequest = userRequest,
            Strategy = "简单请求，直接使用通用 Agent 处理",
            RequiredCapabilities = new List<string> { "general-chat" },
            Tasks = new List<AgentTask>
            {
                new()
                {
                    TaskId = taskId,
                    Description = "直接回答用户问题",
                    AssignedAgentType = AgentType.Generalist,
                    AssignedAgentId = "agent.generalist",
                    Priority = TaskPriority.Medium,
                    Input = userRequest
                }
            },
            CoordinatorReasoning = "用户请求较简单，无需复杂协作"
        };
    }

    private CoordinatorPlan CreateFallbackPlan(string userRequest)
    {
        XTrace.Log.Warn("[AgentCoordinator] 使用备用计划");
        return CreateSimplePlan(userRequest);
    }

    private async Task<CoordinatorPlan> GeneratePlanWithAIAsync(
        string userRequest,
        AgentType taskType,
        List<string> requiredCapabilities,
        List<AgentDefinition> availableAgents,
        CancellationToken cancellationToken)
    {
        try
        {
            var coordinatorAgent = _agentRegistry.GetAgent("agent.coordinator");
            if (coordinatorAgent == null)
                return CreateFallbackPlan(userRequest);

            var agentDescriptions = string.Join("\n", availableAgents.Select(a =>
                $"- {a.Id}: {a.Name} ({a.Type}) - {a.Description}\n  能力: {string.Join(", ", a.Capabilities)}"));

            var systemPrompt = $@"{coordinatorAgent.SystemPrompt}

当前可用的专业 Agent：
{agentDescriptions}

请分析用户的请求，决定是否需要分解为多个子任务，以及分配给哪些 Agent。

输出格式（严格 JSON 格式）：
{{
  ""strategy"": ""整体策略描述"",
  ""requiredCapabilities"": [""能力1"", ""能力2""],
  ""tasks"": [
    {{
      ""description"": ""任务描述"",
      ""assignedAgentType"": ""Agent类型: Coordinator/Researcher/Writer/Programmer/Analyst/Critic/Generalist"",
      ""assignedAgentId"": ""分配的Agent ID"",
      ""priority"": ""优先级: Low/Medium/High/Critical"",
      ""input"": ""任务输入内容"",
      ""parentTaskIndex"": 父任务索引（从0开始，没有则为-1）
    }}
  ],
  ""reasoning"": ""规划理由说明""
}}

注意：
- 如果任务不复杂，可以只创建一个任务
- 确保每个任务都有明确的输入和目标
- 合理安排任务的依赖关系";

            var messages = new List<AIChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = $"用户请求：\n{userRequest}" }
            };

            var aiResponse = await _aiAgentService.ChatAsync(messages, false);
            var plan = ParseCoordinatorPlan(aiResponse, userRequest, requiredCapabilities);

            return plan;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AgentCoordinator] AI 规划失败，使用简单计划: {0}", ex.Message);
            return CreateFallbackPlan(userRequest);
        }
    }

    private static CoordinatorPlan ParseCoordinatorPlan(string aiResponse, string originalRequest, List<string> requiredCapabilities)
    {
        try
        {
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd <= jsonStart)
                throw new FormatException("未找到有效的 JSON");

            var json = aiResponse[jsonStart..(jsonEnd + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var plan = new CoordinatorPlan
            {
                PlanId = $"plan-{Guid.NewGuid():N}",
                OriginalRequest = originalRequest,
                Strategy = root.TryGetProperty("strategy", out var strategyProp)
                    ? strategyProp.GetString() ?? "默认策略"
                    : "默认策略",
                CoordinatorReasoning = root.TryGetProperty("reasoning", out var reasoningProp)
                    ? reasoningProp.GetString() ?? string.Empty
                    : string.Empty,
                RequiredCapabilities = requiredCapabilities
            };

            if (root.TryGetProperty("tasks", out var tasksProp) && tasksProp.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var taskElem in tasksProp.EnumerateArray())
                {
                    var task = new AgentTask
                    {
                        TaskId = $"task-{Guid.NewGuid():N}",
                        Description = taskElem.TryGetProperty("description", out var descProp)
                            ? descProp.GetString() ?? $"任务 {index + 1}"
                            : $"任务 {index + 1}",
                        Input = taskElem.TryGetProperty("input", out var inputProp)
                            ? inputProp.GetString() ?? originalRequest
                            : originalRequest,
                        Priority = taskElem.TryGetProperty("priority", out var priorityProp)
                            ? ParsePriority(priorityProp.GetString())
                            : TaskPriority.Medium
                    };

                    if (taskElem.TryGetProperty("assignedAgentId", out var agentIdProp))
                    {
                        task.AssignedAgentId = agentIdProp.GetString();
                    }

                    if (taskElem.TryGetProperty("assignedAgentType", out var agentTypeProp))
                    {
                        task.AssignedAgentType = ParseAgentType(agentTypeProp.GetString());
                    }

                    if (taskElem.TryGetProperty("parentTaskIndex", out var parentIdxProp) &&
                        parentIdxProp.GetInt32() >= 0 &&
                        parentIdxProp.GetInt32() < index)
                    {
                        task.ParentTaskId = plan.Tasks[parentIdxProp.GetInt32()].TaskId;
                        plan.Tasks[parentIdxProp.GetInt32()].SubTaskIds.Add(task.TaskId);
                    }

                    plan.Tasks.Add(task);
                    index++;
                }
            }

            if (plan.Tasks.Count == 0)
            {
                plan.Tasks.Add(new AgentTask
                {
                    TaskId = $"task-{Guid.NewGuid():N}",
                    Description = "处理用户请求",
                    AssignedAgentType = AgentType.Generalist,
                    AssignedAgentId = "agent.generalist",
                    Priority = TaskPriority.Medium,
                    Input = originalRequest
                });
            }

            return plan;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AgentCoordinator] 解析计划失败: {0}", ex.Message);
            var fallbackPlan = new CoordinatorPlan
            {
                PlanId = $"plan-{Guid.NewGuid():N}",
                OriginalRequest = originalRequest,
                Strategy = "解析失败，使用简单计划",
                RequiredCapabilities = requiredCapabilities,
                Tasks = new List<AgentTask>
                {
                    new()
                    {
                        TaskId = $"task-{Guid.NewGuid():N}",
                        Description = "处理用户请求",
                        AssignedAgentType = AgentType.Generalist,
                        AssignedAgentId = "agent.generalist",
                        Priority = TaskPriority.Medium,
                        Input = originalRequest
                    }
                }
            };
            return fallbackPlan;
        }
    }

    private static TaskPriority ParsePriority(string? priorityStr)
    {
        if (string.IsNullOrWhiteSpace(priorityStr)) return TaskPriority.Medium;
        return priorityStr.ToLower() switch
        {
            "low" => TaskPriority.Low,
            "high" => TaskPriority.High,
            "critical" => TaskPriority.Critical,
            _ => TaskPriority.Medium
        };
    }

    private static AgentType ParseAgentType(string? typeStr)
    {
        if (string.IsNullOrWhiteSpace(typeStr)) return AgentType.Generalist;
        return typeStr.ToLower() switch
        {
            "coordinator" => AgentType.Coordinator,
            "researcher" => AgentType.Researcher,
            "writer" => AgentType.Writer,
            "programmer" => AgentType.Programmer,
            "analyst" => AgentType.Analyst,
            "critic" => AgentType.Critic,
            _ => AgentType.Generalist
        };
    }

    private async Task<string> AggregateResultsAsync(
        CoordinatorPlan plan,
        Dictionary<string, AgentExecutionResult> results,
        CancellationToken cancellationToken)
    {
        try
        {
            var successfulResults = results.Values.Where(r => r.Success).ToList();
            if (successfulResults.Count == 0)
                return "所有任务都执行失败了。";

            if (successfulResults.Count == 1)
                return successfulResults[0].Output ?? string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("## 多 Agent 协作结果");
            sb.AppendLine();
            sb.AppendLine($"原始请求：{plan.OriginalRequest}");
            sb.AppendLine();
            sb.AppendLine($"执行策略：{plan.Strategy}");
            sb.AppendLine();
            sb.AppendLine($"共完成 {successfulResults.Count} 个任务：");
            sb.AppendLine();

            foreach (var result in successfulResults)
            {
                sb.AppendLine($"### {result.AgentName}");
                sb.AppendLine(result.Output);
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AgentCoordinator] 结果聚合失败: {0}", ex.Message);
            var firstSuccess = results.Values.FirstOrDefault(r => r.Success);
            return firstSuccess?.Output ?? "结果聚合失败";
        }
    }
}
