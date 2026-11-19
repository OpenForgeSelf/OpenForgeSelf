using ForgeSelf.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.AgentHub.Controllers;

/// <summary>
/// Runtime 发现面（P3）：经 <see cref="IAgentRuntime"/> 列出可用 Agent/Runtime。
/// 路由前缀 <c>api/agent-hub/runtimes</c>。
/// </summary>
[ApiController]
[Route("api/agent-hub/runtimes")]
public class AgentHubRuntimesController : ControllerBase
{
    private readonly IAgentRuntime _runtime;

    public AgentHubRuntimesController(IAgentRuntime runtime) => _runtime = runtime;

    /// <summary>列出 Runtime / Agent 描述（含 Transport、RuntimeId、可用性）。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentDescriptor>>>> List(CancellationToken ct)
    {
        var list = await _runtime.ListAgentsAsync(ct).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<AgentDescriptor>>.Ok(list, $"共 {list.Count} 项"));
    }

    /// <summary>按 RuntimeId 取详情（从列表过滤；无则 404）。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<AgentDescriptor>>> Get(String id, CancellationToken ct)
    {
        var list = await _runtime.ListAgentsAsync(ct).ConfigureAwait(false);
        var hit = list.FirstOrDefault(x =>
            String.Equals(x.RuntimeId, id, StringComparison.OrdinalIgnoreCase)
            || String.Equals(x.AgentId.ToString(), id, StringComparison.Ordinal));
        return hit == null
            ? NotFound(ApiResponse<AgentDescriptor>.Error($"runtime `{id}` 不存在", 404))
            : Ok(ApiResponse<AgentDescriptor>.Ok(hit));
    }
}
