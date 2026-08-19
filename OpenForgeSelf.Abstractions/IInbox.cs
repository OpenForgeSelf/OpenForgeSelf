namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 收件箱接缝：三种输入语义（followup / steer / inject）的统一入口。
/// </summary>
public interface IInbox
{
    /// <summary>续聊：用户对当前会话追加一条普通消息。</summary>
    void Followup(string sessionId, string message);

    /// <summary>转向：用户给出即时纠偏/指令（steer），影响后续回合行为。</summary>
    void Steer(string sessionId, string input);

    /// <summary>注入：外部向会话上下文注入一块结构化上下文。</summary>
    void Inject(string sessionId, object contextBlock);
}
