namespace OpenForgeSelf.Abstractions;

public class WorkflowDefinitionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public List<WorkflowStep> Steps { get; set; } = new();
    public List<WorkflowVariable> Variables { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsFavorite { get; set; }
    public int UsageCount { get; set; }
    public WorkflowStatus Status { get; set; }
    public string? StartStepId { get; set; }
    public int StepCount => Steps.Count;
}

public class CreateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public List<WorkflowStep> Steps { get; set; } = new();
    public List<WorkflowVariable> Variables { get; set; } = new();
    public string? StartStepId { get; set; }
}

public class UpdateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public List<WorkflowStep> Steps { get; set; } = new();
    public List<WorkflowVariable> Variables { get; set; } = new();
    public WorkflowStatus Status { get; set; }
    public string? StartStepId { get; set; }
}

public class WorkflowExecutionDto
{
    public long Id { get; set; }
    public long WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public WorkflowStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? CurrentStepId { get; set; }
    public string? ErrorMessage { get; set; }
    public double Progress { get; set; }
    public string? TriggeredBy { get; set; }
    public long DurationMs => EndTime.HasValue ? (long)(EndTime.Value - StartTime).TotalMilliseconds : 0;
}

public class WorkflowExecutionDetailDto
{
    public long Id { get; set; }
    public long WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public WorkflowStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? CurrentStepId { get; set; }
    public List<ExecutionLogEntry> Logs { get; set; } = new();
    public string? ResultsJson { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object?>? Variables { get; set; }
    public Dictionary<string, object?>? StepResults { get; set; }
    public double Progress { get; set; }
    public string? TriggeredBy { get; set; }
}

public class ExecuteWorkflowRequest
{
    public Dictionary<string, object?>? InputVariables { get; set; }
    public string? TriggeredBy { get; set; }
}

public class WorkflowTemplateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public int StepCount { get; set; }
}

public class FavoriteWorkflowRequest
{
    public bool IsFavorite { get; set; }
}
