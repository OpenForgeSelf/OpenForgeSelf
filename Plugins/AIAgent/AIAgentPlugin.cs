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

        // 项目工作区服务：单一共享单例实例。
        // 关键修复：必须同时注册进「宿主 DI（services，供 ProjectController 构造注入）」与「ctx（供文件工具运行时 ctx.Get）」，
        // 且用同一个实例——否则设目录的实例 ≠ 工具读到的实例，目录白设。
        // 旧写法 services.AddSingleton<...,ProjectWorkspaceService>() 只进 services，ctx.Get 解析不到该注册
        // （ctx.Get 与 ctx.Get<IServiceCollection>() 返回的 IServiceCollection 不是同一存储），导致工具抛「项目工作区服务不可用」。
        // 正确模式：显式 new 单例实例，services 注册实例（宿主 DI 可见）+ ctx.Register 实例（运行时 ctx.Get 可见），二者同一实例。
        var workspace = new ProjectWorkspaceService();
        services?.AddSingleton<IProjectWorkspaceService>(workspace);
        ctx.Register<IProjectWorkspaceService>(workspace);

        services?.AddSingleton<IProjectSkillScannerService, ProjectSkillScannerService>();
        services?.AddSingleton<IProjectRegistryService, ProjectRegistryService>();
        services?.AddScoped<IAIAgentService, AIAgentService>();
        // 对话模型解析（有序链：显式指定 → 宿主默认模型 → 启用目录首个 → 明确失败）：
        // 内置执行与 Run 恢复共用，替代原先硬编码的 ornith-1.0-9b（该模型不在模型表中）。
        services?.AddScoped<IChatModelResolver, ChatModelResolver>();
        services?.AddScoped<IPluginMessageService, PluginMessageService>();
        // B4（040）：会话日志 → AIChatMessage 只读视图的投影同步器（调用方返回前 await，勿改 Singleton）
        services?.AddScoped<AIAgentProjectionService>();
        // 单例（缺陷召回 #2，qa e2e 暴露）：本类是「内存字典视图 + DB 持久化」的注册表，并发字典线程安全；
        // 插件 4 处 eager scope（advisor/builtin/schedule/dispatch）与 UI 请求作用域必须共享同一实例，
        // 否则 UI 新建 Agent 对派发/内置执行不可见（多视图分裂）。原 AddScoped 被 eager 捕获=俘虏依赖。
        services?.AddSingleton<IAgentRegistryService, AgentRegistryService>();
        // B5（2026-11-02-b5-legacy-cleanup，lead 拍板=删除）：AgentCoordinator/AgentExecutor 启发式旁路
        // 已删除（零 UI/插件/IM 消费者 + ad-hoc 循环无审批泵；裁决留痕 agenthub-adjudication.md）。
        services?.AddScoped<IProactivePlanningService, ProactivePlanningService>();
        services?.AddScoped<IWorkflowPlannerService, WorkflowPlannerService>();
        services?.AddScoped<IToolSelectorService, ToolSelectorService>();
        // 补注册 AI 工作流助手与 AI 重试顾问（供插件内部构造注入）。
        services?.AddScoped<IAIWorkflowAssistant, AIWorkflowAssistant>();
        services?.AddScoped<IWorkflowAIAdvisor, AIWorkflowAdvisor>();

        // 计划驱动执行引擎（029，T016）：RunFlowToolSet 承载 submit_plan/complete_step/request_help
        // 三特殊工具，供 PlanGeneratorService（规划）/ RunOrchestratorService（步骤 turn）经
        // AgentOptions.ExtraTools 显式挂载（R4；B7 统一循环后步骤循环本体=统一状态机（B5 起唯一运行时 MafTaskLoopAgent），StepRunLoopService 已删）。
        var runFlowTools = new RunFlowToolSet(pluginId);
        services?.AddSingleton(runFlowTools);
        services?.AddScoped<IPlanGeneratorService, PlanGeneratorService>();
        services?.AddScoped<IRunOrchestratorService, RunOrchestratorService>();
        // 内置执行接缝（供 todo-tracker 等消费方经 ctx.Get<IBuiltInAgentExecution>() 把任务交给本工具内置 AI Agent）
        services?.AddScoped<IBuiltInAgentExecution, BuiltInAgentExecutionService>();
        // 调度器到点触发本机 MAF Run（同进程，不走 HTTP）。实现保持薄，不改审批挂起。
        services?.AddScoped<IScheduledAgentRunTrigger, ScheduledAgentRunTrigger>();
        // IM 网关等消费方派任务到本机 MAF Run 的接缝（PILOT-079）：同进程后台驱动，不走 HTTP。
        services?.AddScoped<IAgentRunDispatch, AgentRunDispatchService>();

        // eager 提供 IWorkflowAIAdvisor（调研 §5.6 裁决 F：每上下文 eager 单例，非懒解析委托）：
        // 经子容器 scope 解析其依赖链（AIWorkflowAdvisor → IAIWorkflowAssistant → IAIAgentService/IToolSelectorService + IContext），
        // 再 ctx.Register<IWorkflowAIAdvisor>(实例) 写入 root 共享服务表，供兄弟插件（WorkflowEngine）经 ctx.Get 消费。
        // ===== 缺陷单 #3：共享插件级 provider + eager 实例单例注册（全局单视图） =====
        // 原 4 个 eager 块各自 services.BuildServiceProvider() = 4 个独立 root provider + 宿主主 provider
        // = 5 个 per-provider 单例注册表（各 DB 加载一次、各持独立字典）——运行期新建 Agent 只落创建侧，
        // 控制器/IM 派发/内置执行互不可见。现共享同一个 provider（eagerScope 承载 scoped 依赖图存活），
        // eager 产出以实例单例注册回 services（last-wins 覆盖同接口类型注册）→ 宿主主 provider 解析与
        // ctx.Get 返回同一实例 → 全局单视图。AgentRegistryService 的 builtin seed 行为不变（ctor 仍 seed）。
        IServiceProvider? pluginProvider = null;
        IServiceScope? eagerScope = null;
        if (services != null)
        {
            pluginProvider = services.BuildServiceProvider();
            eagerScope = pluginProvider.CreateScope();
            // 卸载时释放 eager 依赖图（等义于原 4 个独立 scope effect；provider 随插件回收）
            ctx.Effect(() => eagerScope);
            var advisor = eagerScope.ServiceProvider.GetRequiredService<IWorkflowAIAdvisor>();
            ctx.Register<IWorkflowAIAdvisor>(advisor);
            services.AddSingleton<IAgentRegistryService>(eagerScope.ServiceProvider.GetRequiredService<IAgentRegistryService>());
            services.AddSingleton<IWorkflowAIAdvisor>(advisor);
        }

        RegisterMenuExtensions(pluginId);
        RegisterToolFunctionExtensions(pluginId, ctx, runFlowTools);

        // 注册跨插件聊天补全契约（供 im-gateway 等消费方经 ctx.Get<IChatCompletion>() 调用）
        services?.RegisterChatCompletion(ctx);

        // B5（041）：Agent 运行时注册表（turn/step 状态机）。
        // 会话 Agent 跨请求存活（连续对话复用），故注册表本身是单例，并自建 root provider 开 scope：
        // Agent 依赖 IAIAgentService（scoped），scope 由 ScopedAgent 持有、注销时一起释放，
        // 避免「请求结束依赖被回收 → Agent 悬空」。注册表双注册（宿主 DI + ctx 共享服务表），与 IChatCompletion 同款。
        if (services != null && pluginProvider != null)
        {
            // 缺陷单 #3：工厂并入共享 provider——会话 Agent 解析 IAIAgentService 触达的注册表单例与其余消费者同视图
            // （原独立 BuildServiceProvider 会产出第 6 份实例）。
            var registry = new AgentRuntimeRegistry((sessionId, options) =>
            {
                var agentScope = pluginProvider.CreateScope();
                var agentService = agentScope.ServiceProvider.GetRequiredService<IAIAgentService>();
                return new ScopedAgent(agentService.CreateAgent(sessionId, options), agentScope);
            });

            services.AddSingleton<IAgentRegistry>(registry);
            ctx.Register<IAgentRegistry>(registry);
            XTrace.Log.Info("[AIAgentPlugin] 已注册 IAgentRegistry（Agent 运行时注册表，B5/041）");
        }

        // eager 提供 IBuiltInAgentExecution（内置执行接缝）：经子容器 scope 解析其依赖链
        // （BuiltInAgentExecutionService → IRunOrchestratorService/IAgentRegistryService），
        // 再 ctx.Register<IBuiltInAgentExecution>(实例) 写入 root 共享服务表，供 todo-tracker 等消费。
        if (services != null && eagerScope != null)
        {
            var builtinExecutor = eagerScope.ServiceProvider.GetRequiredService<IBuiltInAgentExecution>();
            ctx.Register<IBuiltInAgentExecution>(builtinExecutor);
            services.AddSingleton<IBuiltInAgentExecution>(builtinExecutor);
            XTrace.Log.Info("[AIAgentPlugin] 已注册 IBuiltInAgentExecution（内置 AI Agent 执行接缝）");
        }

        // eager 提供 IScheduledAgentRunTrigger：调度器经 ctx.Get 到点创建 Run。
        // 与内置执行同一 lifetime（插件存活的 scope），卸载时 effect 先摘共享表再释放 scope。
        if (services != null && eagerScope != null)
        {
            var scheduleTrigger = eagerScope.ServiceProvider.GetRequiredService<IScheduledAgentRunTrigger>();
            ctx.Register<IScheduledAgentRunTrigger>(scheduleTrigger);
            services.AddSingleton<IScheduledAgentRunTrigger>(scheduleTrigger);
            XTrace.Log.Info("[AIAgentPlugin] 已注册 IScheduledAgentRunTrigger（本机计划任务触发 MAF Run）");
        }

        // eager 提供 IAgentRunDispatch（PILOT-079）：IM 网关经 ctx.Get 把消息派成 MAF Run。
        // 与 IScheduledAgentRunTrigger 同一 lifetime（插件存活的 scope），卸载时 effect 先摘共享表再释放 scope。
        if (services != null && eagerScope != null)
        {
            var dispatch = eagerScope.ServiceProvider.GetRequiredService<IAgentRunDispatch>();
            ctx.Register<IAgentRunDispatch>(dispatch);
            services.AddSingleton<IAgentRunDispatch>(dispatch);
            XTrace.Log.Info("[AIAgentPlugin] 已注册 IAgentRunDispatch（IM 网关派任务接缝，PILOT-079）");
        }

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

    private void RegisterToolFunctionExtensions(string pluginId, IContext ctx, RunFlowToolSet runFlowTools)
    {
        XTrace.Log.Debug("[AIAgentPlugin] 注册AI工具函数扩展点");

        // 2026-10-28 工具命名空间统一（ADR-004）：本插件只保留 AI 编排专属工具。
        // 公共工具（universal_tool/search_tools/run_terminal_command/文件三件套/get_current_time/calculate）
        // 迁至 mcp-center（见 Plugins/McpCenter/Services/PublicTools/）；与 WorkflowEngine 重复的
        // execute_workflow/get_workflow_status/list_workflows 删除（归属 WorkflowEngine）。
        // 保留名豁免：finish / submit_plan / complete_step / request_help（协议面）；
        // plan_workflow 加 aiagent_ 前缀。
        ToolExtensions.Add(new PlanWorkflowToolFunction(pluginId, ctx));
        // 计划驱动运行流特殊工具（029 R4）：注册进 ToolRegistry 供引擎执行兜底，
        // 但 ResolveOwnToolDefinitions 按名排除 → 不进 FreeLoop，只经 extraTools 显式挂载。
        ToolExtensions.Add(runFlowTools.SubmitPlan);
        ToolExtensions.Add(runFlowTools.CompleteStep);
        ToolExtensions.Add(runFlowTools.RequestHelp);

        XTrace.Log.Debug("[AIAgentPlugin] AI工具函数扩展点注册完成，共 {0} 个工具函数", ToolExtensions.Count);
    }
}

