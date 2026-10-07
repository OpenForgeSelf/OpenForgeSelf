using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Data;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TodoTracker;

/// <summary>
/// 待办追踪插件入口（PILOT-054 起：待办 = 可下发给 agent 的任务台账）。
///
/// 两处规范缺口在本批闭合（都是插件既有欠账，不是新功能）：
/// <list type="bullet">
/// <item><b>自行建表</b>（铁律 12）：宿主 <c>EnsureTablesCreated</c> 只扫已加载程序集，插件 DLL 加载晚于它
/// → 必须自己触发一次连接建表，否则生产库会出现「表神秘缺失 → no such table」；</item>
/// <item><b>菜单只声明一处</b>（铁律 19②）：界面入口在 <c>plugin.json.frontend</c>（menu/route/entry）声明，
/// 原 <c>IMenuExtension</c> 贡献与之并存会造成两份菜单真相，故撤销。</item>
/// </list>
/// </summary>
public class TodoTrackerPlugin : IPlugin
{
    /// <summary>界面归 <c>plugin.json.frontend</c>，本插件不再贡献后端菜单（铁律 19②）。</summary>
    public List<IMenuExtension> MenuExtensions { get; private set; } = [];

    public List<IToolFunctionExtension> ToolExtensions { get; private set; } = [];

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("初始化待办追踪插件");

        // 解析顺序无关：全部 Scoped，由插件子容器按构造签名装配
        var services = ctx.Get<IServiceCollection>();
        services?.AddScoped<ITodoProjectService, TodoProjectService>();
        services?.AddScoped<ITaskExecutionService, TaskExecutionService>();
        services?.AddScoped<IArtifactImportService, ArtifactImportService>();
        services?.AddScoped<IAgentTaskGateway, AgentTaskGateway>();
        services?.AddScoped<ITodoService, TodoService>();
        services?.AddScoped<ITodoDispatchService, TodoDispatchService>();

        // 建表必须先于任何读写与回填（Apply 早于宿主建库；失败绝不静默吞）
        if (!TodoTrackerTables.EnsureCreated())
            XTrace.Log.Warn("待办追踪插件数据库初始化未成功，任务台账与执行记录可能不可用");
        else
            TodoBackfill.Run();

        RegisterToolExtensions(pluginId, ctx);

