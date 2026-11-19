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

    /// <summary>列出 Runtime 模型（未安装 / unsupported 原样返回，禁止假成功）。</summary>
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

    /// <summary>只读安装计划（不执行）。</summary>
    [HttpGet("{id}/install-plan")]
    public async Task<ActionResult<ApiResponse<RuntimeInstallPlanDto>>> InstallPlan(String id, CancellationToken ct)
    {
        if (id.IsNullOrWhiteSpace())
            return BadRequest(ApiResponse<RuntimeInstallPlanDto>.Error("runtime id 不能为空", 400));

        var started = _worker.IsAvailable || await _worker.EnsureStartedAsync(ct).ConfigureAwait(false);
        if (!started || !_worker.IsAvailable)
        {
            return StatusCode(503, ApiResponse<RuntimeInstallPlanDto>.Error(
                _worker.UnavailableReason ?? "OAR Worker 不可用", 503));
        }

        try
        {
            var el = await _worker.InstallPlanAsync(id, ct).ConfigureAwait(false);
            return Ok(ApiResponse<RuntimeInstallPlanDto>.Ok(new RuntimeInstallPlanDto
            {
                RuntimeId = id,
                PlanJson = el.GetRawText(),
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<RuntimeInstallPlanDto>.Error(ex.Message, 400));
        }
    }

    /// <summary>执行安装（必须 confirmed=true；禁止静默自动装）。</summary>
    [HttpPost("{id}/install")]
    public async Task<ActionResult<ApiResponse<RuntimeInstallResultDto>>> Install(
        String id,
        [FromBody] RuntimeInstallRequest? body,
        CancellationToken ct)
    {
        if (id.IsNullOrWhiteSpace())
            return BadRequest(ApiResponse<RuntimeInstallResultDto>.Error("runtime id 不能为空", 400));
        if (body == null || !body.Confirmed)
        {
            return BadRequest(ApiResponse<RuntimeInstallResultDto>.Error(
                "安装须用户确认：请求体 confirmed 必须为 true", 400));
        }

        var started = _worker.IsAvailable || await _worker.EnsureStartedAsync(ct).ConfigureAwait(false);
        if (!started || !_worker.IsAvailable)
        {
            return StatusCode(503, ApiResponse<RuntimeInstallResultDto>.Error(
                _worker.UnavailableReason ?? "OAR Worker 不可用", 503));
        }

        try
        {
            var el = await _worker.InstallAsync(id, body.Line, ct).ConfigureAwait(false);
            return Ok(ApiResponse<RuntimeInstallResultDto>.Ok(new RuntimeInstallResultDto
            {
                RuntimeId = id,
                ResultJson = el.GetRawText(),
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<RuntimeInstallResultDto>.Error(ex.Message, 400));
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

/// <summary>Runtime installPlan 原样 JSON。</summary>
public class RuntimeInstallPlanDto
{
    public String RuntimeId { get; set; } = String.Empty;
    public String? PlanJson { get; set; }
}

/// <summary>安装执行请求：必须 confirmed。</summary>
public class RuntimeInstallRequest
{
    public Boolean Confirmed { get; set; }
    public String? Line { get; set; }
}

/// <summary>Runtime install 结果原样 JSON。</summary>
public class RuntimeInstallResultDto
{
    public String RuntimeId { get; set; } = String.Empty;
    public String? ResultJson { get; set; }
}
