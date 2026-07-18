namespace OpenForgeSelf.Backend.Models.UsageStats;

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

public class PopularWorkflowDto
{
    public long WorkflowId { get; set; }

    public string WorkflowName { get; set; } = string.Empty;

    public int ExecutionCount { get; set; }

    public double SuccessRate { get; set; }

    public double AverageDurationSeconds { get; set; }

    public int Rank { get; set; }
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
