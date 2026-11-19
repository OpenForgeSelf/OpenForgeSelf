using System;
using System.Collections.Generic;
using NewLife;
using XCode;

namespace ForgeSelf.Api.Plugins.AgentHub.Entities;

/// <summary>
/// AgentRuntimeRecord 业务扩展（人工维护，不被生成器覆盖）。
/// </summary>
public partial class AgentRuntimeRecord : Entity<AgentRuntimeRecord>
{
    #region 对象操作
    static AgentRuntimeRecord()
    {
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <inheritdoc />
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (SessionKey.IsNullOrEmpty()) throw new ArgumentNullException(nameof(SessionKey), "SessionKey cannot be empty!");
        if (Type.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Type), "Type cannot be empty!");
        if (!base.Valid(method)) return false;
        if (method == DataMethod.Insert && Timestamp == DateTime.MinValue) Timestamp = DateTime.Now;
        return true;
    }
    #endregion

    #region 扩展查询
    /// <summary>取某会话 AgentHubSequence 之后的记录（升序）。</summary>
    public static IList<AgentRuntimeRecord> FindAfterSequence(String sessionKey, Int64 afterAgentHubSequence)
    {
        if (sessionKey.IsNullOrEmpty()) return [];
        return FindAll(
            _.SessionKey == sessionKey & _.AgentHubSequence > afterAgentHubSequence,
            _.AgentHubSequence.Asc(),
            null, 0, 0);
    }

    /// <summary>全局最大 AgentHubSequence（无记录返回 0）。</summary>
    public static Int64 GetMaxAgentHubSequence()
    {
        var list = FindAll(null, _.AgentHubSequence.Desc(), null, 0, 1);
        return list.Count > 0 ? list[0].AgentHubSequence : 0;
    }

    /// <summary>某会话最大 OarSequence。</summary>
    public static Int64 GetMaxOarSequence(String sessionKey)
    {
        if (sessionKey.IsNullOrEmpty()) return 0;
        var list = FindAll(_.SessionKey == sessionKey, _.OarSequence.Desc(), null, 0, 1);
        return list.Count > 0 ? list[0].OarSequence : 0;
    }
    #endregion
}
