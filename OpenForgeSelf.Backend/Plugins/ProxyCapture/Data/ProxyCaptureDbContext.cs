using Microsoft.EntityFrameworkCore;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Core;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Data.Entities;

namespace OpenForgeSelf.Backend.Plugins.ProxyCapture.Data;

/// <summary>
/// 插件独立 SQLite 库（capture.db），零宿主侵入。
/// </summary>
public class ProxyCaptureDbContext : DbContext
{
    public ProxyCaptureDbContext(DbContextOptions<ProxyCaptureDbContext> options) : base(options)
    {
    }

    public DbSet<ListenerConfig> ListenerConfigs => Set<ListenerConfig>();

    public DbSet<CaptureSession> CaptureSessions => Set<CaptureSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 抓包记录高频写入，建立时间/监听器索引加速分页查询
        modelBuilder.Entity<CaptureSession>()
            .HasIndex(s => s.Timestamp);
        modelBuilder.Entity<CaptureSession>()
            .HasIndex(s => s.ListenerId);
    }

    /// <summary>构建指向插件独立库的上下文实例。</summary>
    public static ProxyCaptureDbContext Create()
    {
        var dbPath = Path.Combine(CaptureEngine.DataDirectory, "capture.db");
        // 注意：Microsoft.Data.Sqlite 不支持 'BusyTimeout' 连接串关键字（那是 System.Data.SQLite 的），
        // 会抛 ArgumentException；busy timeout 默认 30s，且 SaveRecord 已用 SemaphoreSlim 串行化，无需显式设置。
        var options = new DbContextOptionsBuilder<ProxyCaptureDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new ProxyCaptureDbContext(options);
    }
}
