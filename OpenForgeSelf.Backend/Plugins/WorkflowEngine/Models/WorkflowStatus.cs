namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

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
