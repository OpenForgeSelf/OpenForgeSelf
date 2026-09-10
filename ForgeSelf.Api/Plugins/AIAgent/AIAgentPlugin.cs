using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent;

public class AIAgentPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";

        XTrace.Log.Info("[AIAgentPlugin] 初始化AI代理插件");

        var services = ctx.Get<IServiceCollection>();
        services?.AddSingleton<IProjectWorkspaceService, ProjectWorkspaceService>();
        services?.AddSingleton<IProjectSkillScannerService, ProjectSkillScannerService>();
        services?.AddSingleton<IProjectRegistryService, ProjectRegistryService>();
        services?.AddScoped<IAIAgentService, AIAgentService>();
        services?.AddScoped<IPluginMessageService, PluginMessageService>();
        services?.AddScoped<IAgentRegistryService, AgentRegistryService>();
        services?.AddScoped<IAgentCoordinatorService, AgentCoordinatorService>();
        services?.AddScoped<IAgentExecutorService, AgentExecutorService>();
        services?.AddScoped<IProactivePlanningService, ProactivePlanningService>();
        services?.AddScoped<IWorkflowPlannerService, WorkflowPlannerService>();
        services?.AddScoped<IToolSelectorService, ToolSelectorService>();
        // 补注册 AI 工作流助手与 AI 重试顾问（供插件内部构造注入）。
        services?.AddScoped<IAIWorkflowAssistant, AIWorkflowAssistant>();
        services?.AddScoped<IWorkflowAIAdvisor, AIWorkflowAdvisor>();

        // 计划驱动执行引擎（029，T016）：RunFlowToolSet 承载 submit_plan/complete_step/request_help
        // 三特殊工具，供 PlanGeneratorService（规划）/ StepRunLoopService（步骤循环）经 extraTools 显式挂载（R4）。
        var runFlowTools = new RunFlowToolSet(pluginId);
        services?.AddSingleton(runFlowTools);
        services?.AddScoped<IPlanGeneratorService, PlanGeneratorService>();
        services?.AddScoped<IStepRunLoopService, StepRunLoopService>();
        services?.AddScoped<IRunOrchestratorService, RunOrchestratorService>();

        // eager 提供 IWorkflowAIAdvisor（调研 §5.6 裁决 F：每上下文 eager 单例，非懒解析委托）：
        // 经子容器 scope 解析其依赖链（AIWorkflowAdvisor → IAIWorkflowAssistant → IAIAgentService/IToolSelectorService + IContext），
        // 再 ctx.Register<IWorkflowAIAdvisor>(实例) 写入 root 共享服务表，供兄弟插件（WorkflowEngine）经 ctx.Get 消费。
        if (services != null)
        {
            var advisorScope = services.BuildServiceProvider().CreateScope();
            // 先注册 scope 释放 effect（早进列表，Dispose 逆序时后执行）→ 卸载时共享表条目先摘除、scope 后释放，避免 advisor 悬空引用已释放的服务。
            ctx.Effect(() => advisorScope);
            var advisor = advisorScope.ServiceProvider.GetRequiredService<IWorkflowAIAdvisor>();
            ctx.Register<IWorkflowAIAdvisor>(advisor);
        }

        RegisterMenuExtensions(pluginId);
        RegisterToolFunctionExtensions(pluginId, ctx, runFlowTools);

        XTrace.Log.Info("[AIAgentPlugin] AI代理插件初始化完成");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        XTrace.Log.Debug("[AIAgentPlugin] 注册菜单扩展点");

        MenuExtensions.Add(new AIAgentMenuExtension
        {
            Id = "aiagent.menu.main",
            // 与 plugin.json frontend 契约对齐（route=/ai-agent、menu="AI Agent"）。
            // 注意：侧边栏 IMenuExtension 经 registerPluginRoutes 挂到 /plugin/<path> → PluginPage.vue 占位，
            // 真实界面在 manifest 直路径 /ai-agent（dynamicPlugins 远程加载）。「侧边栏 → PluginPage 占位」
            // 是跨插件预存架构问题（QuickLinks 同构），不在本次接通范围内，见 TODO/工作日记。
            Name = "AI Agent",
            PluginId = pluginId,
            Icon = "fa-solid fa-robot",
            Path = "/ai-agent",
            Order = 10,
            ParentId = null
        });

        XTrace.Log.Debug("[AIAgentPlugin] 菜单扩展点注册完成，共 {0} 个菜单项", MenuExtensions.Count);
    }

    private void RegisterToolFunctionExtensions(string pluginId, IServiceProvider services, RunFlowToolSet runFlowTools)
    {
        XTrace.Log.Debug("[AIAgentPlugin] 注册AI工具函数扩展点");

        ToolExtensions.Add(new GetCurrentTimeToolFunction(pluginId));
        ToolExtensions.Add(new CalculateToolFunction(pluginId));
        ToolExtensions.Add(new PlanWorkflowToolFunction(pluginId, services));
        ToolExtensions.Add(new ExecuteWorkflowToolFunction(pluginId, services));
        ToolExtensions.Add(new GetWorkflowStatusToolFunction(pluginId, services));
        ToolExtensions.Add(new ListWorkflowsToolFunction(pluginId, services));
        // 项目文件 MCP 工具：对选定的工作目录（项目）读/写/列表。
        ToolExtensions.Add(new ListFilesToolFunction(pluginId, services));
        ToolExtensions.Add(new ReadFileToolFunction(pluginId, services));
        ToolExtensions.Add(new WriteFileToolFunction(pluginId, services));
        // 计划驱动运行流特殊工具（029 R4）：注册进 ToolRegistry 供引擎执行兜底，
        // 但 ResolveOwnToolDefinitions 按名排除 → 不进 FreeLoop，只经 extraTools 显式挂载。
        ToolExtensions.Add(runFlowTools.SubmitPlan);
        ToolExtensions.Add(runFlowTools.CompleteStep);
        ToolExtensions.Add(runFlowTools.RequestHelp);

        XTrace.Log.Debug("[AIAgentPlugin] AI工具函数扩展点注册完成，共 {0} 个工具函数", ToolExtensions.Count);
    }
}