/// <summary>
/// 工作流服务解析助手（跨插件软依赖）。
/// workflow-engine 插件在 <c>Apply</c> 内把 IWorkflowService 实例 <c>ctx.Register</c> 进插件上下文共享表，
/// 兄弟插件（本插件）经 IContext（即工具持有的 IServiceProvider）解析；解析不到时返回 null，
/// 由调用方给出可读错误，而不是抛「No service for type 'IWorkflowService' has been registered」
/// ——后者在界面上表现为含糊的「服务没激活」，无法定位。
/// </summary>
internal static class WorkflowServiceResolver
{
    public static IWorkflowService? Resolve(IServiceProvider? provider)
        => provider?.GetService(typeof(IWorkflowService)) as IWorkflowService;

    /// <summary>解析失败时的统一错误 JSON（工具返回给模型的可见结果）。</summary>
    public static string UnavailableJson()
        => JsonSerializer.Serialize(new
        {
            success = false,
            error = "工作流服务不可用：workflow-engine 插件未启用，或未提供 IWorkflowService 契约（请在插件管理中启用「工作流引擎」后重试）"
        }, ForgeJson.Default);
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

public class PlanWorkflowToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "aiagent.plan_workflow";
    public string Name => "aiagent_plan_workflow";
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
                return JsonSerializer.Serialize(new { success = false, error = "用户需求描述不能为空" }, ForgeJson.Default);
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" }, ForgeJson.Default);
            }

            using var scope = _serviceProvider.CreateHostScope();
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

            var json = JsonSerializer.Serialize(response, ForgeJson.Default);

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

            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, ForgeJson.Default);
        }
    }

}
