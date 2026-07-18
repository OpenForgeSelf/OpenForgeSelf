using Microsoft.EntityFrameworkCore;

namespace OpenForgeSelf.Backend.Plugins.MemorySystem.Data;

public class MemoryDbContext : DbContext
{
    public DbSet<MemoryEntity> Memories { get; set; }
    public DbSet<MemoryCategoryEntity> MemoryCategories { get; set; }

    public MemoryDbContext(DbContextOptions<MemoryDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Importance);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<MemoryCategoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SortOrder);
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }
}
