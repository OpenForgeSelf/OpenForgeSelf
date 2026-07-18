using Microsoft.EntityFrameworkCore;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Data;

public class AIAgentDbContext : DbContext
{
    public DbSet<ChatMessageEntity> ChatMessages { get; set; }

    public AIAgentDbContext(DbContextOptions<AIAgentDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreateTime);
            entity.HasIndex(e => new { e.SessionId, e.CreateTime });
        });
    }
}
