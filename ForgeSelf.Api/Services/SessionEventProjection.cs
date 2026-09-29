using ForgeSelf.Abstractions;
using Message = ForgeSelf.Abstractions.Message;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 会话事件 → 模型可见消息的<b>单一投影真源</b>（B2/040）。
/// </summary>
/// <remarks>
/// 内存实现（<see cref="InMemorySessionStore"/>，测试替身）与持久化实现（<see cref="PersistentSessionStore"/>）
/// 都调用 <see cref="Derive"/>，杜绝两处 switch 各自演化导致的投影语义漂移
/// （契约测试对两个实现各跑一遍即自动防漂移）。
/// <para>
/// 投影规则（不变量 1）：只产出 system / user / assistant / tool 四类；
/// attempt、turn/step 结构、请求信封、收件箱拼接等事件<b>已落日志但对模型不可见</b>。
/// 遇到未覆盖的事件类型必须抛 <see cref="UnknownSessionEventException"/> —— 漏投影在开发期炸出，不进生产。
/// </para>
/// </remarks>
public static class SessionEventProjection
{
    /// <summary>spill 阈值默认值：32 KiB（R5，设计稿 §2.7）。</summary>
    public const int DefaultSpillThresholdBytes = 32 * 1024;

    /// <summary>
    /// B9-6（R5）：大工具结果 spill 阈值（字节）。超过该长度的 tool 结果在模型可见投影中
    /// 截断为引用标记（CallId 留痕，完整结果仍走 tools/result 事件与日志）。
    /// 宿主启动时从 ForgeSetting.SpillThresholdBytes 播种（配置中心可调）。
    /// </summary>
    public static int SpillThresholdBytes { get; set; } = DefaultSpillThresholdBytes;

    /// <summary>把事件序列投影为模型可见消息序列（保持事件顺序）。</summary>
    /// <param name="events">按 Id 升序的事件序列。</param>
    /// <returns>模型可见消息；不含结构/轨迹类事件。</returns>
    /// <exception cref="UnknownSessionEventException">出现未覆盖的事件类型（漏投影）。</exception>
    public static IReadOnlyList<Message> Derive(IEnumerable<SessionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var result = new List<Message>();
        foreach (var evt in events)
        {
            switch (evt)
            {
                case SystemMessageEvent s:
                    result.Add(new Message { Role = "system", Content = s.Content });
                    break;

                case UserMessageEvent u:
                    result.Add(new Message { Role = "user", Content = u.Content });
                    break;

                case AssistantMessageEvent a:
                    result.Add(new Message
                    {
                        Role = "assistant",
                        Content = a.Content,
                        ToolCalls = a.ToolCalls,
                        Usage = a.Usage,
                    });
                    break;

                case ToolCallEvent:
                    // 折叠进 assistant.ToolCalls，不单独成条
                    break;

                case ToolResultEvent r:
                    result.Add(new Message { Role = "tool", Content = SpillIfNeeded(r.ResultJson, r.CallId), CallId = r.CallId });
                    break;

                case TurnStartEvent:
                case TurnEndEvent:
                case StepStartEvent:
                case StepEndEvent:
                case RequestHeaderEvent:
                case RequestContextEvent:
                case AssistantAttemptEvent:
                case InboxSplicedEvent:
                    // 结构/轨迹类：对模型不可见，但已落日志
                    // （不变量 1 要求「可见则已记录」，不要求「记录即可见」）
                    break;

                default:
                    // 漏投影在此炸出
                    throw new UnknownSessionEventException(evt.Type);
            }
        }

        return result;
    }

    /// <summary>
    /// B9-6（R5/R4）：大工具结果 spill——超过 <see cref="SpillThresholdBytes"/> 时截断为引用标记。
    /// 完整结果不受影响：tools/result 事件与日志仍保原文（R4：UI 走事件流），进模型历史的只是引用，
    /// 模型可凭 CallId（tool 角色消息天然携带）回查/重调工具。
    /// </summary>
    private static string SpillIfNeeded(string resultJson, string? callId)
    {
        if (resultJson == null || resultJson.Length <= SpillThresholdBytes)
        {
            return resultJson;
        }

        // 预览保留头部至多 1 KiB（内容本身不足 1 KiB 时全量保留），标记里带原始大小与 CallId 留痕
        var preview = resultJson[..Math.Min(1024, resultJson.Length)];
        return $"[tool_result spilled: {resultJson.Length} bytes > {SpillThresholdBytes}，CallId={callId ?? "(无)"}，完整结果见 tools/result 事件]\n{preview}…";
    }
}
