using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.MemorySystem.Data;

public enum MemoryType
{
    Fact = 0,
    Preference = 1,
    Project = 2,
    Personal = 3,
    Workflow = 4,
    Skill = 5,
    Other = 99
}

public enum MemoryImportance
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public class MemoryEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public MemoryType Type { get; set; } = MemoryType.Fact;

    public MemoryImportance Importance { get; set; } = MemoryImportance.Medium;

    [MaxLength(200)]
    public string? Tags { get; set; }

    [MaxLength(500)]
    public string? Source { get; set; }

    public long? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public MemoryCategoryEntity? Category { get; set; }

    public int AccessCount { get; set; } = 0;

    public DateTime? LastAccessedAt { get; set; }

    public double DecayScore { get; set; } = 1.0;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool IsDeleted { get; set; } = false;
}

public class MemoryCategoryEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public int SortOrder { get; set; } = 0;

    public int MemoryCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class ShortTermMemoryItem
{
    public string SessionId { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
