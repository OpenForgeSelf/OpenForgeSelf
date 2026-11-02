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

    public AgentsController(IAgentRegistryService agentRegistry)
    {
        _agentRegistry = agentRegistry;
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

    // B5（2026-11-02-b5-legacy-cleanup，lead 拍板=删除）：AgentCoordinator 启发式旁路的
    // coordinate/execute/handle/instances/execute-task 端点已删除——零 UI/插件/IM 消费者，
    // 且 ad-hoc 工具循环无审批泵（安全缺口）。裁决留痕：docs/ai/pilot/2026-11-02-b5-legacy-cleanup/agenthub-adjudication.md。

    #region Stage 4：Agent CRUD（可编辑提示词）

    /// <summary>新建 Agent。</summary>
    [HttpPost]
    public ActionResult<AgentDefinition> CreateAgent([FromBody] AgentDefinition agent)
    {
        try
        {
            var created = _agentRegistry.CreateAgent(agent);
            return CreatedAtAction(nameof(GetAgent), new { agentId = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>更新 Agent（重点：可编辑 SystemPrompt）。</summary>
    [HttpPut("{agentId}")]
    public ActionResult<AgentDefinition> UpdateAgent(string agentId, [FromBody] AgentDefinition agent)
    {
        var updated = _agentRegistry.UpdateAgent(agentId, agent);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    /// <summary>删除 Agent。</summary>
    [HttpDelete("{agentId}")]
    public IActionResult DeleteAgent(string agentId)
    {
        var ok = _agentRegistry.DeleteAgent(agentId);
        if (!ok) return NotFound();
        return NoContent();
    }

    #endregion
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
