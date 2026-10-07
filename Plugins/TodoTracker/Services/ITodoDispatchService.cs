using ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 下发与委派服务（PILOT-054 · FR-1/FR-5/FR-6）。
/// 只管「把任务交出去」与「把结果收回来」，任务本身的增删改查在 <see cref="ITodoService"/>。
/// </summary>
public interface ITodoDispatchService
{
    /// <summary>下发预览（提示词 + 载荷 + 缺口 + 委派可用性）。按 id；任务不存在返回 null（控制器映射 404）。</summary>
    Task<DispatchPreviewDto?> PreviewAsync(int id, string? baseUrl);

    /// <summary>下发预览（按 taskKey，agent 侧也能读到自己被派了什么）。不存在返回 null。</summary>
    Task<DispatchPreviewDto?> PreviewByKeyAsync(string? taskKey, string? baseUrl);

    /// <summary>人工/系统下发：校验必填 → Ready/Dispatched → Draft 起步自动补齐 → 留痕。</summary>
    Task<TodoOpResult> DispatchAsync(int id, string? assignee, string actor);

    /// <summary>
    /// 一键交给 AgentHub 执行（经 IAgentDelegation 接缝）。
    /// <paramref name="baseUrl"/> 只影响提示词里「完成后怎么回报」的地址段，留空则写占位符。
    /// </summary>
    Task<DelegateToAgentResultDto> DelegateAsync(int id, DelegateToAgentRequest? request, string actor, string? baseUrl = null);

    /// <summary>回读委派任务状态。</summary>
    Task<AgentStatusDto> AgentStatusAsync(int id);

    /// <summary>把委派结果落成一条执行记录。</summary>
    Task<RecordAgentResultDto> RecordAgentResultAsync(int id, string actor);

    /// <summary>agent 领取下一条已下发任务（原子：领到即置 Running 并留痕）。无任务时 Ok=true 且 Data=null。</summary>
    Task<TodoOpResult> ClaimNextAsync(string? assignee, int projectId, string actor);
}
