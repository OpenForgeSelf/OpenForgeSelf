namespace ForgeSelf.Abstractions;

/// <summary>
/// 会话事件注册表：把「事件类型名 → record 类型」固化下来，作为运行期强制点。
/// 新增事件类型而不注册，<see cref="ISessionStore.Append"/> 会直接抛异常——
/// 目的不是防错，而是让「漏注册/漏投影」在开发期炸出，不进生产。
/// </summary>
public static class SessionEventMap
{
    private static readonly IReadOnlyDictionary<string, Type> Known = new Dictionary<string, Type>
    {
        ["system/message"] = typeof(SystemMessageEvent),
        ["user/message"] = typeof(UserMessageEvent),
        ["assistant/message"] = typeof(AssistantMessageEvent),
        ["assistant/attempt"] = typeof(AssistantAttemptEvent),
        ["tool/call"] = typeof(ToolCallEvent),
        ["tool/result"] = typeof(ToolResultEvent),
        ["turn/start"] = typeof(TurnStartEvent),
        ["turn/end"] = typeof(TurnEndEvent),
        ["step/start"] = typeof(StepStartEvent),
        ["step/end"] = typeof(StepEndEvent),
        ["request/header"] = typeof(RequestHeaderEvent),
        ["request/context"] = typeof(RequestContextEvent),
        ["agent/inbox/spliced"] = typeof(InboxSplicedEvent),
    };

    /// <summary>按类型名解析 record 类型；未注册则抛 <see cref="UnknownSessionEventException"/>。</summary>
    public static Type Resolve(string type) =>
        Known.TryGetValue(type, out var t) ? t : throw new UnknownSessionEventException(type);

    /// <summary>
    /// 校验事件的「类型名」与「实际 record 类型」双向一致：
    /// 类型名未注册，或类型名与 record 不匹配（如手工伪造 Type），均抛异常。
    /// </summary>
    public static void EnsureKnown(SessionEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (!Known.TryGetValue(evt.Type, out var t) || t != evt.GetType())
        {
            throw new UnknownSessionEventException($"{evt.Type} 未注册或与 record 类型不符");
        }
    }

    /// <summary>已注册的全部事件类型名（供诊断与测试遍历）。</summary>
    public static IReadOnlyCollection<string> KnownTypes => Known.Keys.ToArray();

    /// <summary>判断类型名是否已注册。</summary>
    public static bool IsKnown(string type) => Known.ContainsKey(type);
}

/// <summary>未知会话事件：未注册、或与 record 类型不符。</summary>
public sealed class UnknownSessionEventException : Exception
{
    public UnknownSessionEventException(string message) : base(message) { }
}
