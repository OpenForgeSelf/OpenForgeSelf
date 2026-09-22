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
/// Agent 中枢实体业务扩展（XCode 分部类规范：泛型基类与业务逻辑放 Biz 侧）。
///
/// ⚠ 本文件由人工维护，XCode 生成器刷新 <c>AgentDefinition.cs</c> 时**不会**覆盖本文件。
/// 自定义查询一律写在此处，不要写进生成文件（会被下一次 xcode 覆写掉）。
/// </summary>
public partial class AgentDefinition : Entity<AgentDefinition>
{
    #region 对象操作
    /// <summary>验证并修补数据（插入/更新时补时间戳）。</summary>
    /// <param name="method">添删改方法</param>
    /// <returns>验证结果</returns>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;

        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "显示名不能为空！");
        if (Vendor.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Vendor), "厂商标识不能为空！");

        if (!base.Valid(method)) return false;

        if (method == DataMethod.Insert)
        {
            if (CreateTime == DateTime.MinValue) CreateTime = DateTime.Now;
            if (UpdateTime == DateTime.MinValue) UpdateTime = DateTime.Now;
        }
        else if (method == DataMethod.Update)
        {
            UpdateTime = DateTime.Now;
        }

        return true;
    }
    #endregion

    #region 扩展查询（正确性关键路径：一律直查数据库，禁走实体缓存）
    /// <summary>
    /// 按厂商查找（**直查数据库，不走实体缓存**）。
    ///
    /// ⚠ 刻意不用 <c>Meta.Cache</c>：本方法服务于业务唯一约束校验
    /// （「同 vendor 只允许登记一次」，见 <c>AgentRegistry.Create</c>），
    /// 而 <c>Meta.Session</c> 是 <see cref="System.Threading.AsyncLocal{T}"/>，
    /// <c>Meta.Cache</c> 因此是**每执行上下文一份**的进程内缓存，
    /// 与「库」并非同一真源。实测（2026-09-21）出现过这样的组合症状：
    /// <c>FindAll()</c> 直查库拿到 0 行，本方法却从缓存读出上一批次留下的幽灵行 →
    /// 抛「厂商标识 xxx 已被占用」，而库里根本没有该行。
    /// 唯一性判断属于**正确性关键路径**，一旦读到过期快照就是假拒绝
    /// （更糟的是在多实例/切库场景下，缓存快照可能来自**另一个物理库**）。
    /// 故此处一律直查数据库：代价是一次索引查询（Vendor 上有 IX_AgentDefinition_Vendor），
    /// 换来的是"校验结果与被校验的库严格同源"。
    /// </summary>
    /// <param name="vendor">厂商标识</param>
    /// <returns>实体列表</returns>
    public static IList<AgentDefinition> FindAllByVendor(String vendor)
    {
        if (vendor.IsNullOrEmpty()) return [];

        return FindAll(_.Vendor == vendor);
    }

    /// <summary>
    /// 按厂商查一条（**直查数据库，不走实体缓存**，理由同 <see cref="FindAllByVendor"/>）。
    /// 供「同 vendor 唯一」这类正确性校验使用。
    /// </summary>
    /// <param name="vendor">厂商标识</param>
    /// <returns>实体对象，不存在返回 null</returns>
    public static AgentDefinition? FindFirstByVendor(String vendor)
    {
        if (vendor.IsNullOrEmpty()) return null;

        return Find(_.Vendor == vendor);
    }

    /// <summary>取全部启用的 agent（按优先级倒序）</summary>
    /// <returns>实体列表</returns>
    public static IList<AgentDefinition> FindAllEnabled()
    {
        return FindAll(_.Enabled == true, _.Priority.Desc(), null, 0, 0);
    }
    #endregion
}
