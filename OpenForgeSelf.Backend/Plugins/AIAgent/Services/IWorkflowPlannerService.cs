using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Services;

public interface IWorkflowPlannerService
{
    Task<WorkflowDefinition> PlanWorkflowAsync(string userRequest, List<string>? availableTools = null);
    Task<string> GetPlanPromptAsync(string userRequest, List<string>? availableTools = null);
}
