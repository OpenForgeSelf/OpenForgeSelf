using ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 执行记录服务（PILOT-054 · FR-4）。<b>append-only</b>：只提供追加与读取，没有更新与删除入口 ——
/// 「agent 说它做完了」必须留下可回放的一行，而不是覆盖上一行。
/// </summary>
public interface ITaskExecutionService
{
    /// <summary>追加一条记录。<paramref name="actor"/> 空时按 <paramref name="defaultActor"/> 落。</summary>
    Task<TodoOpResult> AppendAsync(int todoId, CreateTaskExecutionRequest request, string? defaultActor = null);

    /// <summary>按 Seq 升序分页读取某任务的记录。</summary>
    Task<PagedResult<TaskExecutionDto>> ListAsync(int todoId, int page = 1, int pageSize = 50);

    /// <summary>内部用：无请求体的自动留痕（领取/下发/委派等系统动作）。失败不抛，返回 null 供调用方降级提示。</summary>
    Task<TaskExecutionDto?> AppendSystemAsync(int todoId, string actor, string action, string? detail = null,
        string? result = null, string? verification = null, string? risks = null, string? residuals = null,
        string? evidence = null, string? nextStep = null, string? blockReason = null,
        int stageFrom = -1, int stageTo = -1, long elapsedMs = 0);
}
