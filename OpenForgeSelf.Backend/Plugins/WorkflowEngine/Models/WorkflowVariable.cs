namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

public class WorkflowVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
}
