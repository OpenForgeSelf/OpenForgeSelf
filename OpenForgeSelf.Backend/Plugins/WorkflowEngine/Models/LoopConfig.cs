namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

public class LoopConfig
{
    public string LoopType { get; set; } = "forEach";
    public string? ItemsExpression { get; set; }
    public string? ItemVariableName { get; set; } = "item";
    public string? ConditionExpression { get; set; }
    public int MaxIterations { get; set; } = 1000;
}
