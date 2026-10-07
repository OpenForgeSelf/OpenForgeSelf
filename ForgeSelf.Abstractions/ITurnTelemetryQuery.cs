namespace ForgeSelf.Abstractions;

/// <summary>
/// 遥测只读契约：成本归因与 trace 关联共用的数据访问 seam。
/// 仅声明只读投影，不依赖任何具体存储实现（宿主库 / CostScope 插件库 / AgentRun 插件库）。
/// 消费方据此拿到稳定的「轮次遥测」与「Agent 运行 trace」只读模型，
/// 而无需感知底层是 ChatTurn 表、UsageRecord 表还是 AgentRun 插件表。
/// </summary>
/// <remarks>
/// 本接口是 2026-10-03 LLM 可观测性 pilot 的数据契约种子（原子任务 A1）。
/// 实现方：宿主侧 <c>TurnTelemetryQueryService</c>（A2 之前暂未实现）+ CostScope 插件侧（A2）。
/// 本文件只定义形状，不引入任何运行时行为变更，亦不改写既有实体。
/// </remarks>
public interface ITurnTelemetryQuery
{
    /// <summary>按会话键聚合一轮 ChatTurn 的遥测只读投影。</summary>
    /// <param name="sessionKey">统一会话键（= ChatSession.SessionKey）。</param>
    Task<IReadOnlyList<TurnTelemetryRecord>> GetTurnTelemetryAsync(
        string sessionKey, CancellationToken ct = default);

    /// <summary>按时间窗（可选模型/风格）聚合多个会话的轮次遥测，作为成本归因的底层数据源。</summary>
    Task<IReadOnlyList<TurnTelemetryRecord>> QueryTurnsAsync(
        TurnTelemetryQueryFilter filter, CancellationToken ct = default);

    /// <summary>取一次 Agent 运行的 trace 只读投影（AgentRun + 其步骤）。无对应运行返回 null。</summary>
    Task<AgentRunTelemetry?> GetAgentRunTelemetryAsync(
        string agentRunId, CancellationToken ct = default);

    /// <summary>
    /// 取模型身份目录（模型→供应商 4 级解析的数据源，FR-3.8）。
    /// 聚合宿主 <c>AIModel</c>（ChatModelId / UpstreamModelId / Alias）与 <c>AIProvider</c>（Name）的只读投影；
    /// 无模型配置时返回空集合，**不得返回 null**（消费方按空目录处理，一律判「未归属」）。
    /// </summary>
    /// <remarks>
    /// 2026-10-06 原子任务 **A4 前置**时补入：实读 A1 产物仅有上述三个方法，
    /// 而插件侧 `ModelPriceResolver` 需要模型目录才能做 4 级回退解析（见 03-plan 偏差表 2026-10-05 末行）。
    /// </remarks>
    Task<IReadOnlyList<ModelIdentityDto>> GetModelCatalogAsync(CancellationToken ct = default);
}
