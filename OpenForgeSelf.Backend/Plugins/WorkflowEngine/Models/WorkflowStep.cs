using System.Text.Json.Serialization;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

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
