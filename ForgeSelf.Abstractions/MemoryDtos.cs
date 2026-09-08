namespace ForgeSelf.Abstractions;

/// <summary>记忆类型。</summary>
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

/// <summary>记忆重要程度。</summary>
public enum MemoryImportance
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public class MemoryDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryType Type { get; set; }
    public MemoryImportance Importance { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Source { get; set; }
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int AccessCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public double RelevanceScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MemoryCategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public int MemoryCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMemoryRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryType Type { get; set; } = MemoryType.Fact;
    public MemoryImportance Importance { get; set; } = MemoryImportance.Medium;
    public List<string>? Tags { get; set; }
    public string? Source { get; set; }
    public long? CategoryId { get; set; }
}

public class UpdateMemoryRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public MemoryType? Type { get; set; }
    public MemoryImportance? Importance { get; set; }
    public List<string>? Tags { get; set; }
    public string? Source { get; set; }
    public long? CategoryId { get; set; }
}

public class SearchMemoryRequest
{
    public string? Keyword { get; set; }
    public MemoryType? Type { get; set; }
    public long? CategoryId { get; set; }
    public MemoryImportance? MinImportance { get; set; }
    public string? Tag { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MemorySearchResult
{
    public List<MemoryDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CreateMemoryCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
}

public class UpdateMemoryCategoryRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int? SortOrder { get; set; }
}

public class MemoryStatsDto
{
    public int TotalMemories { get; set; }
    public int TotalCategories { get; set; }
    public int TodayAccessed { get; set; }
    public int WeekAccessed { get; set; }
    public Dictionary<MemoryType, int> ByType { get; set; } = new();
    public Dictionary<MemoryImportance, int> ByImportance { get; set; } = new();
    public List<MemoryDto> RecentMemories { get; set; } = new();
    public List<MemoryDto> FrequentlyAccessed { get; set; } = new();
}

public class MemoryExtractionRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public List<string> Messages { get; set; } = new();
}

public class MemoryExtractionResult
{
    public List<ExtractedMemory> ExtractedMemories { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class ExtractedMemory
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryType Type { get; set; }
    public MemoryImportance Importance { get; set; }
    public List<string> Tags { get; set; } = new();
    public double Confidence { get; set; }
}

public class ImportMemoryRequest
{
    public List<ImportMemoryItem> Items { get; set; } = new();
    public bool OverwriteExisting { get; set; }
}

public class ImportMemoryItem
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryType Type { get; set; } = MemoryType.Fact;
    public MemoryImportance Importance { get; set; } = MemoryImportance.Medium;
    public List<string>? Tags { get; set; }
    public string? CategoryName { get; set; }
    public string? Source { get; set; }
}
