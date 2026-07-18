namespace OpenForgeSelf.Backend.Models.UsageStats;

public class PersonalLibraryStatsDto
{
    public int ScriptCount { get; set; }
    public int CodeSnippetCount { get; set; }
    public int WorkflowCount { get; set; }
    public int FavoriteCount { get; set; }
    public int TotalUsageCount { get; set; }
    public double TotalUsageDurationSeconds { get; set; }
    public List<TopToolItem> TopTools { get; set; } = [];
    public List<TopScriptItem> TopScripts { get; set; } = [];
    public List<PopularWorkflowDto> TopWorkflows { get; set; } = [];
}

public class TopScriptItem
{
    public long ScriptId { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class GrowthCurvePoint
{
    public DateTime Date { get; set; }
    public int UsageCount { get; set; }
    public int NewScripts { get; set; }
    public int NewCodeSnippets { get; set; }
    public int NewWorkflows { get; set; }
    public int NewTools { get; set; }
    public double TotalDurationSeconds { get; set; }
}

public class TimeSavedEstimateDto
{
    public double TotalTimeSavedSeconds { get; set; }
    public double TotalTimeSavedHours => TotalTimeSavedSeconds / 3600;
    public int TotalUsageCount { get; set; }
    public double AverageSavedSecondsPerUse { get; set; }
    public List<TimeSavedCategoryItem> ByCategory { get; set; } = [];
}

public class TimeSavedCategoryItem
{
    public string Category { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public double TimeSavedSeconds { get; set; }
}

public class ContextualRecommendationRequest
{
    public string? CurrentPage { get; set; }
    public string? CurrentAction { get; set; }
    public List<string>? RecentTools { get; set; }
    public int Limit { get; set; } = 5;
}

public class ContextualRecommendationDto
{
    public List<RecommendedToolItem> RecommendedTools { get; set; } = [];
    public List<RecommendedScriptItem> RecommendedScripts { get; set; } = [];
    public List<RecommendedSnippetItem> RecommendedSnippets { get; set; } = [];
    public List<WorkflowRecommendationDto> RecommendedWorkflows { get; set; } = [];
}

public class RecommendedToolItem
{
    public string PluginId { get; set; } = string.Empty;
    public string ToolId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public double MatchScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public class RecommendedScriptItem
{
    public long ScriptId { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public double MatchScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public class RecommendedSnippetItem
{
    public long SnippetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public double MatchScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public class SaveAsSuggestionRequest
{
    public string ActionType { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? Language { get; set; }
    public int UsageFrequency { get; set; }
}

public class SaveAsSuggestionDto
{
    public bool ShouldSave { get; set; }
    public string SuggestedType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? SuggestedTitle { get; set; }
    public List<string> SuggestedTags { get; set; } = [];
    public double Confidence { get; set; }
}
