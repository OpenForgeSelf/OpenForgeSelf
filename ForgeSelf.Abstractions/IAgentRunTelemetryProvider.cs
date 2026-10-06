using System.Threading;
using System.Threading.Tasks;

namespace ForgeSelf.Abstractions;

/// <summary>
/// Agent 运行 trace 的只读提供者（由**持有 AgentRun 数据的那一侧**实现）。
/// </summary>
/// <remarks>
/// 2026-10-06 原子任务 **A3b** 实测发现：`AgentRun` / `AgentStepRun` 是 **AIAgent 插件自有库**的表
/// （宿主 `ForgeSelf.Api/Entities/` 下没有这两个实体），而插件程序集不引用宿主、宿主也不引用插件，
/// ⇒ 宿主侧 `ITurnTelemetryQuery` 实现**编译期拿不到** AgentRun。
/// 故把这一段从契约里**外置**为本接口：由 AIAgent 插件实现并注册；宿主实现只做「有就转调、没有就返回 null」。
/// 未注册时 `ITurnTelemetryQuery.GetAgentRunTelemetryAsync` 按契约返回 <see langword="null"/>
/// ——契约原文即「无对应运行返回 null」，**不伪造空对象**。
/// </remarks>
public interface IAgentRunTelemetryProvider
{
    /// <summary>取一次 Agent 运行的 trace 只读投影（AgentRun + 其步骤）。无对应运行返回 null。</summary>
    Task<AgentRunTelemetry?> GetAgentRunTelemetryAsync(string agentRunId, CancellationToken ct = default);
}
