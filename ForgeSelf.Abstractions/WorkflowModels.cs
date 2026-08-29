namespace ForgeSelf.Abstractions;

public enum WorkflowStatus
{
    Draft = 0,
    Ready = 1,
    Running = 2,
    Paused = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

public enum WorkflowStepType
{
    ToolCall = 0,
    Condition = 1,
    Loop = 2,
    Parallel = 3,
    Wait = 4,
    Http = 5,
    Script = 6
}

public class WorkflowDefinition
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public List<WorkflowStep> Steps { get; set; } = new();
    public List<WorkflowVariable> Variables { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsFavorite { get; set; }
    public int UsageCount { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public string? StartStepId { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
}

public class WorkflowVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
}

public class ErrorHandlingConfig
{
    public int MaxRetries { get; set; } = 0;
    public int RetryDelayMs { get; set; } = 1000;
    public bool ContinueOnError { get; set; } = false;
}

public class LoopConfig
{
    public string LoopType { get; set; } = "forEach";
    public string? ItemsExpression { get; set; }
    public string? ItemVariableName { get; set; } = "item";
    public string? ConditionExpression { get; set; }
    public int MaxIterations { get; set; } = 1000;
}

public class WorkflowStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStepType Type { get; set; }
    public string? ToolName { get; set; }
    public string? ParametersJson { get; set; }
    public string? ConditionExpression { get; set; }
    public LoopConfig? LoopConfig { get; set; }
    public List<WorkflowStep>? ChildrenSteps { get; set; } = new();
    public string? NextStepId { get; set; }
    public ErrorHandlingConfig ErrorHandling { get; set; } = new();
    public Dictionary<string, object?>? InputMappings { get; set; }
    public Dictionary<string, string>? OutputMappings { get; set; }

    public long? ScriptId { get; set; }
    public string? ScriptCode { get; set; }
    public ScriptLanguage? ScriptLanguage { get; set; }
    public Dictionary<string, string>? ParameterMappings { get; set; }
    public string? OutputVariable { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? TimeoutSeconds { get; set; }
    public List<int>? SuccessExitCodes { get; set; }
}

public class ExecutionLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? StepId { get; set; }
    public string? StepName { get; set; }
    public string LogLevel { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string? DataJson { get; set; }
}

public class WorkflowExecution
{
    public long Id { get; set; }
    public long WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public WorkflowStatus Status { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
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
