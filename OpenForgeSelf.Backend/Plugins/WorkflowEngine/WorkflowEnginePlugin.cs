using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine;

public class WorkflowEnginePlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; private set; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; private set; } = new();

    private IWorkflowExecutor? _workflowExecutor;
    private IWorkflowScheduler? _workflowScheduler;
    private IWorkflowService? _workflowService;

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("初始化工作流引擎插件");

        var services = ctx.Get<IServiceCollection>();
        services?.AddScoped<IWorkflowService, WorkflowService>();
        services?.AddScoped<IWorkflowExecutor, WorkflowExecutor>();
        services?.AddScoped<IWorkflowScheduler, WorkflowScheduler>();

        RegisterServices(ctx);
        RegisterMenuExtensions(pluginId);
        RegisterToolExtensions(pluginId, ctx);

        XTrace.Log.Info("工作流引擎插件初始化完成");
    }

    private void RegisterServices(IServiceProvider services)
    {
        _workflowExecutor = new WorkflowExecutor(services);
        _workflowScheduler = new WorkflowScheduler(_workflowExecutor);
        _workflowService = new WorkflowService(_workflowScheduler);

        XTrace.Log.Debug("工作流引擎插件服务已注册");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        MenuExtensions.Add(new WorkflowMenuExtension
        {
            Id = "workflow.menu.main",
            Name = "工作流",
            PluginId = pluginId,
            Icon = "fa-project-diagram",
            Path = "/workflows",
            Order = 80,
            ParentId = null
        });

        XTrace.Log.Debug("工作流引擎插件已注册菜单扩展点");
    }

    private void RegisterToolExtensions(string pluginId, IServiceProvider services)
    {
        ToolExtensions.Add(new CreateWorkflowToolFunction(services)
        {
            Id = "workflow.tool.create",
            Name = "create_workflow",
            PluginId = pluginId,
            Description = "创建一个新的工作流定义",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""name"": {
            ""type"": ""string"",
            ""description"": ""工作流名称""
        },
        ""description"": {
            ""type"": ""string"",
            ""description"": ""工作流描述""
        },
        ""category"": {
            ""type"": ""string"",
            ""description"": ""工作流分类""
        }
    },
    ""required"": [""name""]
}"
        });

        ToolExtensions.Add(new GetWorkflowToolFunction(services)
        {
            Id = "workflow.tool.get",
            Name = "get_workflow",
            PluginId = pluginId,
            Description = "获取指定工作流的详细信息，包括步骤定义、变量配置等",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""workflowId"": {
            ""type"": ""integer"",
            ""description"": ""工作流ID""
        }
    },
    ""required"": [""workflowId""]
}"
        });

        ToolExtensions.Add(new ExecuteWorkflowToolFunction(services)
        {
            Id = "workflow.tool.execute",
            Name = "execute_workflow",
            PluginId = pluginId,
            Description = "执行指定的工作流",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""workflowId"": {
            ""type"": ""integer"",
            ""description"": ""工作流ID""
        },
        ""inputVariables"": {
            ""type"": ""object"",
            ""description"": ""输入变量字典""
        }
    },
    ""required"": [""workflowId""]
}"
        });

        ToolExtensions.Add(new GetWorkflowStatusToolFunction(services)
        {
            Id = "workflow.tool.get_status",
            Name = "get_workflow_status",
            PluginId = pluginId,
            Description = "获取工作流执行状态",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""executionId"": {
            ""type"": ""integer"",
            ""description"": ""执行记录ID""
        }
    },
    ""required"": [""executionId""]
}"
        });

        ToolExtensions.Add(new ListWorkflowsToolFunction(services)
        {
            Id = "workflow.tool.list",
            Name = "list_workflows",
            PluginId = pluginId,
            Description = "获取工作流列表",
            ParametersJsonSchema = @"
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
}"
        });

        XTrace.Log.Debug("工作流引擎插件已注册AI工具函数扩展点，共 {0} 个工具", ToolExtensions.Count);
    }

}

public class WorkflowMenuExtension : IMenuExtension
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

