using System.Security.Claims;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Authentication;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using OpenForgeSelf.Backend.Data;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Services;
using OpenForgeSelf.Backend.Models;
using OpenForgeSelf.Backend.Services;
using Scalar.AspNetCore;
using OpenForgeSelf.Backend.Security;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;
using OpenForgeSelf.Backend.Services.AI.Providers;
using OpenForgeSelf.Backend.Services.Mcp;
using OpenForgeSelf.Backend.Services.Skills;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NewLife.Log;

namespace OpenForgeSelf.Backend;

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
        // 显式固定 WebRoot 为「程序所在目录（exe 目录）下的 wwwroot」，兼容开发期项目目录 wwwroot。
        // 必须在 CreateBuilder 阶段通过 WebApplicationOptions 设定：若在 CreateBuilder 之后调用
        // builder.WebHost.UseWebRoot，ASP.NET Core 会抛 NotSupportedException
        // （"Changing the host configuration using WebApplicationBuilder.WebHost is not supported"）。
        // 以 exe 目录为基准：从其他文件夹启动（如直接运行 publish/OpenForgeSelf.exe）时，
        // ContentRootPath/CWD 会偏离，默认 WebRootPath 解析可能找不到 wwwroot（2026-08-14 实战坑）。
        // 候选基准：exe 目录 + 当前工作目录（dotnet run 开发期 CWD=项目目录，wwwroot 建于此）。
        var webRoot = ResolveWebRootPath(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            WebRootPath = webRoot,
        });

        XTrace.Log.Level = NewLife.Log.LogLevel.Info;

        // 统一数据根服务：按运行形态解析（开发→程序目录 Data/，否则→用户主目录 ~/.forgeself）
        var dataLocation = new DataLocationService(builder.Environment);
        builder.Services.AddSingleton<IDataLocationService>(dataLocation);
        XTrace.Log.Info("运行时数据根目录: {0}", dataLocation.GetHostDataDirectory());

        // 统一所有 NewLife Config<T> 配置文件落盘位置（XCode/Core/Agent/项目自有等），
        // 必须早于 AddXCode（其内部访问 XCodeSetting.Current）及任何 .Current 访问，
        // 避免配置文件散落到程序目录/输出目录。
        var configRoot = Path.Combine(dataLocation.GetHostDataDirectory(), "Config");
        ConfigUnifier.UnifyAllConfigFiles(configRoot);
        XTrace.Log.Info("配置文件统一目录: {0}", configRoot);

        builder.Services.AddXCode(builder.Configuration, dataLocation.GetHostDataDirectory());

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        builder.Services.AddControllers();

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
        builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
        builder.Services.AddSingleton<IMcpService, McpService>();
        builder.Services.AddSingleton<ICronParser, CronParser>();
        builder.Services.AddSingleton<IRuntimeDetector, RuntimeDetector>();
        builder.Services.AddSingleton<ISkillsService, SkillsService>();
        builder.Services.AddHttpClient<IAIService, AIService>();
        // P4 会话/LLM 接缝接线：注册真实实现（消费者经接缝调用，替换 Provider 零改动）
        builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
        builder.Services.AddScoped<ILlmRuntime, AIServiceLlmRuntime>();
        builder.Services.AddSingleton<IAgentLoop, InMemoryAgentLoop>();
        builder.Services.AddSingleton<IInbox, InMemoryInbox>();

        builder.Services.AddScoped<ILogService, LogService>();
        builder.Services.AddScoped<IMessageService, MessageService>();
        builder.Services.AddScoped<IUsageStatsService, UsageStatsService>();
        builder.Services.AddScoped<IWorkflowUsageService, WorkflowUsageService>();
        builder.Services.AddScoped<IWorkflowRecommendationService, WorkflowRecommendationService>();
        builder.Services.AddScoped<IChatTurnService, ChatTurnService>();
        builder.Services.AddScoped<IChatSessionService, ChatSessionService>();
        builder.Services.AddSingleton<IWebSocketBroadcaster, WebSocketBroadcaster>();
        builder.Services.AddScoped<IChatTurnStreamRecorder, ChatTurnStreamRecorder>();

        // AI Provider 配置数据库化（001-ai-provider-config-db）
        builder.Services.AddSingleton<ISecretEncryptionService, AesSecretEncryptionService>();
        builder.Services.AddScoped<IAIProviderRepository, AIProviderRepository>();
        builder.Services.AddScoped<IAIProviderService, AIProviderService>();
        builder.Services.AddScoped<IAIModelService, AIModelService>();

        // API 服务器密钥管理（003-api-server-settings）
        builder.Services.AddSingleton<ApiServerKeyService>();

        // 端口配置管理（009-web-port-token-security）- 基于配置文件
        builder.Services.AddSingleton<IPortConfigurationService, PortConfigurationService>();
        builder.Services.AddSingleton<IApplicationRestartService, ApplicationRestartService>();
        builder.Services.AddSingleton<IPortAvailabilityService, PortAvailabilityService>();

        // ── 托盘图标 + 服务管理 + 自动更新服务注册（008-tray-service-autoupdate） ──

        // 绑定配置节
        builder.Services.Configure<ServiceConfig>(builder.Configuration.GetSection("Service"));
        builder.Services.Configure<UpdateConfig>(builder.Configuration.GetSection("Update"));

        // ServiceManager — 封装 Windows 服务安装/卸载/状态检测
        builder.Services.AddSingleton<IServiceManager>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ServiceConfig>>().Value;
            return new ServiceManager(config);
        });

        // UpdateChecker — 封装 StarServer 版本查询与下载
        builder.Services.AddSingleton<UpdateChecker>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<UpdateConfig>>().Value;
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(config.CheckTimeoutSeconds + 10) };
            return new UpdateChecker(config, httpClient);
        });

        // UpdateService — 更新流程编排
        builder.Services.AddSingleton<UpdateService>(sp =>
        {
            var updateChecker = sp.GetRequiredService<UpdateChecker>();
            var serviceManager = sp.GetRequiredService<IServiceManager>();
            var updateConfig = sp.GetRequiredService<IOptions<UpdateConfig>>().Value;
            var serviceConfig = sp.GetRequiredService<IOptions<ServiceConfig>>().Value;
            var httpClient = new HttpClient();
            return new UpdateService(updateChecker, serviceManager, updateConfig, serviceConfig, httpClient);
        });

        // TrayIconManager — 系统托盘图标（延迟初始化，通过 StartTrayIcon 在控制台模式启动）
        builder.Services.AddSingleton<TrayIconManager>(sp =>
        {
            var serviceManager = sp.GetRequiredService<IServiceManager>();
            var port = ForgeSetting.Current.PortNumber;
            return new TrayIconManager(serviceManager, port);
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

        // 图片识别结果本地缓存（统一 AI 网关多模态处理用）：按会话 id 分文件夹，存于 Data/ImageRecognitionCache
        var imageCacheRoot = Path.Combine(dataLocation.GetHostDataDirectory(), "ImageRecognitionCache");
        builder.Services.AddSingleton<IImageRecognitionCache>(new LocalFileImageRecognitionCache(imageCacheRoot));

        // 插件可变 MS DI 容器（Option A）：先注册注册表单例，使 PluginManager 可构造注入同一份实例。
        var pluginServiceRegistry = new PluginServiceRegistry();
        builder.Services.AddSingleton<IPluginServiceRegistry>(pluginServiceRegistry);

        builder.Services.AddPluginManager();

        var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
        PluginManager pluginManager;
        using (var bootstrap = builder.Services.BuildServiceProvider())
        {
            pluginManager = bootstrap.GetRequiredService<PluginManager>();
            pluginManager.SetPluginsDirectory(pluginsPath);
            pluginManager.RegisterAllServices(builder.Services);
            // P3 事件总线贯穿：注入平台 IEventBus 单例，插件加载/卸载时发 plugin/loaded / plugin/unloaded。
            pluginManager.EventBus = bootstrap.GetService<OpenForgeSelf.Core.IEventBus>();
        }
        builder.Services.AddPluginManager(pluginManager);

        // 把插件服务类型转发到可变容器：宿主按需经 registry.Resolve 解析；插件卸载后即解析失败。
        foreach (var descriptor in pluginServiceRegistry.CollectForwardDescriptors())
        {
            builder.Services.Add(descriptor);
        }

        // 插件文件级热更新监听（side-by-side 版本目录 + FileSystemWatcher 自动 reload）。
        // 单独在此注册一次：AddPluginManager 被调用两次，若把 IHostedService 放进它会重复启动。
        // 测试环境（Testing）下 StartAsync 自动跳过，不干扰 WebApplicationFactory 集成测试。
        builder.Services.AddSingleton<PluginHotReloadWatcher>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PluginHotReloadWatcher>());

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
            options.WithTitle("OpenForgeSelf API 参考");
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

        XTrace.Log.Info("铸己匣 OpenForgeSelf 服务启动中...");
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
    /// 避免从其他文件夹启动（例如直接运行 publish/OpenForgeSelf.exe）时，
    /// 因 ContentRootPath/CWD 偏差而找不到 wwwroot。
    /// 同时兼容开发期：dotnet run 的 CWD 是项目目录，wwwroot 建于项目级 OpenForgeSelf.Backend/wwwroot（构建时会复制到输出目录）。
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