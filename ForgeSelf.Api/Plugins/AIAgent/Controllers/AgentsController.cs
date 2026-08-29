using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.AIAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentRegistryService _agentRegistry;
    private readonly IAgentCoordinatorService _coordinatorService;
    private readonly IAgentExecutorService _executorService;

    public AgentsController(
        IAgentRegistryService agentRegistry,
        IAgentCoordinatorService coordinatorService,
        IAgentExecutorService executorService)
    {
        _agentRegistry = agentRegistry;
        _coordinatorService = coordinatorService;
        _executorService = executorService;
    }

    [HttpGet]
    public ActionResult<List<AgentDefinition>> GetAllAgents()
    {
        var agents = _agentRegistry.GetAllAgents();
        return Ok(agents);
    }

    [HttpGet("{agentId}")]
    public ActionResult<AgentDefinition> GetAgent(string agentId)
    {
        var agent = _agentRegistry.GetAgent(agentId);
        if (agent == null) return NotFound();
        return Ok(agent);
    }

    [HttpGet("type/{type}")]
    public ActionResult<List<AgentDefinition>> GetAgentsByType(AgentType type)
    {
        var agents = _agentRegistry.GetAgentsByType(type);
        return Ok(agents);
    }

    [HttpPost("find")]
    public ActionResult<List<AgentDefinition>> FindAgentsByCapability([FromBody] FindAgentRequest request)
    {
        var agents = _agentRegistry.FindAgentsByCapability(request.Capability);
        return Ok(agents);
    }

    [HttpPost("best-match")]
    public ActionResult<AgentDefinition?> GetBestAgentForTask([FromBody] BestMatchRequest request)
    {
        var agent = _agentRegistry.GetBestAgentForTask(
            request.TaskDescription,
            request.RequiredCapabilities);
        if (agent == null) return NotFound();
        return Ok(agent);
    }

    [HttpPost("coordinate")]
    public async Task<ActionResult<CoordinatorPlan>> CreatePlan([FromBody] CoordinateRequest request)
    {
        var plan = await _coordinatorService.CreatePlanAsync(request.UserRequest);
        return Ok(plan);
    }

    [HttpPost("execute")]
    public async Task<ActionResult<AgentExecutionResult>> ExecutePlan([FromBody] CoordinatorPlan plan)
    {
        var result = await _coordinatorService.ExecutePlanAsync(plan);
        return Ok(result);
    }

    [HttpPost("handle")]
    public async Task<ActionResult<string>> HandleRequest([FromBody] CoordinateRequest request)
    {
        var result = await _coordinatorService.HandleRequestAsync(request.UserRequest);
        return Ok(result);
    }

    [HttpGet("instances")]
    public ActionResult<List<AgentInstance>> GetActiveInstances()
    {
        var instances = _executorService.GetActiveInstances();
        return Ok(instances);
    }

    [HttpGet("instances/{instanceId}")]
    public ActionResult<AgentInstance> GetInstance(string instanceId)
    {
        var instance = _executorService.GetAgentInstance(instanceId);
        if (instance == null) return NotFound();
        return Ok(instance);
    }

    [HttpPost("execute-task")]
    public async Task<ActionResult<AgentExecutionResult>> ExecuteTask([FromBody] AgentTask task)
    {
        var result = await _executorService.ExecuteTaskAsync(task);
        return Ok(result);
    }
}

public class FindAgentRequest
{
    public string Capability { get; set; } = string.Empty;
}

public class BestMatchRequest
{
    public string TaskDescription { get; set; } = string.Empty;
    public List<string> RequiredCapabilities { get; set; } = new();
}

public class CoordinateRequest
{
    public string UserRequest { get; set; } = string.Empty;
}