        XTrace.Log.Info("待办追踪插件初始化完成（工具函数 {0} 个）", ToolExtensions.Count);
    }

    /// <summary>
    /// 注册 AI 工具函数。description 里写清必填/可选与取值（铁律 18：外部调用方只读说明就该知道怎么传参）。
    /// </summary>
    private void RegisterToolExtensions(string pluginId, IServiceProvider services)
    {
        ToolExtensions.Add(new CreateTodoToolFunction(services)
        {
            Id = "todotracker.tool.create_todo",
            Name = "create_todo",
            PluginId = pluginId,
            Description = "创建一个新的待办/任务。必填：title（标题，最长 200 字符）。可选：remark（备注，最长 1000）、dueDate（ISO 8601）、" +
                          "objective（可验证目标）、content（正文 markdown）、acceptance（验收判据，每行一条）、verification（验证命令，每行一条）、" +
                          "allowedScope / forbiddenScope（允许/禁止改动范围）、priority（1=P1 2=P2 3=P3）、assignee（下发对象）、" +
                          "projectPath（项目路径，支持 D:\\proj、D:/proj、/d/proj、/mnt/d/proj 写法，服务端归一后关联）。",
            ParametersJsonSchema = TodoToolSchemas.CreateTodo
        });

        ToolExtensions.Add(new ListTodosToolFunction(services)
        {
            Id = "todotracker.tool.list_todos",
            Name = "list_todos",
            PluginId = pluginId,
            Description = "列出待办/任务，支持过滤与分页。可选：status（Pending/Completed）、stage（Draft/Ready/Dispatched/Running/Blocked/Review/Done/Cancelled）、" +
                          "projectId（0=全部）、q（关键字）、page（默认 1）、pageSize（默认 20，最大 100）。",
            ParametersJsonSchema = TodoToolSchemas.ListTodos
        });

        ToolExtensions.Add(new CompleteTodoToolFunction(services)
        {
            Id = "todotracker.tool.complete_todo",
            Name = "complete_todo",
            PluginId = pluginId,
            Description = "标记指定待办为已完成（同步下发阶段到 Done）。必填：id（待办 ID）。",
            ParametersJsonSchema = TodoToolSchemas.ById
        });

        // === PILOT-054 新增：agent 侧的任务领取 / 内容读取 / 执行回报 / 状态流转 ===
        ToolExtensions.Add(new GetAgentTaskToolFunction(services)
        {
            Id = "todotracker.tool.get_agent_task",
            Name = "get_agent_task",
            PluginId = pluginId,
            Description = "按 taskKey 读取一条任务的完整下发内容：目标、正文、允许/禁止范围、验收判据、验证命令、" +
                          "项目根（agent 的工作目录）、已有执行记录条数。必填：taskKey。",
            ParametersJsonSchema = TodoToolSchemas.ByTaskKey
        });

        ToolExtensions.Add(new ClaimAgentTaskToolFunction(services)
        {
            Id = "todotracker.tool.claim_agent_task",
            Name = "claim_agent_task",
            PluginId = pluginId,
            Description = "领取下一条处于 Dispatched（已下发）的任务并原子置为 Running（执行中），同时留一条执行记录。" +
                          "可选：assignee（按下发对象过滤）、projectId（0=全部）。无可领任务时返回 data:null 且 next=null。",
            ParametersJsonSchema = TodoToolSchemas.ClaimTask
        });

        ToolExtensions.Add(new AppendTaskExecutionToolFunction(services)
        {
            Id = "todotracker.tool.append_task_execution",
            Name = "append_task_execution",
            PluginId = pluginId,
            Description = "给任务追加一条执行记录（append-only 台账）。必填：taskKey、action（做了什么操作，一句话）。" +
                          "可选：result（什么结果）、detail（操作明细）、filesChanged（改了哪些文件，数组 [{path,change}] 或一行一个路径的文本）、" +
                          "verification（跑了什么验证 + 结果）、risks（风险）、residuals（遗留）、evidence（证据）、nextStep（下一步）、" +
                          "elapsedMs（耗时）、stageTo（同批流转到的阶段：Running/Blocked/Review/Done；进 Blocked 必须给 blockReason）。",
            ParametersJsonSchema = TodoToolSchemas.AppendExecution
        });

        ToolExtensions.Add(new UpdateTaskStageToolFunction(services)
        {
            Id = "todotracker.tool.update_task_stage",
            Name = "update_task_stage",
            PluginId = pluginId,
            Description = "流转任务阶段（合法边：Draft→Ready→Dispatched→Running→Review→Done，中途可 Blocked，终态可重开）。" +
                          "必填：taskKey、stage。可选：reason（说明，进执行记录留痕）、blockReason（进 Blocked 时必填）。" +
                          "非法流转返回可达目标清单。",
            ParametersJsonSchema = TodoToolSchemas.UpdateStage
        });

        ToolExtensions.Add(new DispatchTaskToolFunction(services)
        {
            Id = "todotracker.tool.dispatch_task",
            Name = "dispatch_task",
            PluginId = pluginId,
            Description = "下发一条任务（校验必填齐备 → 阶段置 Dispatched → 留痕），并返回可直接交给 agent 的提示词。" +
                          "必填：taskKey。可选：assignee（下发对象）。缺 objective/content/acceptance/verification 任一项即拒绝并点名。",
            ParametersJsonSchema = TodoToolSchemas.DispatchTask
        });

        XTrace.Log.Debug("待办追踪插件已注册 {0} 个 AI 工具函数扩展点", ToolExtensions.Count);
    }
}

/// <summary>
/// 待办状态常量与映射辅助（旧二元状态，Home 面板与既有 e2e 在用）。
/// 新的下发阶段状态机见 <see cref="Services.TodoStage"/>；两者关系：Status 由 Stage 派生（Stage&gt;=Done ⇒ Completed）。
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
