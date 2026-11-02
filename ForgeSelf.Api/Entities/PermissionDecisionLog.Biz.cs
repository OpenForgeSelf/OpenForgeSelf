using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>权限策略决策日志业务面（B4）：建表 + 只增写入。</summary>
public partial class PermissionDecisionLog : Entity<PermissionDecisionLog>
{
    /// <summary>连接名（与 BindTable 一致）。</summary>
    public const String ConnName = "ForgeSelf";

    /// <summary>确保表就绪（幂等）。</summary>
    public static Boolean EnsureCreated(String connName = ConnName)
    {
        try
        {
            EntityFactory.InitConnection(connName);
            var dal = DAL.Create(connName);
            var version = dal.Db.ServerVersion;
            XTrace.Log.Debug("[PermissionDecisionLog] 数据库初始化完成 (ServerVersion={0})", version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[PermissionDecisionLog] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (method == DataMethod.Insert && !Dirtys[nameof(Time)]) Time = DateTime.Now;
        if (ToolName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ToolName), "工具名不能为空！");
        if (Effect.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Effect), "决策不能为空！");
        return base.Valid(method);
    }

    /// <summary>倒序分页查询（联合审计用）。</summary>
    public static IList<PermissionDecisionLog> SearchOrdered(String? toolName, String? sessionId, DateTime? from, DateTime? to, Int32 pageSize, Int64 maxId)
    {
        var exp = new WhereExpression();
        if (!toolName.IsNullOrEmpty()) exp &= _.ToolName == toolName;
        if (!sessionId.IsNullOrEmpty()) exp &= _.SessionId == sessionId;
        if (from != null) exp &= _.Time >= from.Value;
        if (to != null) exp &= _.Time <= to.Value;
        if (maxId > 0) exp &= _.Id <= maxId;
        return FindAll(exp, null, null, 0, pageSize);
    }
}
