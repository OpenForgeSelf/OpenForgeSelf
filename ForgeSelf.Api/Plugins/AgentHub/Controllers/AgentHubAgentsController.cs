using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Controllers;

/// <summary>
/// Agent 注册表接口：登记、扫描、探测、授信。
/// 路由前缀 <c>api/agent-hub/agents</c>。
/// </summary>
[ApiController]
[Route("api/agent-hub/agents")]
public class AgentHubAgentsController : ControllerBase
{
    private readonly IAgentRegistry _registry;
    private readonly IAgentProbeService _probe;

    public AgentHubAgentsController(IAgentRegistry registry, IAgentProbeService probe)
    {
        _registry = registry;
        _probe = probe;
    }

    /// <summary>列出已登记的 agent</summary>
    /// <param name="enabledOnly">是否只返回启用的</param>
    /// <returns>agent 列表</returns>
    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<AgentDto>>> List([FromQuery] Boolean enabledOnly = false)
    {
        var list = _registry.List(enabledOnly);
        return Ok(ApiResponse<IReadOnlyList<AgentDto>>.Ok(list, $"共 {list.Count} 个 agent"));
    }

    /// <summary>取单个 agent 详情（含交互口与能力矩阵）</summary>
    /// <param name="id">主键</param>
    /// <returns>agent</returns>
    [HttpGet("{id:int}")]
    public ActionResult<ApiResponse<AgentDto>> Get(Int32 id)
    {
        var agent = _registry.Get(id);
        return agent == null
            ? NotFound(ApiResponse<AgentDto>.Error($"agent #{id} 不存在", 404))
            : Ok(ApiResponse<AgentDto>.Ok(agent));
    }

    /// <summary>新增 agent（不传交互口时按 profile 预填）</summary>
    /// <param name="request">请求体</param>
    /// <returns>新建的 agent</returns>
    [HttpPost]
    public ActionResult<ApiResponse<AgentDto>> Create([FromBody] AgentSaveRequest request)
    {
        try
        {
            var agent = _registry.Create(request);
            return Ok(ApiResponse<AgentDto>.Ok(agent, $"已登记 agent「{agent.Name}」"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AgentHub] 新增 agent 失败: {0}", ex.Message);
            return BadRequest(ApiResponse<AgentDto>.Error(ex.Message, 400));
        }
    }

    /// <summary>更新 agent</summary>
    /// <param name="id">主键</param>
    /// <param name="request">请求体</param>
    /// <returns>更新后的 agent</returns>
    [HttpPut("{id:int}")]
    public ActionResult<ApiResponse<AgentDto>> Update(Int32 id, [FromBody] AgentSaveRequest request)
    {
        try
        {
            var agent = _registry.Update(id, request);
            return agent == null
                ? NotFound(ApiResponse<AgentDto>.Error($"agent #{id} 不存在", 404))
                : Ok(ApiResponse<AgentDto>.Ok(agent, "已保存"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AgentHub] 更新 agent 失败: {0}", ex.Message);
            return BadRequest(ApiResponse<AgentDto>.Error(ex.Message, 400));
        }
    }

    /// <summary>删除 agent（级联删除其交互口）</summary>
    /// <param name="id">主键</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    public ActionResult<ApiResponse> Delete(Int32 id)
    {
        var ok = _registry.Delete(id);
        return ok
            ? Ok(ApiResponse.Ok("已删除"))
            : NotFound(ApiResponse.Error($"agent #{id} 不存在", 404));
    }

    /// <summary>设置授信（A+C 模型的「授信自动放行」；必须给范围）</summary>
    /// <param name="id">主键</param>
    /// <param name="request">授信请求</param>
    /// <returns>更新后的 agent</returns>
    [HttpPost("{id:int}/trust")]
    public ActionResult<ApiResponse<AgentDto>> SetTrust(Int32 id, [FromBody] TrustRequest request)
    {
        try
        {
            var agent = _registry.SetTrust(id, request.Trusted, request.Scopes);
            return agent == null
                ? NotFound(ApiResponse<AgentDto>.Error($"agent #{id} 不存在", 404))
                : Ok(ApiResponse<AgentDto>.Ok(agent,
                    request.Trusted ? $"已授信（范围：{String.Join(", ", request.Scopes ?? [])}）" : "已取消授信"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<AgentDto>.Error(ex.Message, 400));
        }
    }

    /// <summary>扫描本机已安装的其它 agent（不自动登记）</summary>
    /// <returns>候选列表</returns>
    [HttpGet("discover")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DiscoveredAgentDto>>>> Discover()
    {
        var list = await _probe.DiscoverAsync();
        return Ok(ApiResponse<IReadOnlyList<DiscoveredAgentDto>>.Ok(list, $"扫描到 {list.Count} 个候选"));
    }

    /// <summary>探测某个 agent（存在性 / 版本 / profile 断言）</summary>
    /// <param name="id">agent 主键</param>
    /// <returns>探测结果</returns>
    [HttpPost("{id:int}/probe")]
    public async Task<ActionResult<ApiResponse<ProbeResultDto>>> Probe(Int32 id)
    {
        var result = await _probe.ProbeAsync(id);
        var msg = result.Health switch
        {
            "Ok" => $"可用（版本 {result.Version}）",
            "Degraded" => $"可用但降级：{result.Error ?? result.ProfileWarning}",
            _ => $"不可用：{result.Error}"
        };

        return Ok(ApiResponse<ProbeResultDto>.Ok(result, msg));
    }
}

/// <summary>授信请求体。</summary>
public class TrustRequest
{
    /// <summary>是否授信</summary>
    public Boolean Trusted { get; set; }

    /// <summary>授信范围（权限类别，如 write_file / exec_command；授信时必填）</summary>
    public List<String>? Scopes { get; set; }
}
