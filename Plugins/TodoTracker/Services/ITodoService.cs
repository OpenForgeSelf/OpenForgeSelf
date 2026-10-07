using ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 任务（待办）主服务：CRUD、查询、状态流转、项目关联、历史回填。
/// 下发/委派/领取在 <see cref="ITodoDispatchService"/>（职责分离，别把两类语义搅进一个类）。
/// </summary>
public interface ITodoService
{
    /// <summary>分页查询（条件对象版，支持阶段/项目/关键字）。</summary>
    Task<PagedResult<TodoDto>> GetTodosAsync(TaskQuery query);

    /// <summary>分页查询（既有三参形状，Home 面板与旧调用方在用）。</summary>
    Task<PagedResult<TodoDto>> GetTodosAsync(string? status = null, int page = 1, int pageSize = 20);

    Task<TodoDto?> GetTodoByIdAsync(int id);

    Task<TodoDto?> GetTodoByKeyAsync(string? taskKey);

    Task<TodoDto> CreateTodoAsync(CreateTodoRequest request);

    Task<TodoDto?> UpdateTodoAsync(int id, UpdateTodoRequest request);

    Task<bool> DeleteTodoAsync(int id);

    /// <summary>旧「标记完成」端点：内部同步 Stage=Done（语义保持）。</summary>
    Task<TodoDto?> CompleteTodoAsync(int id);

    /// <summary>旧「重新打开」端点：内部同步 Stage=Draft（语义保持）。</summary>
    Task<TodoDto?> ReopenTodoAsync(int id);

    /// <summary>状态流转（按 id）。非法流转 409 并列出可达目标。</summary>
    Task<TodoOpResult> ChangeStageAsync(int id, StageChangeRequest request, string actor);

    /// <summary>状态流转（按 taskKey，agent 回报面）。</summary>
    Task<TodoOpResult> ChangeStageByKeyAsync(string? taskKey, StageChangeRequest request, string actor);

    /// <summary>关联/换关联项目（路径任何写法皆可；归一后匹配宿主档案，未命中即登记）。</summary>
    Task<TodoOpResult> LinkProjectAsync(int id, LinkProjectRequest request);

    /// <summary>解除关联（只清任务上的投影字段，<b>不删</b>宿主项目档案）。</summary>
    Task<TodoOpResult> UnlinkProjectAsync(int id);

    /// <summary>历史行回填：补外部键 + 由旧 Status 推出 Stage（幂等，只动空值行）。返回被改的行数。</summary>
    Task<int> BackfillLegacyRowsAsync();
}
