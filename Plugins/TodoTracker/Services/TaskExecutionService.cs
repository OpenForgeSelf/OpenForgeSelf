using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using NewLife.Data;
using NewLife.Log;
using XCode;
// 字段访问器 _.TodoId（只导 TaskExecution 的那份 _，避免与 Todo._ 同名冲突）
using static ForgeSelf.Api.Plugins.TodoTracker.Entities.TaskExecution;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 执行记录实现（PILOT-054 · FR-4/BR-4）。
///
/// 两条口径值得写明：
/// <list type="bullet">
/// <item><b>Seq 直查库</b>：用 <see cref="TaskExecution.MaxSeqOf"/>（内部是 <c>FindAll</c> 直查），
/// 不读 <c>Meta.Cache</c> —— 缓存是 AsyncLocal 进程内的，跨请求/跨库时可能是别的库的行（铁律 11）。</item>
/// <item><b>带 StageTo 的记录 = 一次调用同时改状态</b>（FR-4.3），否则会出现"状态变了但台账没留痕"。</item>
/// </list>
/// </summary>
public class TaskExecutionService : ITaskExecutionService
{
    private const int MaxActionLength = 300;
    private const int MaxPageSize = 100;

    public Task<PagedResult<TaskExecutionDto>> ListAsync(int todoId, int page = 1, int pageSize = 50)
    {
        if (todoId <= 0) return Task.FromResult(new PagedResult<TaskExecutionDto>());

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        // 分页与计数都交给库（与本服务 GetTodosAsync 同一形状）：
        // 读全量再 Skip/Take 会把"一条任务的记录上限"变成隐性分页边界，Total 也跟着失真。
        var pageParam = new PageParameter
        {
            PageIndex = page - 1,
            PageSize = pageSize,
            Sort = "Seq ASC",
            RetrieveTotalCount = true
        };

        var rows = TaskExecution.FindAll(_.TodoId == todoId, pageParam);

        return Task.FromResult(new PagedResult<TaskExecutionDto>
        {
            Items = rows.Select(ToDto).ToList(),
            Total = (int)pageParam.TotalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<TodoOpResult> AppendAsync(int todoId, CreateTaskExecutionRequest request, string? defaultActor = null)
    {
        if (request == null) return TodoOpResult.Invalid("请求体不能为空");
        if (todoId <= 0) return TodoOpResult.Invalid("todoId 必须为正整数");

        var todo = Todo.FindById(todoId);
        if (todo == null) return TodoOpResult.NotFound($"任务 {todoId} 不存在");

        var action = (request.Action ?? string.Empty).Trim();
        if (action.Length == 0) return TodoOpResult.Invalid("action（做了什么操作）不能为空");
        if (action.Length > MaxActionLength) return TodoOpResult.Invalid($"action 长度不能超过 {MaxActionLength} 字符");

        // 状态流转（可选）：先验，验不过整笔不写，避免出现"记录写进去了、状态没动"的半截事实
        var stageToValue = -1;
        var stageFromValue = todo.Stage;
        if (!string.IsNullOrWhiteSpace(request.StageTo))
        {
            if (!TodoStage.TryParse(request.StageTo, out var target))
                return TodoOpResult.Invalid($"stageTo 无效：{request.StageTo}。可用值：{string.Join("/", TodoStage.All.Select(TodoStage.ToName))}");
            if (target == TodoStage.Blocked && string.IsNullOrWhiteSpace(request.BlockReason))
                return TodoOpResult.Invalid("流转到 Blocked 必须给出 blockReason（阻塞在哪、缺什么）");
            if (!TodoStage.CanTransit(todo.Stage, target))
                return TodoOpResult.Conflict($"非法状态流转：{TodoStage.ToName(todo.Stage)} → {TodoStage.ToName(target)}。{TodoStage.DescribeAllowed(todo.Stage)}");

            stageToValue = target;
        }

        var files = FileChangeList.ToStorage(request.FilesChanged, request.FilesChangedText);
        var actor = FirstNonBlank(request.Actor, defaultActor, "manual");

        var entity = new TaskExecution
        {
            TodoId = todoId,
            Seq = TaskExecution.MaxSeqOf(todoId) + 1,
            Actor = actor,
            Action = action,
            Detail = request.Detail ?? string.Empty,
            Result = request.Result ?? string.Empty,
            FilesChanged = files,
            Verification = request.Verification ?? string.Empty,
            Risks = request.Risks ?? string.Empty,
            Residuals = request.Residuals ?? string.Empty,
            Evidence = request.Evidence ?? string.Empty,
            StageFrom = stageToValue >= 0 ? stageFromValue : -1,
            StageTo = stageToValue,
            ElapsedMs = ClampElapsed(request.ElapsedMs),
            BlockReason = request.BlockReason ?? string.Empty,
            NextStep = request.NextStep ?? string.Empty,
            CreatedAt = DateTime.Now
        };

        // 状态与记录必须同批落库：先写任务（改了 Stage 就要 Update，否则 ApplyStage 只停在内存里，
        // 界面会出现"台账多了一条状态流转记录、任务阶段却纹丝不动"的分裂事实）
        if (stageToValue >= 0)
        {
            ApplyStage(todo, stageToValue);
            await todo.UpdateAsync();
        }

        await entity.InsertAsync();
        XTrace.Log.Info("[todo-tracker] 执行记录已写入：todo={0} seq={1} actor={2} action={3}", todoId, entity.Seq, actor, action);

        return TodoOpResult.Success(TodoProjection.ToDto(todo));
    }

    public async Task<TaskExecutionDto?> AppendSystemAsync(int todoId, string actor, string action, string? detail = null,
        string? result = null, string? verification = null, string? risks = null, string? residuals = null,
        string? evidence = null, string? nextStep = null, string? blockReason = null,
        int stageFrom = -1, int stageTo = -1, long elapsedMs = 0)
    {
        try
        {
            var entity = new TaskExecution
            {
                TodoId = todoId,
                Seq = TaskExecution.MaxSeqOf(todoId) + 1,
                Actor = actor,
                Action = action,
                Detail = detail ?? string.Empty,
                Result = result ?? string.Empty,
                Verification = verification ?? string.Empty,
                Risks = risks ?? string.Empty,
                Residuals = residuals ?? string.Empty,
                Evidence = evidence ?? string.Empty,
                StageFrom = stageFrom,
                StageTo = stageTo,
                ElapsedMs = ClampElapsed(elapsedMs),
                BlockReason = blockReason ?? string.Empty,
                NextStep = nextStep ?? string.Empty,
                CreatedAt = DateTime.Now
            };
            await entity.InsertAsync();
            return ToDto(entity);
        }
        catch (Exception ex)
        {
            // 系统留痕失败绝不能把主操作判成失败（主操作已完成），但绝不静默吞：告警 + 返回 null 由调用方给提示
            XTrace.Log.Warn("[todo-tracker] 自动执行记录写入失败（todo={0}，动作={1}）：{2}", todoId, action, ex.Message);
            return null;
        }
    }

    /// <summary>把阶段落到实体（含旧二元 Status 派生与 CompletedAt 时序）。调用方随后统一 Update。</summary>
    private static void ApplyStage(Todo todo, int target)
    {
        todo.Stage = target;
        todo.Status = TodoStage.ToLegacyStatus(target);
        todo.CompletedAt = target == TodoStage.Done ? DateTime.Now : target == TodoStage.Draft ? DateTime.MinValue : todo.CompletedAt;
        todo.UpdatedAt = DateTime.Now;
    }

    private static int ClampElapsed(long? ms) => ms switch { null => 0, < 0 => 0, > int.MaxValue => int.MaxValue, _ => (int)ms!.Value };

    private static string FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }

    private static TaskExecutionDto ToDto(TaskExecution e) => new()
    {
        Id = e.Id,
        TodoId = e.TodoId,
        Seq = e.Seq,
        Actor = e.Actor ?? string.Empty,
        Action = e.Action ?? string.Empty,
        Detail = e.Detail ?? string.Empty,
        Result = e.Result ?? string.Empty,
        FilesChanged = FileChangeList.FromStorage(e.FilesChanged).ToList(),
        FilesChangedRaw = FileChangeList.ToRawText(e.FilesChanged),
        Verification = e.Verification ?? string.Empty,
        Risks = e.Risks ?? string.Empty,
        Residuals = e.Residuals ?? string.Empty,
        Evidence = e.Evidence ?? string.Empty,
        StageFrom = e.StageFrom >= 0 ? TodoStage.ToName(e.StageFrom) : string.Empty,
        StageTo = e.StageTo >= 0 ? TodoStage.ToName(e.StageTo) : string.Empty,
        ElapsedMs = e.ElapsedMs,
        BlockReason = e.BlockReason ?? string.Empty,
        NextStep = e.NextStep ?? string.Empty,
        CreatedAt = e.CreatedAt
    };
}
