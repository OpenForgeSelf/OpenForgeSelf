using Microsoft.EntityFrameworkCore;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Data;

public class WorkflowDbContext : DbContext
{
    public DbSet<WorkflowDefinitionEntity> WorkflowDefinitions { get; set; }
    public DbSet<WorkflowExecutionEntity> WorkflowExecutions { get; set; }

    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinitionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IsFavorite);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UpdatedAt);
        });

        modelBuilder.Entity<WorkflowExecutionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.EndTime);
        });
    }
}