public class AIAgentMenuExtension : IMenuExtension
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? ParentId { get; set; }
    public IReadOnlyList<IMenuExtension>? Children { get; set; }
}

public class GetCurrentTimeToolFunction : IToolFunctionExtension
{
    public string Id => "aiagent.get_current_time";
    public string Name => "get_current_time";
    public string PluginId { get; }
    public string Description => "获取当前的日期和时间。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""format"": {
            ""type"": ""string"",
            ""description"": ""时间格式，如 yyyy-MM-dd HH:mm:ss"",
            ""default"": ""yyyy-MM-dd HH:mm:ss""
        },
        ""timezone"": {
            ""type"": ""string"",
            ""description"": ""时区，默认使用本地时区"",
            ""default"": ""local""
        }
    },
    ""required"": []
}";

    public GetCurrentTimeToolFunction(string pluginId)
    {
        PluginId = pluginId;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            XTrace.Log.Debug("[AIAgentPlugin] 执行 get_current_time 工具函数");

            var format = "yyyy-MM-dd HH:mm:ss";
            if (!string.IsNullOrEmpty(parameters))
            {
                using var doc = JsonDocument.Parse(parameters);
                if (doc.RootElement.TryGetProperty("format", out var formatProp))
                {
                    format = formatProp.GetString() ?? format;
                }
            }

            var now = DateTime.Now;
            var result = now.ToString(format);

            var response = new
            {
                success = true,
                currentTime = result,
                timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] get_current_time 执行失败: {0}", ex.Message);
            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return Task.FromResult(JsonSerializer.Serialize(errorResponse));
        }
    }
}

public class CalculateToolFunction : IToolFunctionExtension
{
    public string Id => "aiagent.calculate";
    public string Name => "calculate";
    public string PluginId { get; }
    public string Description => "执行基本的数学计算，支持加减乘除。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""expression"": {
            ""type"": ""string"",
            ""description"": ""数学表达式，如 1 + 2 * 3""
        }
    },
    ""required"": [""expression""]
}";

    public CalculateToolFunction(string pluginId)
    {
        PluginId = pluginId;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            XTrace.Log.Debug("[AIAgentPlugin] 执行 calculate 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var expression = doc.RootElement.GetProperty("expression").GetString() ?? string.Empty;

            var result = EvaluateExpression(expression);

            var response = new
            {
                success = true,
                expression = expression,
                result = result
            };

            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] calculate 执行失败: {0}", ex.Message);
            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return Task.FromResult(JsonSerializer.Serialize(errorResponse));
        }
    }

    private static double EvaluateExpression(string expression)
    {
        var table = new System.Data.DataTable();
        var result = table.Compute(expression, string.Empty);
        return Convert.ToDouble(result);
    }
}

