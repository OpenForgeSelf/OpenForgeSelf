using ForgeSelf.Api.Plugins.AgentHub.Entities;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// AgentHubSession 持久化与 SessionKey 映射（P2）。
/// 对外只暴露 SessionKey；OarSessionId 仅存库内。
/// </summary>
public sealed class AgentSessionManager
{
    /// <summary>创建或打开一条会话记录。未传 <paramref name="sessionKey"/> 时生成 GUID。</summary>
    public AgentHubSession Open(
        Int32 agentId,
        Int32 accessPointId,
        String transport,
        String? runtimeId,
        String? oarSessionId,
        String? cwd,
        String? model,
        Int32 generation = 1,
        String? sessionKey = null)
    {
        var entity = new AgentHubSession
        {
            SessionKey = sessionKey.IsNullOrEmpty() ? Guid.NewGuid().ToString("N") : sessionKey!,
            AgentId = agentId,
            AccessPointId = accessPointId,
            Transport = transport.IsNullOrEmpty() ? "Oar" : transport,
            RuntimeId = runtimeId,
            OarSessionId = oarSessionId,
            Cwd = cwd,
            Model = model,
            Status = AgentHubSession.SessionStatus.Open,
            Generation = generation <= 0 ? 1 : generation,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
        };
        entity.Insert();
        return entity;
    }

    /// <summary>按 SessionKey 查找；不存在返回 null。</summary>
    public AgentHubSession? Find(String sessionKey) => AgentHubSession.FindBySessionKey(sessionKey);

    /// <summary>回写 Worker 侧会话 id / 世代 / 最近任务。</summary>
    public void Touch(
        AgentHubSession entity,
        String? oarSessionId = null,
        Int32? generation = null,
        Int32? lastTaskId = null,
        Int64? agentHubSequence = null)
    {
        if (oarSessionId != null) entity.OarSessionId = oarSessionId;
        if (generation is > 0) entity.Generation = generation.Value;
        if (lastTaskId is > 0) entity.LastTaskId = lastTaskId.Value;
        if (agentHubSequence is >= 0) entity.AgentHubSequence = agentHubSequence.Value;
        entity.UpdateTime = DateTime.Now;
        entity.Update();
    }

    /// <summary>标记关闭。</summary>
    public void Close(String sessionKey)
    {
        var entity = Find(sessionKey);
        if (entity == null) return;
        entity.Status = AgentHubSession.SessionStatus.Closed;
        entity.UpdateTime = DateTime.Now;
        entity.Update();
    }

    /// <summary>宿主重启：把遗留 Open 会话标 Interrupted。</summary>
    public Int32 InterruptOpenSessions()
    {
        var open = AgentHubSession.FindAll(
            AgentHubSession._.Status == AgentHubSession.SessionStatus.Open);
        foreach (var s in open)
        {
            s.Status = AgentHubSession.SessionStatus.Interrupted;
            s.UpdateTime = DateTime.Now;
            s.Update();
        }

        if (open.Count > 0)
            XTrace.Log.Warn("[AgentHub] 宿主重启：{0} 个 Open Session 已标 Interrupted", open.Count);
        return open.Count;
    }
}
