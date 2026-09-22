using ForgeSelf.Api.Plugins.AgentHub.Models;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// 外部 agent 交互口的统一抽象（design §4）。
/// CLI 只是实现之一；ACP / HTTP 长驻服务同为一级公民。
/// </summary>
public interface IAgentTransport
{
    /// <summary>transport 类型（Cli / Acp / Http）</summary>
    String Kind { get; }

    /// <summary>
    /// 执行一次委派，产出归一化事件流（流式，边跑边吐）。
    /// </summary>
    /// <param name="ap">交互口配置</param>
    /// <param name="req">运行请求</param>
    /// <param name="session">会话句柄（输出参数：回填 SessionRef / ProcessId）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>归一化事件流</returns>
    IAsyncEnumerable<AgentEvent> StreamAsync(AgentAccessPointDto ap, AgentRunRequest req, AgentSession session, CancellationToken ct);

    /// <summary>取消正在执行的任务（OneShot 只能杀进程树）。</summary>
    /// <param name="session">会话句柄</param>
    /// <returns>异步任务</returns>
    Task CancelAsync(AgentSession session);
}
