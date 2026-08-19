namespace OpenForgeSelf.Backend.Models.UsageStats;

/// <summary>
/// 记录使用请求
/// </summary>
public class RecordUsageRequest
{
    /// <summary>
    /// 插件ID
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// 工具ID
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// 操作类型
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// 持续时间（毫秒）
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
