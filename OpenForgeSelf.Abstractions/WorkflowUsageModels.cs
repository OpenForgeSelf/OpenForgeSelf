namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 工作流执行状态
/// </summary>
public enum WorkflowExecutionStatus
{
    Success = 0,
    Failed = 1,
    Cancelled = 2
}

public class RecordWorkflowExecutionRequest
{
    public long WorkflowId { get; set; }

    public string WorkflowName { get; set; } = string.Empty;

    public long ExecutionId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public WorkflowExecutionStatus Status { get; set; }

    public double DurationSeconds { get; set; }

    public Dictionary<string, object?>? InputVariables { get; set; }

    public object? OutputResult { get; set; }

    public int ToolCallCount { get; set; }

    public int StepCount { get; set; }

    public string? TriggeredBy { get; set; }

    public string? ErrorMessage { get; set; }
}

public class WorkflowStatsDto
{
    public long WorkflowId { get; set; }

    public string WorkflowName { get; set; } = string.Empty;

    public int TotalExecutions { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    public int CancelledCount { get; set; }

    public double SuccessRate { get; set; }

    public double AverageDurationSeconds { get; set; }

    public double TotalDurationSeconds { get; set; }

    public int AverageToolCallCount { get; set; }

    public int AverageStepCount { get; set; }
}

public class WorkflowUsageTrendDto
{
    public DateTime Date { get; set; }

    public int ExecutionCount { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    public double TotalDurationSeconds { get; set; }
}

public class WorkflowTotalStatsDto
{
    public int TotalWorkflows { get; set; }

    public int TotalExecutions { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    public int CancelledCount { get; set; }

    public double SuccessRate { get; set; }

    public double TotalDurationSeconds { get; set; }

    public double AverageDurationSeconds { get; set; }

    public int TotalToolCalls { get; set; }
}

public class WorkflowToolRankingDto
{
    public string PluginId { get; set; } = string.Empty;

    public string ToolId { get; set; } = string.Empty;

    public int UseCount { get; set; }

    public int WorkflowCount { get; set; }

    public double TotalDurationMs { get; set; }

    public int Rank { get; set; }
}

public class WorkflowRecommendationDto
{
    public long WorkflowId { get; set; }

    public string WorkflowName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public int UsageCount { get; set; }

    public double MatchScore { get; set; }

    public string MatchReason { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();
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