public class PlanWorkflowToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "aiagent.plan_workflow";
    public string Name => "plan_workflow";
    public string PluginId { get; }
    public string Description => "根据用户需求描述，使用AI智能规划工作流，生成工作流定义。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""userRequest"": {
            ""type"": ""string"",
            ""description"": ""用户需求描述，如'批量重命名文件夹中的所有图片文件并压缩'""
        },
        ""availableTools"": {
            ""type"": ""array"",
            ""description"": ""可用工具名称列表（可选，不填则使用所有已注册工具）"",
            ""items"": {
                ""type"": ""string""
            }
        }
    },
    ""required"": [""userRequest""]
}";

    public PlanWorkflowToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 执行 plan_workflow 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var userRequest = root.GetProperty("userRequest").GetString() ?? string.Empty;

            List<string>? availableTools = null;
            if (root.TryGetProperty("availableTools", out var toolsProp))
            {
                availableTools = new List<string>();
                foreach (var tool in toolsProp.EnumerateArray())
                {
                    var toolName = tool.GetString();
                    if (!string.IsNullOrEmpty(toolName))
                    {
                        availableTools.Add(toolName);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(userRequest))
            {
                return JsonSerializer.Serialize(new { success = false, error = "用户需求描述不能为空" });
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var plannerService = scope.ServiceProvider.GetRequiredService<IWorkflowPlannerService>();

            var workflow = await plannerService.PlanWorkflowAsync(userRequest, availableTools);

            var response = new
            {
                success = true,
                workflow = new
                {
                    name = workflow.Name,
                    description = workflow.Description,
                    category = workflow.Category,
                    steps = workflow.Steps.Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        description = s.Description,
                        type = s.Type.ToString(),
                        toolName = s.ToolName,
                        parametersJson = s.ParametersJson,
                        nextStepId = s.NextStepId
                    }).ToList(),
                    variables = workflow.Variables.Select(v => new
                    {
                        name = v.Name,
                        type = v.Type,
                        defaultValue = v.DefaultValue,
                        description = v.Description
                    }).ToList(),
                    startStepId = workflow.StartStepId,
                    stepCount = workflow.Steps.Count
                },
                message = "工作流规划成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "plan_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["stepCount"] = workflow.Steps.Count,
                ["userRequestLength"] = userRequest.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] plan_workflow 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "plan_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

}

public class ExecuteWorkflowToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "aiagent.execute_workflow";
    public string Name => "execute_workflow";
    public string PluginId { get; }
    public string Description => "执行指定的工作流，支持按工作流ID执行或直接传入工作流定义。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""workflowId"": {
            ""type"": ""integer"",
            ""description"": ""工作流ID（与workflowDefinition二选一）""
        },
        ""workflowDefinition"": {
            ""type"": ""object"",
            ""description"": ""工作流定义对象（与workflowId二选一）""
        },
        ""inputVariables"": {
            ""type"": ""object"",
            ""description"": ""输入变量字典""
        }
    },
    ""required"": []
}";

    public ExecuteWorkflowToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 执行 execute_workflow 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            long? workflowId = null;
            if (root.TryGetProperty("workflowId", out var workflowIdProp))
            {
                workflowId = workflowIdProp.GetInt64();
            }

            Dictionary<string, object?>? inputVariables = null;
            if (root.TryGetProperty("inputVariables", out var varsProp))
            {
                inputVariables = JsonSerializer.Deserialize<Dictionary<string, object?>>(varsProp.GetRawText());
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            var service = _serviceProvider.GetRequiredService<IWorkflowService>();
            var executor = _serviceProvider.GetRequiredService<IWorkflowExecutor>();

            WorkflowExecutionDto execution;
            if (workflowId.HasValue)
            {
                var request = new ExecuteWorkflowRequest
                {
                    InputVariables = inputVariables,
                    TriggeredBy = "ai_tool"
                };
                execution = await service.ExecuteWorkflowAsync(workflowId.Value, request);
            }
            else if (root.TryGetProperty("workflowDefinition", out var workflowDefProp))
            {
                var workflowDef = JsonSerializer.Deserialize<CreateWorkflowRequest>(workflowDefProp.GetRawText());
                if (workflowDef == null)
                {
                    return JsonSerializer.Serialize(new { success = false, error = "工作流定义解析失败" });
                }

                if (string.IsNullOrWhiteSpace(workflowDef.Name))
                {
                    workflowDef.Name = "AI生成的工作流";
                }

                var created = await service.CreateWorkflowAsync(workflowDef);
                var request = new ExecuteWorkflowRequest
                {
                    InputVariables = inputVariables,
                    TriggeredBy = "ai_tool"
                };
                execution = await service.ExecuteWorkflowAsync(created.Id, request);
            }
            else
            {
                return JsonSerializer.Serialize(new { success = false, error = "必须提供 workflowId 或 workflowDefinition" });
            }

            var response = new
            {
                success = true,
                execution = new
                {
                    id = execution.Id,
                    workflowId = execution.WorkflowId,
                    workflowName = execution.WorkflowName,
                    status = execution.Status.ToString(),
                    startTime = execution.StartTime,
                    progress = execution.Progress,
                    triggeredBy = execution.TriggeredBy
                },
                message = "工作流已启动"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "execute_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["executionId"] = execution.Id,
                ["workflowId"] = execution.WorkflowId
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] execute_workflow 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "execute_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

}

