using ForgeSelf.Api.Plugins.AgentHub.Models;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// Agent 注册表：登记本机/远端其它 agent，维护其交互口与能力画像。
/// </summary>
public interface IAgentRegistry
{
    /// <summary>取全部 agent（可按启用状态过滤）</summary>
    /// <param name="enabledOnly">是否只返回启用的</param>
    /// <returns>DTO 列表</returns>
    IReadOnlyList<AgentDto> List(Boolean enabledOnly = false);

    /// <summary>按主键取 agent（含交互口）</summary>
    /// <param name="id">主键</param>
    /// <returns>DTO；不存在返回 null</returns>
    AgentDto? Get(Int32 id);

    /// <summary>按显示名取 agent</summary>
    /// <param name="name">显示名</param>
    /// <returns>DTO；不存在返回 null</returns>
    AgentDto? GetByName(String name);

    /// <summary>新增 agent（未提供交互口时按 profile 预填）</summary>
    /// <param name="request">请求体</param>
    /// <returns>新建的 DTO</returns>
    AgentDto Create(AgentSaveRequest request);

    /// <summary>更新 agent（未提供交互口时保持原样）</summary>
    /// <param name="id">主键</param>
    /// <param name="request">请求体</param>
    /// <returns>更新后的 DTO；不存在返回 null</returns>
    AgentDto? Update(Int32 id, AgentSaveRequest request);

    /// <summary>删除 agent（级联删除其交互口）</summary>
    /// <param name="id">主键</param>
    /// <returns>是否删除成功</returns>
    Boolean Delete(Int32 id);

    /// <summary>设置授信（A+C 模型的「授信自动放行」）</summary>
    /// <param name="id">主键</param>
    /// <param name="trusted">是否授信</param>
    /// <param name="scopes">授信范围（权限类别，如 write_file,exec_command）</param>
    /// <returns>更新后的 DTO；不存在返回 null</returns>
    AgentDto? SetTrust(Int32 id, Boolean trusted, IReadOnlyList<String>? scopes);

    /// <summary>取某 agent 的默认交互口（优先 IsDefault，其次第一条）</summary>
    /// <param name="agentId">Agent 主键</param>
    /// <returns>交互口 DTO；无则返回 null</returns>
    AgentAccessPointDto? GetDefaultAccessPoint(Int32 agentId);

    /// <summary>按主键取交互口</summary>
    /// <param name="accessPointId">交互口主键</param>
    /// <returns>交互口 DTO；不存在返回 null</returns>
    AgentAccessPointDto? GetAccessPoint(Int32 accessPointId);

    /// <summary>按擅长标签与能力面选路（用于 delegate_task 未指定 agentId 时）</summary>
    /// <param name="facet">要求的能力面（如 F1_Driving）；null 表示不限制</param>
    /// <param name="tag">要求的擅长标签（模糊匹配）；null 表示不限制</param>
    /// <returns>排序后的候选（已按优先级倒序）；无候选返回空列表</returns>
    IReadOnlyList<AgentDto> SelectCandidates(String? facet, String? tag);

    /// <summary>更新交互口的健康状态（探测后回写）</summary>
    /// <param name="accessPointId">交互口主键</param>
    /// <param name="health">健康状态</param>
    /// <param name="version">探测到的版本</param>
    /// <param name="error">错误信息</param>
    void UpdateHealth(Int32 accessPointId, String health, String? version, String? error);
}
