using System.Security.Claims;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Authentication;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Data;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Services;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Services.CostScope;
using Scalar.AspNetCore;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Providers;
using ForgeSelf.Api.Services.Skills;
using ForgeSelf.Api.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NewLife.Log;

namespace ForgeSelf.Api;

/// <summary>
/// 构建并配置 WebApplication 的静态辅助类。
/// 封装了所有初始化逻辑（XCode、CORS、认证、插件、SignalR、DI 注册等），
/// 供控制台模式（Program.cs）和 Windows 服务模式（WindowsService.cs）共用。
/// </summary>
public static class AppBuilder
{
    /// <summary>
    /// 创建并完全配置一个 <see cref="WebApplication"/> 实例。
    /// 调用方负责启动（Run / StartAsync / RunAsync）和释放。
    /// </summary>
    /// <param name="args">命令行参数，传递给 WebApplication.CreateBuilder。</param>
    /// <returns>已配置好所有中间件和服务的 WebApplication，尚未启动。</returns>
    public static WebApplication CreateWebApplication(string[] args)
    {
        // dev 模式总闸（PILOT-plugin-dev-experience）：必须最早初始化，此后所有 dev 分支据此短路。
        // 未显式启用（FORGESELF_DEV_MODE=1 / --dev）时全部为 false，宿主行为与改动前一致。
        Plugins.Dev.DevMode.Initialize(args);

        // 显式固定 WebRoot 为「程序所在目录下的 wwwroot」，兼容开发期项目目录 wwwroot。
        // 必须在 CreateBuilder 阶段通过 WebApplicationOptions 设定：若在 CreateBuilder 之后调用
        // builder.WebHost.UseWebRoot，ASP.NET Core 会抛 NotSupportedException
        // （"Changing the host configuration using WebApplicationBuilder.WebHost is not supported"）。
        // 程序目录基准（2026-09-28 批次2.1，QQNT 目录结构）＝ 入口程序集（ForgeSelf.Api.dll）所在目录：
        //   扁平部署（publish/）＝ BaseDirectory；QQNT 部署（安装根 + versions/<ver>/ 业务层）＝ 业务层目录。
        // 从其他文件夹启动（如直接运行 publish/ForgeSelf.exe）时，ContentRootPath/CWD 会偏离，
        // 默认 WebRootPath 解析可能找不到 wwwroot（2026-08-14 实战坑）。
        // 候选基准：程序目录 + 当前工作目录（dotnet run 开发期 CWD=项目目录，wwwroot 建于此）。
        var programDir = Path.GetDirectoryName(typeof(AppBuilder).Assembly.Location) ?? AppContext.BaseDirectory;
        var webRoot = ResolveWebRootPath(programDir, Directory.GetCurrentDirectory());
        // ContentRoot 同理指向业务层目录（appsettings.json 基准；QQNT 部署下配置文件在 versions/<ver>/）。
        // 仅当业务层目录确实存在 appsettings.json 时才强制，否则保持 CWD（开发期项目根），避免破坏 dotnet run。
        var contentRoot = File.Exists(Path.Combine(programDir, "appsettings.json"))
            ? programDir
            : Directory.GetCurrentDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            WebRootPath = webRoot,
            ContentRootPath = contentRoot,
        });

        // 统一数据根服务：按运行形态解析（开发→程序目录 data/，否则→用户主目录 ~/.forgeself）
        var dataLocation = new DataLocationService(builder.Environment);
        builder.Services.AddSingleton<IDataLocationService>(dataLocation);

        // B9-6（R5）：spill 阈值从配置中心播种（ForgeSetting.SpillThresholdBytes，默认 32 KiB）
        SessionProjectionSpillSeed();

        void SessionProjectionSpillSeed()
        {
            try
            {
                Services.SessionEventProjection.SpillThresholdBytes = Models.ForgeSetting.Current.SpillThresholdBytes;
            }
            catch (Exception ex)
            {
                // 配置读取失败不影响启动：保持默认 32 KiB（与 B9-5 降级策略一致）
                NewLife.Log.XTrace.Log.Warn("[AppBuilder] 读取 SpillThresholdBytes 失败（保持默认 32KiB）: {0}", ex.Message);
            }
        }

        // 日志目录统一（输入37 目录命名小写）：NewLife 默认 Log（程序目录下）→ 数据根/log（外置 + 全小写）。
        // NewLife.Setting.LogPath 支持绝对路径（TextFileLog 按目录/文件路径解析），须在首次日志写入前设置。
        NewLife.Setting.Current.LogPath = Path.Combine(dataLocation.GetHostDataDirectory(), "log");
        XTrace.LogPath = Path.Combine(dataLocation.GetHostDataDirectory(), "log");
        NewLife.Setting.Current.Save();

        // dev-only：包装 XTrace.Log 为带插件维度前缀的装饰器（[plugin:<id>] 前缀 + 插件分文件），
        // 并放开日志级别到 Debug（插件里大量 XTrace.Log.Debug 在 Info 级别下全被丢弃，dev 定位需要它们）。
        // Production 不包装、保持 Info —— 日志路径与改动前逐字节一致。
        if (Plugins.Dev.DevMode.Enabled)
        {
            XTrace.Log = new Plugins.Dev.PluginTaggedLog(XTrace.Log);
            Plugins.Dev.PluginShadowCopy.CleanupUnlocked();
        }

        XTrace.Log.Level = Plugins.Dev.DevMode.Enabled ? NewLife.Log.LogLevel.Debug : NewLife.Log.LogLevel.Info;
        XTrace.Log.Info("运行时数据根目录: {0}", dataLocation.GetHostDataDirectory());

        // 统一所有 NewLife Config<T> 配置文件落盘位置（XCode/Core/Agent/项目自有等），
        // 必须早于 AddXCode（其内部访问 XCodeSetting.Current）及任何 .Current 访问，
        // 避免配置文件散落到程序目录/输出目录。
        var configRoot = Path.Combine(dataLocation.GetHostDataDirectory(), "config");
        ConfigUnifier.UnifyAllConfigFiles(configRoot);
        XTrace.Log.Info("配置文件统一目录: {0}", configRoot);

        // 启动期端口覆盖（e2e / 多实例并行隔离）：FORGESELF_PORT 环境变量 ＞ --server-port 命令行。
        // 覆盖后落盘 ForgeSetting.config，使 ApplicationRestartService 重启（不传 env）仍读到同一端口，
        // 保证「env 覆盖 / config 落盘 / 重启读取」三方一致；未提供覆盖时零副作用。
        // 须在 ConfigUnifier 之后调用，确保 Save() 落到统一的 {数据根}/config 目录，
        // 且后续 TrayIconManager 注册(263) / StartTrayIcon(272) / 端口绑定(524) 均读到覆盖值。
        StartupPortResolver.ResolveAndApply(args);

        builder.Services.AddXCode(builder.Configuration, dataLocation.GetHostDataDirectory());

        // 项目工作区宿主实现（L1 能力接缝 IProjectRegistry）：seed 进插件 root 上下文（常驻）。
        // 构造期仅凭宿主数据根定位旧共享 JSON 迁移源，不触碰数据库连接（惰性迁移在首次访问时触发）。
        builder.Services.AddSingleton<IProjectRegistry>(sp =>
        {
            var loc = sp.GetRequiredService<IDataLocationService>();
            return new HostProjectRegistry(loc.GetHostDataDirectory());
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        builder.Services.AddControllers().AddApplicationPart(typeof(AppBuilder).Assembly);

        builder.Services.AddEndpointsApiExplorer();

        // 注册 OpenAPI 文档生成服务（003-api-server-settings converge）
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
                {
                    Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = Microsoft.OpenApi.ParameterLocation.Header,
                    BearerFormat = "sk-...",
                    Description = "输入你的 API 密钥（格式：sk-...）"
                };
                return Task.CompletedTask;
            });
        });

        builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
        // 平台级共享事件总线（027-cordis-kernel）：工具执行管道 tools/* 拦截点（pre-execute / execute / post-execute）
        builder.Services.AddSingleton<IEventBus>(new EventBus());
        // B8（042 工具管线）：单调守卫注册表 + 审批服务（默认占位实现 fail-closed，真实审批服务就绪后替换）
        builder.Services.AddSingleton<IToolGuardRegistry, ToolGuardRegistry>();
        builder.Services.AddSingleton<IApprovalService, NoopApprovalService>();
        builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
        // IMcpService 已随宿主 mcp-tools 迁入 McpCenter 插件（034 v2.0.0），由插件 Apply 注册进插件子 provider
        builder.Services.AddSingleton<ICronParser, CronParser>();
        builder.Services.AddSingleton<IRuntimeDetector, RuntimeDetector>();
        builder.Services.AddSingleton<ISkillsService, SkillsService>();
        builder.Services.AddHttpClient<IAIService, AIService>();
        // P4 会话/LLM 接缝接线：注册真实实现（消费者经接缝调用，替换 Provider 零改动）
        // B2（040）：会话事件日志落 Sqlite/XCode（持久化真相源）；
        // InMemorySessionStore 退为测试替身，不再注册进生产 DI（接缝不变，替换零改动）。
        builder.Services.AddSingleton<ISessionStore>(_ => new PersistentSessionStore());
        // B4（040）：ChatMessage 只读投影同步器。
        // 它依赖 ILogService（Scoped），而它自己被 Scoped 的控制器（ChatController）注入，
        // 所以必须注册为 Scoped：若注册成 Singleton，DI 会把 Scoped 的 ILogService 拖进根容器（ captive dependency ），
        // 运行时要么解析失败、要么让 Scoped 依赖被单例长期持有。它消费的 ISessionStore 本身是 Singleton，
        // 由 Scoped 服务依赖 Singleton 是安全的（方向相反才危险）。
        builder.Services.AddScoped<SessionProjectionService>();
        builder.Services.AddScoped<ILlmRuntime, AIServiceLlmRuntime>();
        // B5（041）：IAgentLoop/InMemoryAgentLoop 已删除（旧 P4 接缝，生产消费方 0），
        // Agent 循环改由插件侧 IAgentRegistry + ReactLoopAgent 承载（架构师 §2-B5）。
        // B6（040 §2.5）：收件箱升级为持久投影——followup/steer/inject 一律落 agent/inbox/spliced 事件，
        // 从日志重放派生待处理集（无活 Agent 时 UI 也能 Peek 出待办）；
        // InMemoryInbox 退为测试替身，不再注册进生产 DI。依赖的 ISessionStore 是 Singleton，Singleton 安全。
        builder.Services.AddSingleton<IInbox, PersistentInbox>();

        builder.Services.AddScoped<ILogService, LogService>();
        builder.Services.AddScoped<IMessageService, MessageService>();
        builder.Services.AddScoped<IUsageStatsService, UsageStatsService>();
        // A3b（PILOT-033）：成本/trace/聚合插件取宿主数据的唯一合法通道（只读取数，不含业务）
        builder.Services.AddScoped<ITurnTelemetryQuery, TurnTelemetryQueryService>();
        builder.Services.AddScoped<IWorkflowUsageService, WorkflowUsageService>();
        builder.Services.AddScoped<IWorkflowRecommendationService, WorkflowRecommendationService>();
        builder.Services.AddScoped<IChatTurnService, ChatTurnService>();
        builder.Services.AddScoped<IChatSessionService, ChatSessionService>();
        builder.Services.AddSingleton<IWebSocketBroadcaster, WebSocketBroadcaster>();
        builder.Services.AddScoped<IChatTurnStreamRecorder, ChatTurnStreamRecorder>();

        // 机器派生密钥提供器（030 认证体系升级）：进程内单例，PBKDF2 只派生一次
        builder.Services.AddSingleton<IMachineKeyProvider, MachineKeyProvider>();

        // AI Provider 配置数据库化（001-ai-provider-config-db）
        builder.Services.AddSingleton<ISecretEncryptionService, AesSecretEncryptionService>();
        builder.Services.AddScoped<IAIProviderRepository, AIProviderRepository>();
        builder.Services.AddScoped<IAIProviderService, AIProviderService>();
        builder.Services.AddScoped<IAIModelService, AIModelService>();

        // API 服务器密钥管理（003-api-server-settings）
        builder.Services.AddSingleton<ApiServerKeyService>();

        // API 子密钥管理（030 认证体系升级）：Scoped，请求内共享（内部依赖单例加密服务）
        builder.Services.AddScoped<ApiKeyService>();

        // 端口配置管理（009-web-port-token-security）- 基于配置文件
        builder.Services.AddSingleton<IPortConfigurationService, PortConfigurationService>();
        builder.Services.AddSingleton<IApplicationRestartService, ApplicationRestartService>();
        builder.Services.AddSingleton<IPortAvailabilityService, PortAvailabilityService>();

        // ── 托盘图标 + 服务管理 + 自动更新服务注册（008-tray-service-autoupdate） ──

        // 绑定配置节
        builder.Services.Configure<ServiceConfig>(builder.Configuration.GetSection("Service"));
        builder.Services.Configure<UpdateConfig>(builder.Configuration.GetSection("Update"));

        // 运行时可变更新配置（2026-09-27）：appsettings "Update" 节为基座，
        // 设置页修改后落盘到 {数据根}/config/update-settings.json（下次启动该文件优先）。
        // 与 UpdateChecker / UpdateController 共享同一 UpdateConfig 实例引用，改配置无需重启。
        var updateConfigInitial = builder.Configuration.GetSection("Update").Get<UpdateConfig>()
            ?? new UpdateConfig();
        var updateSettingsFile = Path.Combine(
            dataLocation.GetHostDataDirectory(), "config", "update-settings.json");
        builder.Services.AddSingleton(new UpdateSettingsService(updateConfigInitial, updateSettingsFile));
        // 插件更新源配置（2026-09-28，输入27）：同构 UpdateSettingsService，
        // 设置页（插件管理 tab）修改后落盘到 {数据根}/config/plugin-update-settings.json，
        // PluginVersionService / PluginController 共享同一实例引用，改配置无需重启宿主。
        var pluginUpdateSettingsFile = Path.Combine(
            dataLocation.GetHostDataDirectory(), "config", "plugin-update-settings.json");
        builder.Services.AddSingleton(new PluginUpdateSettingsService(new Models.Plugins.PluginUpdateSettings(), pluginUpdateSettingsFile));

        // ServiceManager — 封装 Windows 服务安装/卸载/状态检测
        builder.Services.AddSingleton<IServiceManager>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ServiceConfig>>().Value;
            return new ServiceManager(config);
        });

        // UpdateChecker — 封装版本查询与下载（stardust / GitHub Releases / 本地目录 三 provider）
        builder.Services.AddSingleton<UpdateChecker>(sp =>
        {
            var settings = sp.GetRequiredService<UpdateSettingsService>();
            // Timeout 交给各请求自己的 CancellationTokenSource（check/download 超时不同量级）；
            // HttpClient 级超时若按 CheckTimeoutSeconds 配置，会把大文件下载在十几秒处掐断。
            var httpClient = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            return new UpdateChecker(settings.Current, httpClient, currentVersion: UpdateChecker.GetCurrentVersion());
        });

        // StagedUpdateService — 暂存式更新编排（spec 036：下载→校验→解压 staged→agent 重启并更新）
        builder.Services.AddSingleton<StagedUpdateService>(sp =>
        {
            var updateChecker = sp.GetRequiredService<UpdateChecker>();
            var lifetime = sp.GetRequiredService<IHostApplicationLifetime>();
            return new StagedUpdateService(updateChecker, lifetime);
        });

        // UpdateService — 更新流程编排（008 冻结，2026-09-28：仅保留注册防 WindowsService 模式引用，不再维护；升级统一走上方 036）
        builder.Services.AddSingleton<UpdateService>(sp =>
        {
            var updateChecker = sp.GetRequiredService<UpdateChecker>();
            var serviceManager = sp.GetRequiredService<IServiceManager>();
            var updateConfig = sp.GetRequiredService<UpdateSettingsService>().Current;
            var serviceConfig = sp.GetRequiredService<IOptions<ServiceConfig>>().Value;
            var httpClient = new HttpClient();
            return new UpdateService(updateChecker, serviceManager, updateConfig, serviceConfig, httpClient);
        });

        // TrayIconManager — 系统托盘图标（延迟初始化，通过 StartTrayIcon 在控制台模式启动）
        builder.Services.AddSingleton<TrayIconManager>(sp =>
        {
            var serviceManager = sp.GetRequiredService<IServiceManager>();
            // 令牌解析器：惰性调用，点击托盘菜单时实时取，主密钥重新生成后自动拿到新值
            var keyService = sp.GetRequiredService<ApiServerKeyService>();
            var port = ForgeSetting.Current.PortNumber;
            return new TrayIconManager(serviceManager, port, mainPageTokenResolver: () => keyService.GetActiveKeyPlain());
        });

        // ── 结束 008-tray-service-autoupdate 注册 ──

        // Bearer API 密钥认证（003-api-server-settings）
        builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, null);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("ApiKeyPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add(ApiKeyAuthenticationHandler.SchemeName);
                policy.RequireAuthenticatedUser();
            });
        });

        // AI 网关服务：注册表单例初始为空，启动阶段从数据库重载
        builder.Services.AddSingleton<AIProviderRegistry>(sp => new AIProviderRegistry());
        // 契约形态（Abstractions）：供插件经 ctx.Get<IAIProviderRegistry>() 消费，须与上者为同一实例。
        builder.Services.AddSingleton<IAIProviderRegistry>(sp => sp.GetRequiredService<AIProviderRegistry>());

        // 图片识别结果本地缓存（统一 AI 网关多模态处理用）：按会话 id 分文件夹，存于 {数据根}/ImageRecognitionCache
        var imageCacheRoot = Path.Combine(dataLocation.GetHostDataDirectory(), "ImageRecognitionCache");
        builder.Services.AddSingleton<IImageRecognitionCache>(new LocalFileImageRecognitionCache(imageCacheRoot));

        // 插件可变 MS DI 容器（Option A）：先注册注册表单例，使 PluginManager 可构造注入同一份实例。
        var pluginServiceRegistry = new PluginServiceRegistry();
        builder.Services.AddSingleton<IPluginServiceRegistry>(pluginServiceRegistry);

        builder.Services.AddPluginManager();

        // 插件根（两路，2026-10-04 输入18）：
        //   第一路＝内置根，随宿主版本发布，位于业务层旁边 versions/<ver>/plugins（--plugins-dir / FORGESELF_PLUGINS_DIR 可覆盖，dev 场景指向源码 Plugins/）；
        //   第二路＝数据目录根 {数据根}/plugins（用户自行安装的插件包；与「插件数据」同树，扫描只认带 plugin.json 的子目录）。
        // 同 Id 跨根时由版本号裁决（内置在前，版本更高者生效），来源逐条写进发现日志。
        var pluginsPath = Plugins.Dev.DevMode.PluginsDirectoryOverride
                          ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!string.Equals(pluginsPath, Path.Combine(AppContext.BaseDirectory, "plugins"), StringComparison.OrdinalIgnoreCase))
        {
            XTrace.Log.Info("插件根目录已覆盖: {0}", pluginsPath);
        }

        PluginManager pluginManager;
        using (var bootstrap = builder.Services.BuildServiceProvider())
        {
            pluginManager = bootstrap.GetRequiredService<PluginManager>();
            pluginManager.SetPluginsDirectory(pluginsPath);
            pluginManager.AddPluginRoot(
                Path.Combine(dataLocation.GetHostDataDirectory(), IDataLocationService.PluginDataRootName), "数据目录");
            // 提前把数据位置服务 seed 进插件根上下文：RegisterAllServices 会立刻触发插件 Apply，
            // 而 Apply 内就要 ctx.GetPluginDataDirectory()；ProvideHostServices 却要等 Build 之后
            // 才能解析 DI —— 不提前 seed，用到数据目录的插件会整体注册失败。
            pluginManager.ProvideHostService(typeof(IDataLocationService), dataLocation);
            pluginManager.RegisterAllServices(builder.Services);
            // P3 事件总线贯穿：注入平台 IEventBus 单例，插件加载/卸载时发 plugin/loaded / plugin/unloaded。
            pluginManager.EventBus = bootstrap.GetService<ForgeSelf.Core.IEventBus>();
        }
        builder.Services.AddPluginManager(pluginManager);

        // 把插件服务类型转发到可变容器：宿主按需经 registry.Resolve 解析；插件卸载后即解析失败。
        foreach (var descriptor in pluginServiceRegistry.CollectForwardDescriptors())
        {
            builder.Services.Add(descriptor);
        }

        // 插件感知控制器激活器：插件控制器从实时注册表解析（经其子 provider 注入插件服务 + IContext），
        // 绕开热更新后宿主转发描述符类型身份陈旧导致的插件端点 500；宿主控制器回退默认激活。
        builder.Services.AddSingleton<Microsoft.AspNetCore.Mvc.Controllers.IControllerActivator,
            ForgeSelf.Api.Plugins.Services.PluginAwareControllerActivator>();

        // 插件文件级热更新监听已移除（2026-09-24 一刀切）：自动 reload 会绕过版本化控制、
        // 造成「覆盖式热重载」与 side-by-side 布局冲突。插件生效一律走版本化显式更新
        // （POST /api/plugin/update/{id}）或冷启动，见 docs/02-features/035-plugin-versioned-layout.md。

        builder.Services.AddSignalR();

        // 动态端点移除：ApplicationPartManager 的 ApplicationParts 集合变更不会自动触发 ActionDescriptor 刷新
        // （框架无内置 ChangeToken 通知），注册自定义 IActionDescriptorChangeProvider，由 PluginManager
        // 在卸载插件移除 AssemblyPart 后主动 NotifyChange 刷新路由。
        builder.Services.AddSingleton<MvcActionDescriptorChangeProvider>();
        builder.Services.AddSingleton<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorChangeProvider>(
            sp => sp.GetRequiredService<MvcActionDescriptorChangeProvider>());

        var app = builder.Build();

        // 宿主 Build 完成后，为所有已挂载插件构建子 provider（仅插件自身注册的服务，含 IContext）。
        // 插件服务的宿主依赖经 Cordis Context 运行期 ctx.Get<T>() 获取，无需宿主转发。必须在第一次请求前完成。
        pluginServiceRegistry.BuildAll();

        // 初始化阶段：把宿主应提供的服务（能力接缝契约）seed 进插件根上下文，
        // 子插件经父级链继承消费（彻底 Cordis 模式，对标 app.service）。
        pluginManager.ProvideHostServices(app.Services);

        // 启动路径接线：对所有已加载插件发现扩展点（菜单/工具），
        // 修复「启动后菜单/工具扩展为空」的缺陷（缺陷2：发现仅挂在 PluginController.EnablePlugin）。
        var extensionPointManager = app.Services.GetRequiredService<ExtensionPointManager>();
        pluginManager.DiscoverAllExtensions(extensionPointManager);

        // 插件版本管理服务接线：必须在 app.Services（而非 bootstrap 临时 provider）上解析，
        // 否则初始化的是另一个单例实例，控制器拿到的实例备份目录仍为空串，
        // 导致 /api/plugin/updates 恒空、update 恒 400。
        app.Services.GetRequiredService<PluginVersionService>().Initialize(pluginsPath);

        // 把独立程序集插件的控制器程序集加入 MVC 部件，使其 [Route] 控制器被路由发现（修复拆独立程序集后的 404）。
        // 同时把 partManager 与 ActionDescriptor 刷新通知交给 PluginManager：卸载插件时移除对应 AssemblyPart
        // 并主动触发刷新（动态端点移除）。
        var partManager = app.Services.GetRequiredService<Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartManager>();
        var actionDescriptorRefresh = app.Services.GetRequiredService<MvcActionDescriptorChangeProvider>();
        pluginManager.RegisterPluginApplicationParts(partManager, actionDescriptorRefresh.NotifyChange);

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseCors("AllowAll");

        app.UseWebSockets();

        // 聊天记录实时流式推送：WebSocket 端点 /ws（前端 wsService 已连接此地址）。
        // 仅作为服务端→客户端的实时事件广播通道；暂不对客户端消息做业务处理。
        var wsBroadcaster = app.Services.GetRequiredService<IWebSocketBroadcaster>();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var ws = await context.WebSockets.AcceptWebSocketAsync();
                    wsBroadcaster.Add(ws);
                    await ChatRecordWebSocketLoop(ws, wsBroadcaster);
                    return;
                }

                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("WebSocket expected");
                return;
            }

            await next();
        });

        // 插件自带界面资源：/plugins/{插件id}/frontend/** → 只读静态服务。
        // 访问范围限定在各插件自身的 frontend/ 目录内并拒绝路径穿越；
        // 插件目录在请求时经 PluginManager 解析，因此运行时新增/热更的插件其界面资源立即可被访问。
        app.UseMiddleware<PluginFrontendFileMiddleware>();

        app.UseStaticFiles();

        // SPA Fallback 中间件：前端路由（如 /agents、/settings）的 404 请求返回 index.html。
        // 判断规则：不拦截带文件后缀的请求（.js/.css/.png 等静态资源），
        // 不拦截已知 API 前缀（/api、/v1、/openapi、/scalar、/swagger 等）的请求。
        app.Use(async (context, next) =>
        {
            await next();
            if (context.Response.StatusCode == 404 && !IsApiOrStaticRequest(context.Request.Path))
            {
                // WebRootPath 在服务启动时解析：若当时 wwwroot 不存在则为 null（且不会因后续目录出现而自动补正）。
                // 此处兜底到 ContentRootPath/wwwroot，与 UseStaticFiles 内部对 null WebRootPath 的处理一致；
                // 仅当 index.html 确实存在时才改写响应，避免返回空 200。
                var indexPath = Path.Combine(ResolveSpaWebRootPath(app.Environment.WebRootPath, app.Environment.ContentRootPath), "index.html");
                if (File.Exists(indexPath))
                {
                    context.Response.Clear();
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync(indexPath);
                }
            }
        });

        // 判断请求路径是否为 API 调用或静态资源请求
        static bool IsApiOrStaticRequest(PathString path)
        {
            var pathStr = path.Value ?? "";
            // 带文件后缀的请求（.html .js .css .png .ico .svg .json .webp .woff2 等）→ 静态资源，不拦截
            if (Path.HasExtension(pathStr))
                return true;
            // 已知 API 前缀的请求
            var apiPrefixes = new[] { "/api", "/v1", "/v2", "/v3", "/openapi", "/scalar", "/swagger" };
            foreach (var prefix in apiPrefixes)
            {
                if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        app.UseAuthorization();

        app.MapControllers();

        // 003-api-server-settings converge：注册 OpenAPI 端点 + Scalar UI
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("ForgeSelf API 参考");
            options.WithTheme(ScalarTheme.Purple);
            options.WithDarkModeToggle(true);
            options.AddPreferredSecuritySchemes("Bearer");
            options.AddHttpAuthentication("Bearer", auth =>
            {
                auth.Token = "";
            });
        });

        if (!app.Environment.IsEnvironment("Testing"))
        {
            app.InitializeXCodeDatabase(app.Environment, app.Services.GetRequiredService<IDataLocationService>().GetHostDataDirectory());
            XTrace.Log.Info("数据库初始化完成");
        }

        // 密文迁移（030 认证体系升级）：建表完成后、播种主密钥之前把 v1 旧密文重封装为 v2。
        // 顺序不可颠倒——先迁移再播种，避免播种出的新密文被旧逻辑二次处理。
        // 迁移是可选优化，失败只记日志、绝不阻断启动（v1 读兼容链永久保留）。
        try
        {
            using var migrationScope = app.Services.CreateScope();
            var migration = new SecretMigrationService(migrationScope.ServiceProvider.GetRequiredService<ISecretEncryptionService>());
            migration.MigrateOnce();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("密文迁移失败（已跳过，不影响功能）: {0}", ex.Message);
        }

        // 数据库就绪后，从 DB 加载 AI 提供方到网关
        try
        {
            using var seedScope = app.Services.CreateScope();
            var providerService = seedScope.ServiceProvider.GetRequiredService<IAIProviderService>();
            var configService = seedScope.ServiceProvider.GetRequiredService<IConfigurationService>();
            providerService.EnsureSeeded(configService.GetAIProviderConfigs());

            var registry = seedScope.ServiceProvider.GetRequiredService<AIProviderRegistry>();
            var httpClientFactory = seedScope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            registry.ReloadAsync(httpClientFactory, providerService.GetAllConfigs()).GetAwaiter().GetResult();
            XTrace.Log.Info("AI 网关已从数据库加载提供方配置");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("启动时加载 AI 提供方配置失败: {0}", ex.Message);
        }

        // API 服务器密钥：首次启动自动生成密钥并加密存储
        try
        {
            using var keyScope = app.Services.CreateScope();
            var keyService = keyScope.ServiceProvider.GetRequiredService<ApiServerKeyService>();
            keyService.EnsureApiKeySeeded();
            XTrace.Log.Info("API 服务器密钥已就绪");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("API 服务器密钥播种失败: {0}", ex.Message);
        }

        try
        {
            // 经共享宿主契约启动调度器（Scheduler 插件注册 DI 单例，ADR D2），避免宿主直接依赖插件程序集。
            var taskScheduler = app.Services.GetRequiredService<ISchedulerHost>();
            taskScheduler.StartAsync().Wait();
            XTrace.Log.Info("任务调度器已启动");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("启动任务调度器失败: {0}", ex.Message);
        }

        // 使用 ForgeSetting 配置的端口号（支持动态修改，重启生效）
        var port = ForgeSetting.Current.PortNumber;
        app.Urls.Add($"http://0.0.0.0:{port}");

        XTrace.Log.Info("铸己匣 ForgeSelf 服务启动中...");
        XTrace.Log.Info("监听端口: {0}", port);

        return app;
    }

    /// <summary>
    /// /ws WebSocket 连接循环：保持连接、读取客户端消息（当前忽略）、断开时清理。
    /// </summary>
    private static async Task ChatRecordWebSocketLoop(WebSocket ws, IWebSocketBroadcaster broadcaster)
    {
        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                // 来自客户端的消息（如聊天指令）当前忽略，仅保持连接存活
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            broadcaster.Remove(ws);
            try
            {
                if (ws.State != WebSocketState.Closed && ws.State != WebSocketState.Aborted)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { }
        }
    }

    /// <summary>
    /// 解析 SPA 静态根目录路径。
    /// <see cref="WebRootPath"/> 在 WebApplication 启动时按 {ContentRoot}/wwwroot 解析：
    /// 若启动时该目录尚不存在，则该属性保持 <c>null</c>（且不会因后续目录出现而自动补正），
    /// 直接导致 SPA Fallback 中间件 <c>Path.Combine(null, "index.html")</c> 抛 <see cref="ArgumentNullException"/>。
    /// 此处对空值兜底到 {ContentRoot}/wwwroot，与 <c>UseStaticFiles</c> 内部对 null WebRootPath 的处理一致。
    /// </summary>
    /// <param name="webRootPath">环境变量中的 WebRootPath（可能为 null 或空）。</param>
    /// <param name="contentRootPath">环境变量中的 ContentRootPath（始终非空）。</param>
    /// <returns>用于查找 index.html 的 SPA 根目录绝对路径。</returns>
    public static string ResolveSpaWebRootPath(string? webRootPath, string contentRootPath)
        => string.IsNullOrWhiteSpace(webRootPath)
            ? Path.Combine(contentRootPath, "wwwroot")
            : webRootPath;

    /// <summary>
    /// 解析 WebRoot（静态资源根目录）的绝对路径。
    /// 必须以「程序所在目录（exe 目录，<see cref="AppContext.BaseDirectory"/>）」为基准，
    /// 避免从其他文件夹启动（例如直接运行 publish/ForgeSelf.exe）时，
    /// 因 ContentRootPath/CWD 偏差而找不到 wwwroot。
    /// 同时兼容开发期：dotnet run 的 CWD 是项目目录，wwwroot 建于项目级 ForgeSelf.Api/wwwroot（构建时会复制到输出目录）。
    /// 优先返回「存在且含 index.html」的候选；若都不存在则回退到首个候选基准下的 wwwroot
    /// （保证 WebRootPath 非空，避免 SPA Fallback 的 Path.Combine(null) 崩溃）。
    /// </summary>
    /// <param name="candidateBases">候选基准目录集合（按优先级排列，通常为 exe 目录、当前工作目录等）。</param>
    /// <returns>用于托管静态资源与 SPA 的 wwwroot 绝对路径。</returns>
    public static string ResolveWebRootPath(params string[] candidateBases)
    {
        var candidates = new List<string>();
        foreach (var baseDir in candidateBases)
        {
            if (!string.IsNullOrWhiteSpace(baseDir))
                candidates.Add(Path.Combine(baseDir!, "wwwroot"));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "index.html")))
                return candidate;
        }

        return candidates.Count > 0 ? candidates[0] : Path.Combine(AppContext.BaseDirectory, "wwwroot");
    }
}