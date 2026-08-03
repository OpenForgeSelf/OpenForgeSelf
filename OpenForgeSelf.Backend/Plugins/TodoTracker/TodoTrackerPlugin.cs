using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Entities;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Models;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Services;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.TodoTracker;

public class TodoTrackerPlugin : IPlugin
{
    public string Id => "todotracker.plugin";
    public string Name => "待办追踪插件";
    public string Version => "1.0.0";
    public string Author => "OpenForgeSelf Team";
    public string Description => "待办事项追踪插件，提供 CRUD REST API、独立管理页与 AI 工具函数。";
    public string IconUrl => "https://example.com/todotracker-icon.png";

    public List<IMenuExtension> MenuExtensions { get; private set; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; private set; } = new();

    private IServiceProvider? _serviceProvider;

    public void Initialize(IServiceProvider services)
    {
        try
        {
            XTrace.Log.Info("初始化待办追踪插件");
            _serviceProvider = services;

            RegisterMenuExtensions();
            RegisterToolExtensions();

            XTrace.Log.Info("待办追踪插件初始化完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("待办追踪插件初始化失败: {0}", ex.Message);
            throw;
        }
    }

    public void Start()
    {
        try
        {
            XTrace.Log.Info("启动待办追踪插件");

            // 确保表已创建（XCodeConfig.EnsureTablesCreated 会自动通过 EntityBase 反射调用 Meta.CreateTable）
            EnsureTablesCreated();

            XTrace.Log.Info("待办追踪插件启动完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("待办追踪插件启动失败: {0}", ex.Message);
            throw;
        }
    }

    public void Stop()
    {
        try
        {
            XTrace.Log.Info("停止待办追踪插件");
            XTrace.Log.Info("待办追踪插件已停止");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("待办追踪插件停止失败: {0}", ex.Message);
            throw;
        }
    }

    public void Destroy()
    {
        try
        {
            XTrace.Log.Info("销毁待办追踪插件");
            MenuExtensions.Clear();
            ToolExtensions.Clear();
            XTrace.Log.Info("待办追踪插件已销毁");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("待办追踪插件销毁失败: {0}", ex.Message);
            throw;
        }
    }

    private void RegisterMenuExtensions()
    {
        MenuExtensions.Add(new TodoTrackerMenuExtension
        {
            Id = "todotracker.menu.main",
            Name = "待办追踪",
            PluginId = Id,
            Icon = "fa-check-square",
            Path = "/todo",
            Order = 200,
            ParentId = null
        });

        XTrace.Log.Debug("待办追踪插件已注册菜单扩展点");
    }

    private void RegisterToolExtensions()
    {
        ToolExtensions.Add(new CreateTodoToolFunction(_serviceProvider)
        {
            Id = "todotracker.tool.create_todo",
            Name = "create_todo",
            PluginId = Id,
            Description = "创建一个新的待办事项。必填参数：title（标题，最长 200 字符）。可选：remark（备注，最长 1000 字符）、dueDate（截止日期 ISO 8601）。",
            ParametersJsonSchema = @"{
  ""type"": ""object"",
  ""properties"": {
    ""title"": { ""type"": ""string"", ""description"": ""待办标题（最长 200 字符）"" },
    ""remark"": { ""type"": ""string"", ""description"": ""备注说明（最长 1000 字符）"" },
    ""dueDate"": { ""type"": ""string"", ""description"": ""截止日期 ISO 8601 格式，如 2026-12-31T23:59:59"" }
  },
  ""required"": [""title""]
}"
        });

        ToolExtensions.Add(new ListTodosToolFunction(_serviceProvider)
        {
            Id = "todotracker.tool.list_todos",
            Name = "list_todos",
            PluginId = Id,
            Description = "列出待办事项，支持按状态过滤与分页。可选参数：status（Pending/Completed）、page（页码，默认 1）、pageSize（每页条数，默认 20，最大 100）。",
            ParametersJsonSchema = @"{
  ""type"": ""object"",
  ""properties"": {
    ""status"": { ""type"": ""string"", ""enum"": [""Pending"", ""Completed""], ""description"": ""按状态过滤；不传则返回全部"" },
    ""page"": { ""type"": ""integer"", ""description"": ""页码（从 1 开始），默认 1"", ""minimum"": 1 },
    ""pageSize"": { ""type"": ""integer"", ""description"": ""每页条数（最大 100），默认 20"", ""minimum"": 1, ""maximum"": 100 }
  }
}"
        });

        ToolExtensions.Add(new CompleteTodoToolFunction(_serviceProvider)
        {
            Id = "todotracker.tool.complete_todo",
            Name = "complete_todo",
            PluginId = Id,
            Description = "标记指定待办事项为已完成。必填参数：id（待办 ID）。",
            ParametersJsonSchema = @"{
  ""type"": ""object"",
  ""properties"": {
    ""id"": { ""type"": ""integer"", ""description"": ""待办 ID"", ""minimum"": 1 }
  },
  ""required"": [""id""]
}"
        });

        XTrace.Log.Debug("待办追踪插件已注册 {0} 个 AI 工具函数扩展点", ToolExtensions.Count);
    }

    private void EnsureTablesCreated()
    {
        try
        {
            // XCodeConfig.InitializeXCodeDatabase 已通过反射统一调用所有 EntityBase 子类的 Meta.CreateTable，
            // 这里仅触发 Todo 实体类的静态构造以确保元数据加载。
            var tableName = Todo.Meta.TableName;
            XTrace.Log.Info("待办追踪插件表 {0} 元数据已加载", tableName);
            XTrace.Log.Info("待办追踪插件数据库表已就绪");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("待办追踪插件建表失败: {0}", ex.Message);
        }
    }
}

public class TodoTrackerMenuExtension : IMenuExtension
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

/// <summary>
/// 待办状态常量与映射辅助。
/// </summary>
public static class TodoStatus
{
    public const int PendingValue = 0;
    public const int CompletedValue = 1;

    public const string PendingName = "Pending";
    public const string CompletedName = "Completed";

    public static string ToName(int value) => value == CompletedValue ? CompletedName : PendingName;

    public static int? ToValue(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return name switch
        {
            PendingName => PendingValue,
            CompletedName => CompletedValue,
            _ => null
        };
    }
}
