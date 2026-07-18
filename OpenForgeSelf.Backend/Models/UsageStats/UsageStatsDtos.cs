namespace OpenForgeSelf.Backend.Models.UsageStats;

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// 数据项列表
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// 总记录数
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; set; }
}

/// <summary>
/// 工具排名项
/// </summary>
public class ToolRankingItem
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
    /// 使用次数
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>
    /// 总持续时间（毫秒）
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// 排名
    /// </summary>
    public int Rank { get; set; }
}

/// <summary>
/// 使用趋势数据点
/// </summary>
public class UsageTrendPoint
{
    /// <summary>
    /// 日期
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// 使用次数
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>
    /// 总持续时间（毫秒）
    /// </summary>
    public long TotalDurationMs { get; set; }
}

/// <summary>
/// 常用工具项
/// </summary>
public class TopToolItem
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
    /// 使用次数
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>
    /// 总持续时间（毫秒）
    /// </summary>
    public long TotalDurationMs { get; set; }
}

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
