using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 实体 → 出参的唯一投影点（PILOT-054）。
///
/// 抽出来的原因：任务投影同时被 <see cref="TodoService"/>（列表/详情）与
/// <see cref="TodoDispatchService"/>（下发/领取/回报）使用；两处各写一份"读时补键"这类规则，
/// 迟早漂（一处补了 TaskKey 另一处没补，agent 就会拿到空 taskKey 无法回报）。
/// </summary>
internal static class TodoProjection
{
    /// <summary>投影为出参。<paramref name="projectName"/>/<paramref name="recordCount"/> 由调用方批量补齐（避免逐行查）。</summary>
    internal static TodoDto ToDto(Todo todo, string? projectName = null, int recordCount = 0)
    {
        // 读时补外部键：只补内存不写库。本次操作若顺带落库（Update），新键一起持久化；
        // 纯读不落库也无害 —— 真正要写库的历史行走 BackfillAsync。
        if (string.IsNullOrWhiteSpace(todo.TaskKey)) todo.TaskKey = Guid.NewGuid().ToString("N");

        var stage = todo.Stage;
        return new TodoDto
        {
            Id = todo.Id,
            TaskKey = todo.TaskKey,
            Title = todo.Title,
            Remark = string.IsNullOrEmpty(todo.Remark) ? null : todo.Remark,
            Status = TodoStatus.ToName(TodoStage.ToLegacyStatus(stage)),
            Stage = TodoStage.ToName(stage),
            StageLabel = TodoStage.ToLabel(stage),
            Priority = todo.Priority is >= 1 and <= 3 ? todo.Priority : 1,
            Assignee = todo.Assignee ?? string.Empty,
            Objective = todo.Objective ?? string.Empty,
            Content = todo.Content ?? string.Empty,
            AllowedScope = todo.AllowedScope ?? string.Empty,
            ForbiddenScope = todo.ForbiddenScope ?? string.Empty,
            Acceptance = todo.Acceptance ?? string.Empty,
            Verification = todo.Verification ?? string.Empty,
            ProjectId = todo.ProjectId,
            ProjectRoot = todo.ProjectRoot ?? string.Empty,
            ProjectPathRaw = todo.ProjectPathRaw ?? string.Empty,
            ProjectName = projectName ?? string.Empty,
            ArtifactRef = todo.ArtifactRef ?? string.Empty,
            DueDate = todo.DueDate == DateTime.MinValue ? null : todo.DueDate,
            DispatchedAt = todo.DispatchedAt == DateTime.MinValue ? null : todo.DispatchedAt,
            AgentTaskKey = todo.AgentTaskKey ?? string.Empty,
            AgentId = todo.AgentId,
            AgentEngine = string.IsNullOrEmpty(todo.AgentEngine) ? "agenthub" : todo.AgentEngine,
            PermissionMode = string.IsNullOrEmpty(todo.PermissionMode) ? "read-only" : todo.PermissionMode,
            RecordCount = recordCount,
            AllowedTargets = TodoStage.NextOf(stage).Select(TodoStage.ToName).ToList(),
            CreatedAt = todo.CreatedAt,
            UpdatedAt = todo.UpdatedAt,
            CompletedAt = todo.CompletedAt == DateTime.MinValue ? null : todo.CompletedAt,
            Missing = DispatchPayloadBuilder.Missing(ToPayload(todo)).ToList()
        };
    }

    /// <summary>投影为下发载荷（<b>含补好的 TaskKey</b>，所以必须先跑 ToDto 再取载荷）。</summary>
    internal static DispatchPayloadBuilder.TaskPayload ToPayload(Todo todo) => new(
        Id: todo.Id,
        TaskKey: todo.TaskKey ?? string.Empty,
        Title: todo.Title ?? string.Empty,
        Objective: todo.Objective ?? string.Empty,
        Content: todo.Content ?? string.Empty,
        Acceptance: todo.Acceptance ?? string.Empty,
        Verification: todo.Verification ?? string.Empty,
        AllowedScope: todo.AllowedScope ?? string.Empty,
        ForbiddenScope: todo.ForbiddenScope ?? string.Empty,
        Priority: todo.Priority,
        Stage: todo.Stage,
        ProjectRoot: todo.ProjectRoot ?? string.Empty,
        ProjectPathRaw: todo.ProjectPathRaw ?? string.Empty,
        ArtifactRef: todo.ArtifactRef ?? string.Empty,
        Assignee: todo.Assignee ?? string.Empty,
        PermissionMode: string.IsNullOrEmpty(todo.PermissionMode) ? "read-only" : todo.PermissionMode);
}

/// <summary>
/// 字典取值扩展。名字叫 Pick 而不是再用 GetValueOrDefault：
/// NewLife 也提供了一个 GetValueOrDefault(IReadOnlyDictionary) 扩展，两者同域时编译器推断不出类型参数
/// （实测 CS0411），换一个独有名比到处写全限定名干净。
/// </summary>
internal static class TodoMapExtensions
{
    internal static V Pick<K, V>(this IReadOnlyDictionary<K, V> map, K key) => map.TryGetValue(key, out var value) ? value : default!;
}