namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

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
