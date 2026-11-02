using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

public partial class ToolApproval : Entity<ToolApproval>
{
    private static Int32 MaxCacheCount = 0;

    /// <summary>连接名（与 BindTable 一致）。</summary>
    public const String ConnName = "ForgeSelf";

    /// <summary>确保表就绪（幂等）。</summary>
    public static Boolean EnsureCreated(String connName = ConnName)
    {
        try
        {
            EntityFactory.InitConnection(connName);
            var dal = DAL.Create(connName);
            // 勿用 discard「_」——与 XCode 字段类 ToolApproval._ 同名冲突（CS0118）
            var version = dal.Db.ServerVersion;
            XTrace.Log.Debug("[ToolApproval] 数据库初始化完成 (ServerVersion={0})", version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ToolApproval] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (SessionId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(SessionId), "会话ID不能为空！");
        if (ToolName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ToolName), "工具名不能为空！");
        if (Status.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Status), "状态不能为空！");
        if (method == DataMethod.Insert && !Dirtys[nameof(CreatedAt)]) CreatedAt = DateTime.Now;
        return base.Valid(method);
    }

    /// <summary>按会话列出审批（Id 升序）。</summary>
    public static IList<ToolApproval> FindAllBySessionIdOrdered(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) return [];
        return FindAll(_.SessionId == sessionId).OrderBy(e => e.Id).ToList();
    }
}