public class GetWorkflowStatusToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "aiagent.get_workflow_status";
    public string Name => "get_workflow_status";
    public string PluginId { get; }
    public string Description => "查询工作流执行状态、进度和日志。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""executionId"": {
            ""type"": ""integer"",
            ""description"": ""执行记录ID""
        }
    },
    ""required"": [""executionId""]
}";

    public GetWorkflowStatusToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 执行 get_workflow_status 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var executionId = root.GetProperty("executionId").GetInt64();

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            var service = _serviceProvider.GetRequiredService<IWorkflowService>();

            var execution = await service.GetExecutionDetailAsync(executionId);
            if (execution == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "执行记录不存在" });
            }

            var response = new
            {
                success = true,
                execution = new
                {
                    id = execution.Id,
                    workflowId = execution.WorkflowId,
                    workflowName = execution.WorkflowName,
                    status = execution.Status.ToString(),
                    startTime = execution.StartTime,
                    endTime = execution.EndTime,
                    currentStepId = execution.CurrentStepId,
                    progress = execution.Progress,
                    errorMessage = execution.ErrorMessage,
                    logCount = execution.Logs.Count,
                    recentLogs = execution.Logs.Take(10).Select(l => new
                    {
                        timestamp = l.Timestamp,
                        stepId = l.StepId,
                        stepName = l.StepName,
                        logLevel = l.LogLevel,
                        message = l.Message
                    }).ToList()
                },
                message = "获取状态成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "get_workflow_status", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["executionId"] = executionId
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] get_workflow_status 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "get_workflow_status", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

}

public class ListWorkflowsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "aiagent.list_workflows";
    public string Name => "list_workflows";
    public string PluginId { get; }
    public string Description => "列出可用的工作流模板和定义，支持关键词搜索和分类筛选。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""keyword"": {
            ""type"": ""string"",
            ""description"": ""搜索关键词""
        },
        ""category"": {
            ""type"": ""string"",
            ""description"": ""分类筛选""
        },
        ""page"": {
            ""type"": ""integer"",
            ""description"": ""页码，默认1"",
            ""default"": 1
        },
        ""pageSize"": {
            ""type"": ""integer"",
            ""description"": ""每页数量，默认20"",
            ""default"": 20
        }
    },
    ""required"": []
}";

    public ListWorkflowsToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 执行 list_workflows 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            string? keyword = null;
            if (root.TryGetProperty("keyword", out var keywordProp))
                keyword = keywordProp.GetString();

            string? category = null;
            if (root.TryGetProperty("category", out var categoryProp))
                category = categoryProp.GetString();

            int page = 1;
            if (root.TryGetProperty("page", out var pageProp))
                page = pageProp.GetInt32();

            int pageSize = 20;
            if (root.TryGetProperty("pageSize", out var pageSizeProp))
                pageSize = pageSizeProp.GetInt32();

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            var service = _serviceProvider.GetRequiredService<IWorkflowService>();

            var result = await service.ListWorkflowsAsync(keyword, category, page, pageSize);

            var templates = await service.GetTemplatesAsync();

            var response = new
            {
                success = true,
                total = result.Total,
                page = result.Page,
                pageSize = result.PageSize,
                workflows = result.Items.Select(w => new
                {
                    id = w.Id,
                    name = w.Name,
                    description = w.Description,
                    category = w.Category,
                    status = w.Status.ToString(),
                    stepCount = w.StepCount,
                    usageCount = w.UsageCount,
                    isFavorite = w.IsFavorite,
                    updatedAt = w.UpdatedAt
                }).ToList(),
                templates = templates.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    description = t.Description,
                    category = t.Category,
                    icon = t.Icon,
                    tags = t.Tags,
                    stepCount = t.StepCount
                }).ToList(),
                message = "获取工作流列表成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_workflows", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["keyword"] = keyword ?? string.Empty,
                ["category"] = category ?? string.Empty,
                ["total"] = result.Total
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] list_workflows 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_workflows", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

}
