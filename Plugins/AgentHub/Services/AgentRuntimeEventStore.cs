using ForgeSelf.Api.Plugins.AgentHub.Entities;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// 双层事件最小耐久路径（架构 §7.2）：底层 <see cref="AgentRuntimeRecord"/> + Session 水位
/// <c>AgentHubSequence</c>/<c>OarSequence</c>/<c>Generation</c>。
/// 上层任务事件仍由 <see cref="DelegationEvent"/> 承担；本存储不替代任务 SSE 词表。
/// </summary>
public sealed class AgentRuntimeEventStore
{
    /// <summary>Payload 硬上限（与 Model.xml Length=4000 对齐）。</summary>
    public const Int32 MaxPayloadChars = 4000;

    private readonly AgentSessionManager _sessions;
    private readonly Object _seqLock = new();

    public AgentRuntimeEventStore(AgentSessionManager sessions) =>
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

    /// <summary>
    /// 追加一条脱敏 Record，分配全局单调 <see cref="AgentRuntimeRecord.AgentHubSequence"/>，
    /// 并回写 Session 水位。
    /// </summary>
    public AgentRuntimeRecord Append(
        String sessionKey,
        String type,
        String? payloadJson,
        Int64? oarSequence = null,
        Int32 taskId = 0,
        Int32? generation = null)
    {
        if (sessionKey.IsNullOrWhiteSpace())
            throw new ArgumentNullException(nameof(sessionKey));

        var (payload, truncated) = TruncatePayload(payloadJson);
        var session = _sessions.Find(sessionKey);
        var gen = generation ?? session?.Generation ?? 1;
        if (gen <= 0) gen = 1;

        Int64 hubSeq;
        Int64 oarSeq;
        lock (_seqLock)
        {
            hubSeq = AgentRuntimeRecord.GetMaxAgentHubSequence() + 1;
            var localMax = sessionKey.IsNullOrEmpty()
                ? 0
                : AgentRuntimeRecord.GetMaxOarSequence(sessionKey);
            oarSeq = oarSequence is > 0 ? oarSequence.Value : localMax + 1;
            if (oarSeq <= localMax) oarSeq = localMax + 1;

            var entity = new AgentRuntimeRecord
            {
                SessionKey = sessionKey,
                TaskId = taskId,
                OarSequence = oarSeq,
                AgentHubSequence = hubSeq,
                Generation = gen,
                Type = type.IsNullOrWhiteSpace() ? "record" : type!,
                PayloadJson = payload,
                Truncated = truncated,
                Timestamp = DateTime.Now,
            };
            entity.Insert();

            if (session != null)
            {
                _sessions.Touch(
                    session,
                    agentHubSequence: hubSeq,
                    oarSequence: oarSeq,
                    generation: gen);
            }

            return entity;
        }
    }

    /// <summary>列出会话在 <paramref name="afterAgentHubSequence"/> 之后的 Record。</summary>
    public IReadOnlyList<AgentRuntimeRecord> List(String sessionKey, Int64 afterAgentHubSequence = 0) =>
        AgentRuntimeRecord.FindAfterSequence(sessionKey, afterAgentHubSequence).ToList();

    private static (String? payload, Boolean truncated) TruncatePayload(String? raw)
    {
        if (raw.IsNullOrEmpty()) return (raw, false);
        // 默认剔除常见 reasoning 键名片段（最小脱敏；完整策略可后续加强）
        var cleaned = raw!
            .Replace("\"reasoning\"", "\"reasoning_omitted\"", StringComparison.OrdinalIgnoreCase);
        if (cleaned.Length <= MaxPayloadChars) return (cleaned, !ReferenceEquals(cleaned, raw) && cleaned != raw);
        XTrace.Log.Debug("[AgentRuntimeEventStore] payload truncated from {0} chars", cleaned.Length);
        return (cleaned[..MaxPayloadChars], true);
    }
}
