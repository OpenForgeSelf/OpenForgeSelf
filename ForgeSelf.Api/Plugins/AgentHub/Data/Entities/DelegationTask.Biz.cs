using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.AgentHub.Entities;

/// <summary>
/// 委派任务实体业务扩展（XCode 分部类规范：泛型基类与业务逻辑放 Biz 侧）。
///
/// ⚠ 本文件由人工维护，XCode 生成器刷新 <c>DelegationTask.cs</c> 时**不会**覆盖本文件。
/// 自定义查询一律写在此处，不要写进生成文件（会被下一次 xcode 覆写掉）。
/// </summary>
public partial class DelegationTask : Entity<DelegationTask>
{
    #region 对象操作
    /// <summary>验证并修补数据（插入时生成对外任务标识、补时间戳）。</summary>
    /// <param name="method">添删改方法</param>
    /// <returns>验证结果</returns>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;

        if (AgentId <= 0) throw new ArgumentNullException(nameof(AgentId), "目标 Agent 不能为空！");

        if (!base.Valid(method)) return false;

        if (method == DataMethod.Insert)
        {
            // 对外统一用 GUID 标识，避免把自增主键暴露给工具与 UI
            if (TaskKey.IsNullOrEmpty()) TaskKey = Guid.NewGuid().ToString("N");
            if (Status.IsNullOrEmpty()) Status = "Queued";
            if (PermissionMode.IsNullOrEmpty()) PermissionMode = "read-only";
            if (CreateTime == DateTime.MinValue) CreateTime = DateTime.Now;
        }

        return true;
    }
    #endregion

    #region 业务操作
    /// <summary>是否为终态（终态不再接受状态推进）。</summary>
    /// <returns>是否终态</returns>
    public Boolean IsTerminal() => Status is "Succeeded" or "Failed" or "Cancelled" or "Timeout" or "Interrupted";

    /// <summary>是否为未结束态（宿主重启时需标记为 Interrupted，见 G5）。</summary>
    /// <returns>是否未结束</returns>
    public Boolean IsUnfinished() => !IsTerminal();
    #endregion

    #region 扩展查询
    /// <summary>按对外任务标识查找</summary>
    /// <param name="taskKey">任务标识（GUID）</param>
    /// <returns>实体对象</returns>
    public static DelegationTask FindByTaskKey(String taskKey)
    {
        if (taskKey.IsNullOrEmpty()) return null;

        return Find(_.TaskKey == taskKey);
    }

    /// <summary>按状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<DelegationTask> FindAllByStatus(String status)
    {
        if (status.IsNullOrEmpty()) return [];

        return FindAll(_.Status == status, _.Id.Desc(), null, 0, 0);
    }

    /// <summary>查找未结束的任务（宿主重启时用于标记 Interrupted，G5）</summary>
    /// <returns>实体列表</returns>
    public static IList<DelegationTask> FindAllUnfinished()
    {
        // 未结束 = Running / AwaitingPermission / Queued 三态
        return FindAll(_.Status == "Running" | _.Status == "AwaitingPermission" | _.Status == "Queued", null, null, 0, 0);
    }
    #endregion
}
