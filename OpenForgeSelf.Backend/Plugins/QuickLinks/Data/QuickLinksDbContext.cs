using Microsoft.EntityFrameworkCore;

namespace OpenForgeSelf.Backend.Plugins.QuickLinks.Data;

public class QuickLinksDbContext : DbContext
{
    public DbSet<QuickLinkEntity> QuickLinks { get; set; }
    public DbSet<QuickLinkCategoryEntity> QuickLinkCategories { get; set; }

    public QuickLinksDbContext(DbContextOptions<QuickLinksDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuickLinkEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.SortOrder);
            entity.HasIndex(e => e.Name);
        });

        modelBuilder.Entity<QuickLinkCategoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SortOrder);
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }
}
