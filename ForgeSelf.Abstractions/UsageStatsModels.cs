using System.ComponentModel.DataAnnotations;

namespace ForgeSelf.Abstractions;

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
/// 使用记录实体
/// </summary>
public class UsageRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// 插件ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// 工具ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// 操作类型
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// 用户代理
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// IP地址
    /// </summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// 持续时间（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 元数据JSON
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// 工作流执行ID（标识是否来自工作流调用）
    /// </summary>
    public long? WorkflowExecutionId { get; set; }

    /// <summary>
    /// 工作流步骤ID（工作流中的哪个步骤）
    /// </summary>
    [MaxLength(100)]
    public string? StepId { get; set; }
}

/// <summary>
/// 每日使用汇总实体
/// </summary>
public class UsageDailySummary
{
    /// <summary>
    /// 汇总记录ID
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// 日期
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// 插件ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// 工具ID
    /// </summary>
    [Required]
    [MaxLength(100)]
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
    /// 独立用户数
    /// </summary>
    public int UniqueUsers { get; set; }
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
/// 个人工具库总览统计
/// </summary>
public class PersonalLibraryStatsDto
{
    public int ScriptCount { get; set; }
    public int CodeSnippetCount { get; set; }
    public int WorkflowCount { get; set; }
    public int FavoriteCount { get; set; }
    public int TotalUsageCount { get; set; }
    public double TotalUsageDurationSeconds { get; set; }
    public List<TopToolItem> TopTools { get; set; } = [];
    public List<TopScriptItem> TopScripts { get; set; } = [];
    public List<PopularWorkflowDto> TopWorkflows { get; set; } = [];
}

/// <summary>
/// 常用脚本项
/// </summary>
public class TopScriptItem
{
    public long ScriptId { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// 常用工作流项
/// </summary>
public class PopularWorkflowDto
{
    public long WorkflowId { get; set; }

    public string WorkflowName { get; set; } = string.Empty;

    public int ExecutionCount { get; set; }

    public double SuccessRate { get; set; }

    public double AverageDurationSeconds { get; set; }

    public int Rank { get; set; }
}

/// <summary>
/// 能力成长曲线数据点
/// </summary>
public class GrowthCurvePoint
{
    public DateTime Date { get; set; }
    public int UsageCount { get; set; }
    public int NewScripts { get; set; }
    public int NewCodeSnippets { get; set; }
    public int NewWorkflows { get; set; }
    public int NewTools { get; set; }
    public double TotalDurationSeconds { get; set; }
}

/// <summary>
/// 节省时间估算
/// </summary>
public class TimeSavedEstimateDto
{
    public double TotalTimeSavedSeconds { get; set; }
    public double TotalTimeSavedHours => TotalTimeSavedSeconds / 3600;
    public int TotalUsageCount { get; set; }
    public double AverageSavedSecondsPerUse { get; set; }
    public List<TimeSavedCategoryItem> ByCategory { get; set; } = [];
}

/// <summary>
/// 节省时间分类项
/// </summary>
public class TimeSavedCategoryItem
{
    public string Category { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public double TimeSavedSeconds { get; set; }
}
