using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Models.UsageStats;

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
