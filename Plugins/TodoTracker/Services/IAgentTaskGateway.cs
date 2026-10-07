using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>委派接缝的调用结果（Success=false 时 Error 必填，供控制器映射 400/503）。</summary>
/// <param name="Success">是否成功。</param>
/// <param name="Error">失败原因原文。</param>
/// <param name="SeamMissing">失败是否因接缝缺席（未装/未启用 agent-hub）⇒ 控制器映射 503，而非 400。</param>
/// <param name="Value">结果载荷（失败时为 null）。</param>
public readonly record struct GatewayResult<T>(bool Success, string? Error, bool SeamMissing, T? Value)
{
    public static GatewayResult<T> Ok(T value) => new(true, null, false, value);

    public static GatewayResult<T> Failed(string error) => new(false, error, false, default);

    public static GatewayResult<T> Missing(string error) => new(false, error, true, default);
}

/// <summary>
/// 委派接缝消费侧（PILOT-054 · FR-6）。
/// 本插件<b>只经 IAgentDelegation 能力接缝</b>用 AgentHub，不经 HTTP 直连
/// （architecture-design 铁律 2/3；接缝登记见 docs/01-architecture/host-capability-seams.md）。
/// </summary>
public interface IAgentTaskGateway
{
    /// <summary>接缝是否在场（未装/未启用 agent-hub 时 false，界面据此置灰「一键执行」）。</summary>
    bool IsAvailable { get; }

    /// <summary>可用 agent 候选（接缝缺席返回空集）。</summary>
    IReadOnlyList<AgentDelegationAgent> AvailableAgents();

    /// <summary>提交委派（入队即返回，不阻塞等 agent 跑完）。</summary>
    Task<GatewayResult<AgentDelegationOutcome>> SubmitAsync(AgentDelegationRequest request);

    /// <summary>按委派 taskKey 读回状态快照。找不到 ⇒ Success=false 且 <see cref="GatewayResult{T}.SeamMissing"/>=false。</summary>
    Task<GatewayResult<AgentDelegationSnapshot>> Query(string taskKey);
}
