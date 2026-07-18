using Microsoft.EntityFrameworkCore;

namespace OpenForgeSelf.Backend.Plugins.Scheduler.Data;

public class SchedulerDbContext : DbContext
{
    public DbSet<ScheduledTaskEntity> ScheduledTasks { get; set; }
    public DbSet<ScheduledTaskLogEntity> ScheduledTaskLogs { get; set; }

    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduledTaskEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ScheduleType);
            entity.HasIndex(e => e.NextRunTime);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.TaskType);
        });

        modelBuilder.Entity<ScheduledTaskLogEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.Status);
        });
    }
}
