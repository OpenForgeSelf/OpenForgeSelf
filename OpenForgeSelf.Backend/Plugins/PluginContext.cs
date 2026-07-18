using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Services.UsageStats;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins;

/// <summary>
/// 插件上下文实现
/// </summary>
public class PluginContext : IPluginContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUsageStatsService _usageStatsService;

    /// <inheritdoc />
    public PluginMetadata Metadata { get; }

    /// <inheritdoc />
    public IServiceProvider Services => _serviceProvider;

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string ConfigDirectory { get; }

    /// <inheritdoc />
    public PluginState State { get; internal set; }

    /// <inheritdoc />
    public IUsageStatsService UsageStats => _usageStatsService;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="metadata">插件元数据</param>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="usageStatsService">使用统计服务</param>
    /// <param name="dataDirectory">数据目录</param>
    /// <param name="configDirectory">配置目录</param>
    public PluginContext(
        PluginMetadata metadata,
        IServiceProvider serviceProvider,
        IUsageStatsService usageStatsService,
        string dataDirectory,
        string configDirectory)
    {
        Metadata = metadata;
        _serviceProvider = serviceProvider;
        _usageStatsService = usageStatsService;
        DataDirectory = dataDirectory;
        ConfigDirectory = configDirectory;
        State = PluginState.NotLoaded;
    }

    /// <inheritdoc />
    public void LogInfo(string message)
    {
        XTrace.Log.Info("[{0}] {1}", Metadata.Id, message);
    }

    /// <inheritdoc />
    public void LogWarn(string message)
    {
        XTrace.Log.Warn("[{0}] {1}", Metadata.Id, message);
    }

    /// <inheritdoc />
    public void LogError(string message)
    {
        XTrace.Log.Error("[{0}] {1}", Metadata.Id, message);
    }

    /// <inheritdoc />
    public async Task<long> RecordUsageAsync(
        string toolId,
        string actionType,
        long? durationMs = null,
        Dictionary<string, object>? metadata = null)
    {
        return await _usageStatsService.RecordUsageAsync(
            Metadata.Id,
            toolId,
            actionType,
            durationMs,
            metadata);
    }
}
