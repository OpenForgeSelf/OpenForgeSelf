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
/// 委派事件实体业务扩展（XCode 分部类规范：泛型基类与业务逻辑放 Biz 侧）。
///
/// ⚠ 本文件由人工维护，XCode 生成器刷新 <c>DelegationEvent.cs</c> 时**不会**覆盖本文件。
/// 自定义查询一律写在此处，不要写进生成文件（会被下一次 xcode 覆写掉）。
/// </summary>
public partial class DelegationEvent : Entity<DelegationEvent>
{
    #region 对象操作
    /// <summary>验证并修补数据（插入时补时间戳）。</summary>
    /// <param name="method">添删改方法</param>
    /// <returns>验证结果</returns>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;

        if (TaskId <= 0) throw new ArgumentNullException(nameof(TaskId), "所属任务不能为空！");
        if (Type.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Type), "事件类型不能为空！");

        if (!base.Valid(method)) return false;

        if (method == DataMethod.Insert && Timestamp == DateTime.MinValue) Timestamp = DateTime.Now;

        return true;
    }
    #endregion

    #region 扩展查询
    /// <summary>取某任务的全部事件（按序号升序）</summary>
    /// <param name="taskId">任务主键</param>
    /// <returns>实体列表</returns>
    public static IList<DelegationEvent> FindAllByTaskId(Int32 taskId)
    {
        if (taskId <= 0) return [];

        return FindAll(_.TaskId == taskId, _.Seq.Asc(), null, 0, 0);
    }

    /// <summary>取某任务从指定序号之后的事件（SSE 断线续读，G1）</summary>
    /// <param name="taskId">任务主键</param>
    /// <param name="fromSeq">起始序号（不含）</param>
    /// <returns>实体列表</returns>
    public static IList<DelegationEvent> FindAfterSeq(Int32 taskId, Int32 fromSeq)
    {
        if (taskId <= 0) return [];

        return FindAll(_.TaskId == taskId & _.Seq > fromSeq, _.Seq.Asc(), null, 0, 0);
    }

    /// <summary>取某任务当前最大序号（追加事件时用于分配新序号）</summary>
    /// <param name="taskId">任务主键</param>
    /// <returns>最大序号；无事件时返回 0</returns>
    public static Int32 GetMaxSeq(Int32 taskId)
    {
        if (taskId <= 0) return 0;

        var list = FindAll(_.TaskId == taskId, _.Seq.Desc(), null, 0, 1);
        return list.Count > 0 ? list[0].Seq : 0;
    }
    #endregion
}
