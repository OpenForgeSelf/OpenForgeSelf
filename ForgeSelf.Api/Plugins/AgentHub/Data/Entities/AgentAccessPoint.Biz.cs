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
/// Agent 交互口实体业务扩展（XCode 分部类规范：泛型基类与业务逻辑放 Biz 侧）。
///
/// ⚠ 本文件由人工维护，XCode 生成器刷新 <c>AgentAccessPoint.cs</c> 时**不会**覆盖本文件。
/// 自定义查询一律写在此处，不要写进生成文件（会被下一次 xcode 覆写掉）。
/// </summary>
public partial class AgentAccessPoint : Entity<AgentAccessPoint>
{
    #region 对象操作
    /// <summary>验证并修补数据（插入/更新时补时间戳）。</summary>
    /// <param name="method">添删改方法</param>
    /// <returns>验证结果</returns>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;

        if (AgentId <= 0) throw new ArgumentNullException(nameof(AgentId), "所属 Agent 不能为空！");
        if (Executable.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Executable), "可执行文件不能为空！");

        if (!base.Valid(method)) return false;

        if (method == DataMethod.Insert)
        {
            if (CreateTime == DateTime.MinValue) CreateTime = DateTime.Now;
            if (UpdateTime == DateTime.MinValue) UpdateTime = DateTime.Now;
            if (Health.IsNullOrEmpty()) Health = "Unknown";
        }
        else if (method == DataMethod.Update)
        {
            UpdateTime = DateTime.Now;
        }

        return true;
    }
    #endregion

    #region 扩展查询
    /// <summary>按 Agent 查找全部交互口（默认交互口优先，其次按主键升序）</summary>
    /// <param name="agentId">Agent 主键</param>
    /// <returns>实体列表</returns>
    public static IList<AgentAccessPoint> FindAllByAgentId(Int32 agentId)
    {
        if (agentId <= 0) return [];

        return FindAll(_.AgentId == agentId, _.IsDefault.Desc() + "," + _.Id.Asc(), null, 0, 0);
    }
    #endregion
}