public class CreateWorkflowToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public CreateWorkflowToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[WorkflowEngine] 执行 create_workflow 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var name = root.GetProperty("name").GetString() ?? string.Empty;
            var description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            var category = root.TryGetProperty("category", out var catProp) ? catProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(name))
            {
                return JsonSerializer.Serialize(new { success = false, error = "工作流名称不能为空" });
            }

            var request = new CreateWorkflowRequest
            {
                Name = name,
                Description = description,
                Category = category
            };

            var executor = new WorkflowExecutor(_serviceProvider);
            var scheduler = new WorkflowScheduler(executor);
            var service = new WorkflowService(scheduler);

            var workflow = await service.CreateWorkflowAsync(request);

            var response = new
            {
                success = true,
                workflow = new
                {
                    id = workflow.Id,
                    name = workflow.Name,
                    description = workflow.Description,
                    category = workflow.Category,
                    status = workflow.Status.ToString(),
                    createdAt = workflow.CreatedAt
                },
                message = "工作流创建成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "create_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["workflowId"] = workflow.Id,
                ["workflowName"] = workflow.Name
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowEngine] 执行 create_workflow 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "create_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

}

public class GetWorkflowToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public GetWorkflowToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[WorkflowEngine] 执行 get_workflow 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var workflowId = root.GetProperty("workflowId").GetInt64();

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            var executor = new WorkflowExecutor(_serviceProvider);
            var scheduler = new WorkflowScheduler(executor);
            var service = new WorkflowService(scheduler);

            var workflow = await service.GetWorkflowAsync(workflowId);
            if (workflow == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "工作流不存在" });
            }

            var response = new
            {
                success = true,
                workflow = new
                {
                    id = workflow.Id,
                    name = workflow.Name,
                    description = workflow.Description,
                    category = workflow.Category,
                    status = workflow.Status.ToString(),
                    stepCount = workflow.StepCount,
                    usageCount = workflow.UsageCount,
                    isFavorite = workflow.IsFavorite,
                    createdAt = workflow.CreatedAt,
                    updatedAt = workflow.UpdatedAt,
                    steps = workflow.Steps.Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        description = s.Description,
                        type = s.Type.ToString(),
                        toolName = s.ToolName,
                        nextStepId = s.NextStepId
                    }).ToList(),
                    variables = workflow.Variables.Select(v => new
                    {
                        name = v.Name,
                        type = v.Type,
                        defaultValue = v.DefaultValue,
                        description = v.Description
                    }).ToList()
                },
                message = "获取工作流详情成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "get_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["workflowId"] = workflowId,
                ["workflowName"] = workflow.Name
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowEngine] 执行 get_workflow 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "get_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public ExecuteWorkflowToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[WorkflowEngine] 执行 execute_workflow 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var workflowId = root.GetProperty("workflowId").GetInt64();

            Dictionary<string, object?>? inputVariables = null;
            if (root.TryGetProperty("inputVariables", out var varsProp))
            {
                inputVariables = JsonSerializer.Deserialize<Dictionary<string, object?>>(varsProp.GetRawText());
            }

            var executor = new WorkflowExecutor(_serviceProvider);
            var scheduler = new WorkflowScheduler(executor);
            var service = new WorkflowService(scheduler);

            var request = new ExecuteWorkflowRequest
            {
                InputVariables = inputVariables,
                TriggeredBy = "ai_tool"
            };

            var execution = await service.ExecuteWorkflowAsync(workflowId, request);

            var response = new
            {
                success = true,
                execution = new
                {
                    id = execution.Id,
                    workflowId = execution.WorkflowId,
                    status = execution.Status.ToString(),
                    startTime = execution.StartTime,
                    progress = execution.Progress
                },
                message = "工作流已启动"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "execute_workflow", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["workflowId"] = workflowId,
                ["executionId"] = execution.Id
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowEngine] 执行 execute_workflow 工具函数失败: {0}", ex.Message);

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

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public GetWorkflowStatusToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[WorkflowEngine] 执行 get_workflow_status 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var executionId = root.GetProperty("executionId").GetInt64();

            var executor = new WorkflowExecutor(_serviceProvider);
            var scheduler = new WorkflowScheduler(executor);
            var service = new WorkflowService(scheduler);

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
                    errorMessage = execution.ErrorMessage
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
            XTrace.Log.Error("[WorkflowEngine] 执行 get_workflow_status 工具函数失败: {0}", ex.Message);

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

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public ListWorkflowsToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[WorkflowEngine] 执行 list_workflows 工具函数，参数: {0}", parameters);

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

            var executor = new WorkflowExecutor(_serviceProvider);
            var scheduler = new WorkflowScheduler(executor);
            var service = new WorkflowService(scheduler);

            var result = await service.ListWorkflowsAsync(keyword, category, page, pageSize);

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
                }).ToList()
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
            XTrace.Log.Error("[WorkflowEngine] 执行 list_workflows 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_workflows", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

}
