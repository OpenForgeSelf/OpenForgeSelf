using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 历史行回填（PILOT-054）。扩列之前入库的行没有 <c>TaskKey</c>，也没有 <c>Stage</c>，
/// 而新读的界面/新接口都按这两列工作 —— 不回填就会出现「老待办显示成草稿、agent 无法按 key 引用」。
///
/// 做成静态、可重入的原因：<see cref="TodoTrackerPlugin.Apply"/> 时期只有 <c>IServiceCollection</c>
/// （还没建成 provider），拿不到 <see cref="ITodoService"/> 实例；而这件事只碰实体，不需要依赖注入。
/// 因此 <see cref="TodoService.BackfillLegacyRowsAsync"/> 只是转调这里，两个入口共用一套规则，不留第二份真相。
/// </summary>
public static class TodoBackfill
{
    /// <summary>
    /// 幂等回填：只改需要改的行。
    /// ① <c>TaskKey</c> 空 ⇒ 补 GUID("N")；
    /// ② <c>Status=Completed</c> 但 <c>Stage!=Done</c> ⇒ 落回 Done（否则"已完成"与"草稿"自相矛盾），
    ///    <c>CompletedAt</c> 缺失时用 <c>UpdatedAt</c> 兜底（比 MinValue 更贴近事实）；
    /// ③ <c>PermissionMode</c> 空 ⇒ 补 <c>read-only</c>（一键委派的默认值，缺它会把空串当模式发出去）。
    /// </summary>
    /// <returns>被修补的行数。</returns>
    public static int Run()
    {
        var fixedCount = 0;
        try
        {
            foreach (var todo in Todo.FindAll())
            {
                var dirty = false;

                if (string.IsNullOrWhiteSpace(todo.TaskKey))
                {
                    todo.TaskKey = Guid.NewGuid().ToString("N");
                    dirty = true;
                }

                if (todo.Status == TodoStatus.CompletedValue && todo.Stage != TodoStage.Done)
                {
                    todo.Stage = TodoStage.Done;
                    if (todo.CompletedAt == DateTime.MinValue) todo.CompletedAt = todo.UpdatedAt;
                    dirty = true;
                }

                if (string.IsNullOrWhiteSpace(todo.PermissionMode))
                {
                    todo.PermissionMode = "read-only";
                    dirty = true;
                }

                if (!dirty) continue;

                todo.Update();
                fixedCount++;
            }
        }
        catch (Exception ex)
        {
            // 回填失败不影响读写主链路（读路径 TodoProjection 仍会补内存键），但必须留下可见告警
            XTrace.Log.Warn("[todo-tracker] 历史行回填未完成（已处理 {0} 行）：{1}", fixedCount, ex.Message);
        }

        if (fixedCount > 0)
            XTrace.Log.Info("[todo-tracker] 历史行回填：{0} 行已补齐 taskKey/stage/permissionMode", fixedCount);

        return fixedCount;
    }
}
