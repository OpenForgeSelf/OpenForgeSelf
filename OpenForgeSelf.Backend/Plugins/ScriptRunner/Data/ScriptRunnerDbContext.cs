using Microsoft.EntityFrameworkCore;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Data;

public class ScriptRunnerDbContext : DbContext
{
    public DbSet<ScriptEntity> Scripts { get; set; }
    public DbSet<ScriptExecutionEntity> ScriptExecutions { get; set; }
    public DbSet<CodeSnippetEntity> CodeSnippets { get; set; }

    public ScriptRunnerDbContext(DbContextOptions<ScriptRunnerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScriptEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Language);
            entity.HasIndex(e => e.IsFavorite);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasIndex(e => e.LastUsedAt);
        });

        modelBuilder.Entity<ScriptExecutionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ScriptId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.EndTime);
        });

        modelBuilder.Entity<CodeSnippetEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Language);
            entity.HasIndex(e => e.IsFavorite);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasIndex(e => e.LastUsedAt);
            entity.HasIndex(e => e.Source);
        });
    }
}
