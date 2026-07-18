using OpenForgeSelf.Backend.Data;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.AIAgent.Services;
using OpenForgeSelf.Backend.Plugins.DevTools.Services;
using OpenForgeSelf.Backend.Plugins.FileTools.Services;
using OpenForgeSelf.Backend.Plugins.MemorySystem.Services;
using OpenForgeSelf.Backend.Plugins.QuickLinks.Services;
using OpenForgeSelf.Backend.Plugins.Scheduler.Services;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;
using OpenForgeSelf.Backend.Plugins.TextTools.Services;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;
using OpenForgeSelf.Backend.Services.AI.Providers;
using OpenForgeSelf.Backend.Services.Mcp;
using OpenForgeSelf.Backend.Services.Skills;
using OpenForgeSelf.Backend.Services.UsageStats;
using NewLife.Log;
using SchedulerTaskScheduler = OpenForgeSelf.Backend.Plugins.Scheduler.Services.TaskScheduler;
using DevEncodingService = OpenForgeSelf.Backend.Plugins.DevTools.Services.EncodingService;
using DevHashService = OpenForgeSelf.Backend.Plugins.DevTools.Services.HashService;
using IDevEncodingService = OpenForgeSelf.Backend.Plugins.DevTools.Services.IEncodingService;
using IDevHashService = OpenForgeSelf.Backend.Plugins.DevTools.Services.IHashService;
using TextEncodingService = OpenForgeSelf.Backend.Plugins.TextTools.Services.EncodingService;
using TextHashService = OpenForgeSelf.Backend.Plugins.TextTools.Services.HashService;
using ITextEncodingService = OpenForgeSelf.Backend.Plugins.TextTools.Services.IEncodingService;
using ITextHashService = OpenForgeSelf.Backend.Plugins.TextTools.Services.IHashService;

var builder = WebApplication.CreateBuilder(args);

XTrace.UseConsole();
XTrace.Log.Level = NewLife.Log.LogLevel.Info;

var connectionString = builder.Configuration.GetConnectionString("OpenForgeSelf") ?? "Data Source=Data\\OpenForgeSelf.db";
XTrace.Log.Info("数据库连接字符串: {0}", connectionString);

builder.Services.AddXCode(builder.Configuration);

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

builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddSingleton<IMcpService, McpService>();
builder.Services.AddSingleton<ICronParser, CronParser>();
builder.Services.AddSingleton<IRuntimeDetector, RuntimeDetector>();
builder.Services.AddHttpClient<IAIService, AIService>();

builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IUsageStatsService, UsageStatsService>();
builder.Services.AddScoped<IWorkflowUsageService, WorkflowUsageService>();
builder.Services.AddScoped<IWorkflowRecommendationService, WorkflowRecommendationService>();
builder.Services.AddScoped<IChatRecordService, ChatRecordService>();

builder.Services.AddScoped<IMemoryService, MemoryServiceXCode>();
builder.Services.AddScoped<IMemoryIntegrationService, MemoryIntegrationService>();
builder.Services.AddScoped<IQuickLinkService, QuickLinkService>();
builder.Services.AddScoped<ISchedulerService, SchedulerService>();
builder.Services.AddSingleton<ITaskScheduler, SchedulerTaskScheduler>();
builder.Services.AddSingleton<WorkflowTaskHandler>();
builder.Services.AddSingleton<HttpWebhookHandler>();
builder.Services.AddSingleton<ITaskExecutor, TaskExecutor>();
builder.Services.AddScoped<IScriptService, ScriptService>();
builder.Services.AddScoped<IScriptExecutor, ScriptExecutor>();
builder.Services.AddScoped<ICodeSnippetService, CodeSnippetService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IWorkflowExecutor, WorkflowExecutor>();
builder.Services.AddScoped<IWorkflowScheduler, WorkflowScheduler>();

builder.Services.AddScoped<IAIAgentService, AIAgentService>();
builder.Services.AddScoped<IPluginMessageService, PluginMessageService>();
builder.Services.AddScoped<IAgentRegistryService, AgentRegistryService>();
builder.Services.AddScoped<IAgentCoordinatorService, AgentCoordinatorService>();
builder.Services.AddScoped<IAgentExecutorService, AgentExecutorService>();
builder.Services.AddScoped<IProactivePlanningService, ProactivePlanningService>();
builder.Services.AddScoped<IWorkflowPlannerService, WorkflowPlannerService>();
builder.Services.AddScoped<IToolSelectorService, ToolSelectorService>();

