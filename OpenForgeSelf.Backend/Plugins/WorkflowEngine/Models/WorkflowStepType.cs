namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

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
