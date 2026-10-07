namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 任务列表查询条件（值对象）。用它的理由：查询参数从 3 个涨到 6 个之后，
/// 位置参数的可选默认值会让调用点读起来全是逗号，命名字段比位置参数更不容易错位。
/// </summary>
public sealed class TaskQuery
{
    /// <summary>旧二元状态过滤（Pending/Completed）；null=不过滤。Home 面板仍在用这一维。</summary>
    public string? Status { get; init; }

    /// <summary>下发阶段过滤；null=不过滤。</summary>
    public int? Stage { get; init; }

    /// <summary>项目过滤；0=全部项目。筛「未关联」由调用方用 <see cref="UnlinkedOnly"/>。</summary>
    public int ProjectId { get; init; }

    /// <summary>只看未关联项目（ProjectId=0）的任务。</summary>
    public bool UnlinkedOnly { get; init; }

    /// <summary>关键字（标题/备注/目标模糊匹配）。</summary>
    public string? Keyword { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
