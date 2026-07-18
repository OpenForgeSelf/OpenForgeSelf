using System.ComponentModel.DataAnnotations;

namespace OpenForgeSelf.Backend.Models.UsageStats;

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