builder.Services.AddScoped<IScriptTemplateService, ScriptTemplateService>();

// DevTools 插件服务
builder.Services.AddScoped<IJsonFormatterService, JsonFormatterService>();
builder.Services.AddScoped<IYamlFormatterService, YamlFormatterService>();
builder.Services.AddScoped<IXmlFormatterService, XmlFormatterService>();
builder.Services.AddScoped<global::OpenForgeSelf.Backend.Plugins.DevTools.Services.IEncodingService, global::OpenForgeSelf.Backend.Plugins.DevTools.Services.EncodingService>();
builder.Services.AddScoped<global::OpenForgeSelf.Backend.Plugins.DevTools.Services.IHashService, global::OpenForgeSelf.Backend.Plugins.DevTools.Services.HashService>();
builder.Services.AddScoped<IRegexService, RegexService>();
builder.Services.AddScoped<ITimestampService, TimestampService>();
builder.Services.AddScoped<IColorService, ColorService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUuidService, UuidService>();
builder.Services.AddScoped<IQrCodeService, QrCodeService>();

// SystemMonitor 插件服务
builder.Services.AddSingleton<ICpuMonitorService, CpuMonitorService>();
builder.Services.AddSingleton<IMemoryMonitorService, MemoryMonitorService>();
builder.Services.AddSingleton<IDiskMonitorService, DiskMonitorService>();
builder.Services.AddSingleton<INetworkMonitorService, NetworkMonitorService>();
builder.Services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();

// FileTools 插件服务
builder.Services.AddScoped<IFileStatsService, FileStatsService>();
builder.Services.AddScoped<IArchiveService, ArchiveService>();
builder.Services.AddScoped<ICleanupService, CleanupService>();
builder.Services.AddScoped<IRenameService, RenameService>();

// TextTools 插件服务
builder.Services.AddScoped<ITextStatsService, TextStatsService>();
builder.Services.AddScoped<ITextFormatterService, TextFormatterService>();
builder.Services.AddScoped<ITextEncodingService, TextEncodingService>();
builder.Services.AddScoped<ITextHashService, TextHashService>();

// AI 网关服务
builder.Services.AddSingleton<AIProviderRegistry>(sp =>
{
    var configService = sp.GetRequiredService<IConfigurationService>();
    var registry = new AIProviderRegistry();

    var providerConfigs = configService.GetAIProviderConfigs();
    foreach (var config in providerConfigs)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();

        IAIProvider provider = config.ProviderType switch
        {
            AIProviderType.OpenAI => new OpenAICompatibleProvider(config, httpClient),
            AIProviderType.Anthropic => new OpenAICompatibleProvider(config, httpClient),
            _ => new OpenAICompatibleProvider(config, httpClient)
        };

        registry.RegisterProvider(provider);
    }

    XTrace.Log.Info("AI 提供者注册表初始化完成，共 {0} 个提供者", providerConfigs.Count);
    return registry;
});

builder.Services.AddPluginManager();

builder.Services.AddSignalR();

var app = builder.Build();

var pluginManager = app.Services.GetRequiredService<PluginManager>();
var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
pluginManager.SetPluginsDirectory(pluginsPath);
pluginManager.DiscoverPlugins();
XTrace.Log.Info("已发现 {0} 个插件", pluginManager.LoadedPluginIds.Count());

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors("AllowAll");

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.InitializeXCodeDatabase(app.Environment);
    
    XTrace.Log.Info("数据库初始化完成");
}

try
{
    var taskScheduler = app.Services.GetRequiredService<ITaskScheduler>();
    taskScheduler.StartAsync().Wait();
    XTrace.Log.Info("任务调度器已启动");
}
catch (Exception ex)
{
    XTrace.Log.Error("启动任务调度器失败: {0}", ex.Message);
}

var port = builder.Configuration.GetValue<int>("Port", 7102);
app.Urls.Add($"http://0.0.0.0:{port}");

XTrace.Log.Info("铸己匣 OpenForgeSelf 服务启动中...");
XTrace.Log.Info("监听端口: {0}", port);

app.Run();
