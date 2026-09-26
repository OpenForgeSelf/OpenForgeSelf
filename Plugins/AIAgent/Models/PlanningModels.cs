using System.ComponentModel.DataAnnotations;

namespace ForgeSelf.Api.Plugins.AIAgent.Models;

public enum UsageEventType
{
    Chat = 0,
    ToolCall = 1,
    PluginUse = 2,
    WorkflowRun = 3,
    MemoryAccess = 4,
    AgentUse = 5,
    PageVisit = 6
}

public enum SuggestionType
{
    ToolRecommendation = 0,
    WorkflowRecommendation = 1,
    MemoryReminder = 2,
    AgentRecommendation = 3,
    ShortcutSuggestion = 4,
    OptimizationTip = 5,
    LearningRecommendation = 6
}

public enum SuggestionPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum PatternType
{
    Temporal = 0,
    Sequential = 1,
    Frequency = 2,
    Contextual = 3,
    Behavioral = 4
}

public class UsageEventEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public UsageEventType EventType { get; set; }

    [Required]
    public string EventName { get; set; } = string.Empty;

    public string? EventData { get; set; }

    public string? Category { get; set; }

    public string? Tags { get; set; }

    public int DurationMs { get; set; }

    public bool Success { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class UsagePatternEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public PatternType PatternType { get; set; }

    [Required]
    public string PatternName { get; set; } = string.Empty;

    public string? PatternDescription { get; set; }

    public string? PatternData { get; set; }

    public double Confidence { get; set; } = 0.0;

    public int OccurrenceCount { get; set; } = 0;

    public DateTime FirstObservedAt { get; set; } = DateTime.Now;

    public DateTime LastObservedAt { get; set; } = DateTime.Now;

    public double TrendScore { get; set; } = 0.0;

    public bool IsActive { get; set; } = true;
}

public class SuggestionEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public SuggestionType Type { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string? ActionUrl { get; set; }

    public string? ActionData { get; set; }

    public SuggestionPriority Priority { get; set; } = SuggestionPriority.Medium;

    public double RelevanceScore { get; set; } = 0.5;

    public string? SourcePatternId { get; set; }

    public bool IsRead { get; set; } = false;

    public bool IsDismissed { get; set; } = false;

    public bool IsActioned { get; set; } = false;

    public DateTime? ReadAt { get; set; }

    public DateTime? DismissedAt { get; set; }

    public DateTime? ActionedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime ExpiresAt { get; set; }
}

public class UserPreferenceEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string PreferenceKey { get; set; } = string.Empty;

    public string? PreferenceValue { get; set; }

    public string? Category { get; set; }

    public double Confidence { get; set; } = 0.8;

    public int UpdateCount { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string? Source { get; set; }
}

public class UserSkillEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string SkillName { get; set; } = string.Empty;

    public string? SkillCategory { get; set; }

    public string? SkillDescription { get; set; }

    public double ProficiencyLevel { get; set; } = 0.0;

    public int UsageCount { get; set; } = 0;

    public double SuccessRate { get; set; } = 0.0;

    public double AverageDurationMs { get; set; } = 0.0;

    public DateTime FirstUsedAt { get; set; } = DateTime.Now;

    public DateTime LastUsedAt { get; set; } = DateTime.Now;

    public double TrendScore { get; set; } = 0.0;
}

public class UserProfileSummary
{
    public string UserId { get; set; } = string.Empty;
    public int TotalUsageDays { get; set; }
    public int TotalActions { get; set; }
    public List<string> TopTools { get; set; } = new();
    public List<string> TopCategories { get; set; } = new();
    public List<UserSkillEntity> Skills { get; set; } = new();
    public List<string> Preferences { get; set; } = new();
    public string? PrimaryUseTime { get; set; }
    public string? UsageStyle { get; set; }
    public double EfficiencyScore { get; set; }
    public double LearningRate { get; set; }
}
