using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;

namespace ForgeSelf.Api.Plugins.AgentHub.Controllers;

/// <summary>
/// Runtime 发现 / 探测面：经 <see cref="IAgentRuntime"/> 与 <see cref="OarWorkerClient"/>。
/// 路由前缀 <c>api/agent-hub/runtimes</c>。
/// </summary>
[ApiController]
[Route("api/agent-hub/runtimes")]
[Authorize("ApiKeyPolicy")]
public class AgentHubRuntimesController : ControllerBase
{
    private readonly IAgentRuntime _runtime;
    private readonly OarWorkerClient _worker;

    public AgentHubRuntimesController(IAgentRuntime runtime, OarWorkerClient worker)
    {
        _runtime = runtime;
        _worker = worker;
    }

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

    /// <summary>安装 + 认证只读探测（不自动 install）。</summary>
    [HttpPost("{id}/probe")]
    public async Task<ActionResult<ApiResponse<RuntimeProbeDto>>> Probe(String id, CancellationToken ct)
    {
        if (id.IsNullOrWhiteSpace())
            return BadRequest(ApiResponse<RuntimeProbeDto>.Error("runtime id 不能为空", 400));

        var started = _worker.IsAvailable || await _worker.EnsureStartedAsync(ct).ConfigureAwait(false);
        if (!started || !_worker.IsAvailable)
        {
            return StatusCode(503, ApiResponse<RuntimeProbeDto>.Error(
                _worker.UnavailableReason ?? "OAR Worker 不可用", 503));
        }

        try
        {
            var installation = await _worker.InstallationAsync(id, ct).ConfigureAwait(false);
            JsonElement? auth = null;
            String? authError = null;
            try
            {
                auth = await _worker.AuthAsync(id, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                authError = ex.Message;
            }

            var dto = new RuntimeProbeDto
            {
                RuntimeId = id,
                InstallationJson = installation.GetRawText(),
                AuthJson = auth?.GetRawText(),
                AuthError = authError,
            };
            return Ok(ApiResponse<RuntimeProbeDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<RuntimeProbeDto>.Error(ex.Message, 400));
        }
    }

    /// <summary>列出 Runtime 模型（scripted 可能 not_supported）。</summary>
    [HttpGet("{id}/models")]
    public async Task<ActionResult<ApiResponse<RuntimeModelsDto>>> Models(String id, CancellationToken ct)
    {
        if (id.IsNullOrWhiteSpace())
            return BadRequest(ApiResponse<RuntimeModelsDto>.Error("runtime id 不能为空", 400));

        var started = _worker.IsAvailable || await _worker.EnsureStartedAsync(ct).ConfigureAwait(false);
        if (!started || !_worker.IsAvailable)
        {
            return StatusCode(503, ApiResponse<RuntimeModelsDto>.Error(
                _worker.UnavailableReason ?? "OAR Worker 不可用", 503));
        }

        try
        {
            var el = await _worker.ModelsAsync(id, ct).ConfigureAwait(false);
            return Ok(ApiResponse<RuntimeModelsDto>.Ok(new RuntimeModelsDto
            {
                RuntimeId = id,
                PayloadJson = el.GetRawText(),
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<RuntimeModelsDto>.Error(ex.Message, 400));
        }
    }
}

/// <summary>Runtime probe 结果（installation / auth 分列 JSON 字符串）。</summary>
public class RuntimeProbeDto
{
    public String RuntimeId { get; set; } = String.Empty;
    public String? InstallationJson { get; set; }
    public String? AuthJson { get; set; }
    public String? AuthError { get; set; }
}

/// <summary>Runtime models 原样可解释载荷。</summary>
public class RuntimeModelsDto
{
    public String RuntimeId { get; set; } = String.Empty;
    public String? PayloadJson { get; set; }
}
