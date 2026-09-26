using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public class WorkflowPlannerService : IWorkflowPlannerService
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolSelectorService _toolSelectorService;
    private readonly IAIAgentService _aiAgentService;

    public WorkflowPlannerService(
        IContext ctx,
        IToolSelectorService toolSelectorService,
        IAIAgentService aiAgentService)
    {
        // 宿主契约（IToolRegistry）经 Cordis 上下文在运行期获取；插件自有服务（工具选择器/AI 代理）保持构造注入。
        _toolRegistry = ctx.Get<IToolRegistry>() ?? throw new InvalidOperationException("宿主未提供 IToolRegistry 契约，无法初始化工作流规划器");
        _toolSelectorService = toolSelectorService;
        _aiAgentService = aiAgentService;
    }

    public async Task<WorkflowDefinition> PlanWorkflowAsync(string userRequest, List<string>? availableTools = null)
    {
        XTrace.Log.Info("[WorkflowPlannerService] 开始规划工作流，用户请求: {0}", userRequest);

        try
        {
            var systemPrompt = GetSystemPrompt();
            var toolsInfo = GetToolsInfo(availableTools);
            var userPrompt = BuildUserPrompt(userRequest, toolsInfo);

            var messages = new List<AIChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            var aiResponse = await _aiAgentService.ChatAsync(messages, false);

            var workflow = ParseWorkflowResponse(aiResponse, userRequest);

            XTrace.Log.Info("[WorkflowPlannerService] 工作流规划完成，步骤数: {0}", workflow.Steps.Count);

            return workflow;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowPlannerService] 工作流规划失败: {0}", ex.Message);
            XTrace.Log.Debug("[WorkflowPlannerService] 异常堆栈: {0}", ex.StackTrace);
            return GenerateFallbackWorkflow(userRequest);
        }
    }

    public Task<string> GetPlanPromptAsync(string userRequest, List<string>? availableTools = null)
    {
        var systemPrompt = GetSystemPrompt();
        var toolsInfo = GetToolsInfo(availableTools);
        var userPrompt = BuildUserPrompt(userRequest, toolsInfo);

        var fullPrompt = new StringBuilder();
        fullPrompt.AppendLine("=== System Prompt ===");
        fullPrompt.AppendLine(systemPrompt);
        fullPrompt.AppendLine();
        fullPrompt.AppendLine("=== User Prompt ===");
        fullPrompt.AppendLine(userPrompt);

        return Task.FromResult(fullPrompt.ToString());
    }

    private string GetSystemPrompt()
    {
        return @"
你是一个专业的工作流规划助手。你的任务是根据用户的需求描述，分析任务并生成一个结构化的工作流定义。

## 工作流概念
工作流由一系列有序的步骤组成，每个步骤执行特定的操作。步骤之间可以有数据流转，支持条件判断、循环、并行执行等控制流。

## 步骤类型
1. **ToolCall** - 工具调用步骤：调用一个已注册的工具函数执行具体操作
2. **Condition** - 条件判断步骤：根据条件表达式决定执行路径
3. **Loop** - 循环步骤：对列表进行迭代或按条件循环执行子步骤
4. **Parallel** - 并行步骤：同时执行多个子步骤
5. **Wait** - 等待步骤：等待一段时间后继续执行
6. **Http** - HTTP请求步骤：发送HTTP请求获取数据

## 数据流转
- 步骤可以通过变量传递数据
- 使用 `{{var.variableName}}` 引用工作流变量
- 使用 `{{step.stepId.output}}` 引用其他步骤的输出结果

## 输出格式
你必须输出一个有效的 JSON 对象，格式如下：

```json
{
  ""name"": ""工作流名称"",
  ""description"": ""工作流描述"",
  ""category"": ""分类名称"",
  ""variables"": [
    {
      ""name"": ""变量名"",
      ""type"": ""string|number|boolean|object|array"",
      ""defaultValue"": ""默认值"",
      ""description"": ""变量描述""
    }
  ],
  ""steps"": [
    {
      ""id"": ""step_001"",
      ""name"": ""步骤名称"",
      ""description"": ""步骤描述"",
      ""type"": ""ToolCall"",
      ""toolName"": ""工具函数名"",
      ""parametersJson"": ""{\""param1\"": \""value1\""}"",
      ""nextStepId"": ""step_002"",
      ""errorHandling"": {
        ""maxRetries"": 3,
        ""retryDelayMs"": 1000,
        ""continueOnError"": false
      }
    }
  ],
  ""startStepId"": ""step_001""
}
```

## 重要规则
1. 步骤ID必须唯一，建议使用 step_001, step_002 等格式
2. 第一个步骤的ID必须与 startStepId 匹配
3. 每个步骤的 nextStepId 指向下一个要执行的步骤ID，最后一个步骤可以不设置
4. 工具调用步骤必须指定 toolName 和 parametersJson
5. 条件判断步骤需要设置 conditionExpression
6. 确保工作流逻辑合理，步骤顺序正确
7. 只输出 JSON，不要包含任何额外的解释文字
";
    }

    private string GetToolsInfo(List<string>? availableTools = null)
    {
        var allTools = _toolRegistry.GetAllTools();
        var toolsToUse = availableTools != null && availableTools.Count > 0
            ? allTools.Where(t => availableTools.Contains(t.Name)).ToList()
            : allTools.ToList();

        var sb = new StringBuilder();
        sb.AppendLine("## 可用工具列表");
        sb.AppendLine();

        foreach (var tool in toolsToUse)
        {
            sb.AppendLine($"### {tool.Name}");
            sb.AppendLine($"- 描述: {tool.Description}");
            sb.AppendLine($"- 参数 Schema:");
            sb.AppendLine(tool.ParametersJsonSchema);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildUserPrompt(string userRequest, string toolsInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 用户需求");
        sb.AppendLine(userRequest);
        sb.AppendLine();
        sb.AppendLine(toolsInfo);
        sb.AppendLine();
        sb.AppendLine("请根据用户需求和可用工具，规划一个完整的工作流。");
        sb.AppendLine("要求：");
        sb.AppendLine("1. 分析用户需求，确定需要哪些步骤");
        sb.AppendLine("2. 为每个步骤选择最合适的工具");
        sb.AppendLine("3. 设置合理的步骤参数");
        sb.AppendLine("4. 确保步骤之间的逻辑顺序正确");
        sb.AppendLine("5. 考虑错误处理和重试机制");
        sb.AppendLine("6. 输出完整的 JSON 格式工作流定义");

        return sb.ToString();
    }

    private WorkflowDefinition ParseWorkflowResponse(string aiResponse, string userRequest)
    {
        try
        {
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (workflow != null && workflow.Steps.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(workflow.Name))
                    {
                        workflow.Name = "AI规划的工作流";
                    }
                    workflow.Description ??= userRequest;
                    workflow.CreatedAt = DateTime.Now;
                    workflow.UpdatedAt = DateTime.Now;
                    workflow.Status = WorkflowStatus.Draft;

                    if (string.IsNullOrWhiteSpace(workflow.StartStepId) && workflow.Steps.Count > 0)
                    {
                        workflow.StartStepId = workflow.Steps[0].Id;
                    }

                    return workflow;
                }
            }
        }
        catch (JsonException ex)
        {
            XTrace.Log.Warn("[WorkflowPlannerService] 解析AI响应JSON失败: {0}", ex.Message);
        }

        return GenerateFallbackWorkflow(userRequest);
    }

    private WorkflowDefinition GenerateFallbackWorkflow(string userRequest)
    {
        XTrace.Log.Info("[WorkflowPlannerService] 使用回退策略生成工作流");

        var rankedTools = _toolSelectorService.RankTools(userRequest);
        var topTools = rankedTools.Where(t => t.MatchScore > 0.3).Take(5).ToList();

        var steps = new List<WorkflowStep>();
        var stepIndex = 1;

        foreach (var toolMatch in topTools)
        {
            var stepId = $"step_{stepIndex:D3}";
            var nextStepId = stepIndex < topTools.Count ? $"step_{stepIndex + 1:D3}" : null;

            steps.Add(new WorkflowStep
            {
                Id = stepId,
                Name = $"{toolMatch.Tool.Name} 步骤",
                Description = $"使用 {toolMatch.Tool.Name} 执行相关操作",
                Type = WorkflowStepType.ToolCall,
                ToolName = toolMatch.Tool.Name,
                ParametersJson = "{}",
                NextStepId = nextStepId,
                ErrorHandling = new ErrorHandlingConfig
                {
                    MaxRetries = 2,
                    RetryDelayMs = 1000,
                    ContinueOnError = false
                }
            });

            stepIndex++;
        }

        if (steps.Count == 0)
        {
            steps.Add(new WorkflowStep
            {
                Id = "step_001",
                Name = "等待步骤",
                Description = "默认等待步骤",
                Type = WorkflowStepType.Wait,
                ParametersJson = "{\"waitSeconds\": 1}",
                ErrorHandling = new ErrorHandlingConfig()
            });
        }

        return new WorkflowDefinition
        {
            Name = "AI规划工作流",
            Description = userRequest,
            Category = "AI生成",
            Steps = steps,
            Variables = new List<WorkflowVariable>(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Status = WorkflowStatus.Draft,
            StartStepId = steps.Count > 0 ? steps[0].Id : null
        };
    }
}
