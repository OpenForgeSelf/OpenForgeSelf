using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Models.UsageStats;
using Microsoft.EntityFrameworkCore;

namespace OpenForgeSelf.Backend.Data;

/// <summary>
/// 数据库上下文
/// </summary>
public class OpenForgeSelfDbContext : DbContext
{
    /// <summary>
    /// 聊天消息表
    /// </summary>
    public DbSet<ChatMessageEntity> ChatMessages { get; set; }

    /// <summary>
    /// 使用记录表
    /// </summary>
    public DbSet<UsageRecord> UsageRecords { get; set; }

    /// <summary>
    /// 每日使用汇总表
    /// </summary>
    public DbSet<UsageDailySummary> UsageDailySummaries { get; set; }

    /// <summary>
    /// 工作流使用记录表
    /// </summary>
    public DbSet<WorkflowUsageRecord> WorkflowUsageRecords { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public OpenForgeSelfDbContext(DbContextOptions<OpenForgeSelfDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 配置实体
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreateTime);
            entity.HasIndex(e => new { e.SessionId, e.CreateTime });
        });

        modelBuilder.Entity<UsageRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PluginId);
            entity.HasIndex(e => e.ToolId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.ActionType);
            entity.HasIndex(e => new { e.PluginId, e.ToolId, e.Timestamp });
        });

        modelBuilder.Entity<UsageDailySummary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.PluginId);
            entity.HasIndex(e => e.ToolId);
            entity.HasIndex(e => new { e.Date, e.PluginId, e.ToolId });
            entity.HasIndex(e => new { e.PluginId, e.ToolId, e.Date });
        });

        modelBuilder.Entity<WorkflowUsageRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.ExecutionId);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.WorkflowId, e.StartTime });
            entity.HasIndex(e => new { e.Status, e.StartTime });
        });
    }
}