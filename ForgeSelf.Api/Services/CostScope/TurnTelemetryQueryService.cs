using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using XCode;

namespace ForgeSelf.Api.Services.CostScope;

/// <summary>
/// <see cref="ITurnTelemetryQuery"/> 的**宿主侧只读**实现（原子任务 A3b）。
/// </summary>
/// <remarks>
/// <b>为什么必须留宿主</b>（编译期事实，非偏好）：`ChatTurn` / `AIModel` 等 XCode 实体在
/// <c>ForgeSelf.Api/Entities/</c>，而实测 19 个插件 csproj 只引用 <c>ForgeSelf.Core</c> + <c>ForgeSelf.Abstractions</c>，
/// 零个引用 <c>ForgeSelf.Api</c> ⇒ 插件代码编译期拿不到宿主实体。
/// 本类只做**取数与投影**，<b>不含任何成本/聚合业务</b>（F0），且<b>只读</b>（BR-5 / F1）：
/// 全类不出现 Insert / Update / Delete / Save / ExecuteNonQuery。
/// </remarks>
public class TurnTelemetryQueryService : ITurnTelemetryQuery
{
    private readonly IAgentRunTelemetryProvider? _agentRunProvider;

    /// <summary>构造；<paramref name="agentRunProvider"/> 可选（未注册时 AgentRun trace 返回 null，不伪造）。</summary>
    public TurnTelemetryQueryService(IAgentRunTelemetryProvider? agentRunProvider = null)
        => _agentRunProvider = agentRunProvider;

    /// <inheritdoc />
    public Task<IReadOnlyList<TurnTelemetryRecord>> GetTurnTelemetryAsync(
        string sessionKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            return Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(Array.Empty<TurnTelemetryRecord>());

        var rows = ChatTurn.FindAll(ChatTurn._.SessionKey == sessionKey);
        var list = rows.OrderBy(t => t.TurnIndex).Select(ToRecord).ToList();

        return Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(list);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TurnTelemetryRecord>> QueryTurnsAsync(
        TurnTelemetryQueryFilter filter, CancellationToken ct = default)
    {
        if (filter is null)
            return Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(Array.Empty<TurnTelemetryRecord>());

        var exp = new WhereExpression();
        var hasCondition = false;

        if (filter.From.HasValue)
        {
            exp &= ChatTurn._.CreatedTime >= filter.From.Value;
            hasCondition = true;
        }

        if (filter.To.HasValue)
        {
            exp &= ChatTurn._.CreatedTime <= filter.To.Value;
            hasCondition = true;
        }

        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            exp &= ChatTurn._.Model == filter.Model;
            hasCondition = true;
        }

        if (!string.IsNullOrWhiteSpace(filter.Style))
        {
            exp &= ChatTurn._.Style == filter.Style;
            hasCondition = true;
        }

        // NFR-4：无条件即全表扫，显式拒绝并返回空集合（不静默扫全表）
        if (!hasCondition)
            return Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(Array.Empty<TurnTelemetryRecord>());

        var rows = ChatTurn.FindAll(exp);
        var list = rows
            .OrderBy(t => t.CreatedTime)
            .ThenBy(t => t.TurnIndex)
            .Select(ToRecord)
            .ToList();

        return Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(list);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ModelIdentityDto>> GetModelCatalogAsync(CancellationToken ct = default)
    {
        // 仅取启用态模型：停用模型不应参与供应商归因
        var rows = AIModel.FindAll(AIModel._.Enabled == true);
        var list = rows
            .Select(m => new ModelIdentityDto
            {
                ChatModelId = m.ChatModelId ?? string.Empty,
                UpstreamModelId = m.UpstreamModelId ?? string.Empty,
                Alias = m.Alias ?? string.Empty,
                ProviderName = m.ProviderName ?? string.Empty,
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ModelIdentityDto>>(list);
    }

    /// <inheritdoc />
    public Task<AgentRunTelemetry?> GetAgentRunTelemetryAsync(
        string agentRunId, CancellationToken ct = default)
    {
        // AgentRun 不在宿主库（属 AIAgent 插件自有库，宿主编译期拿不到实体）
        // ⇒ 委托给持有该数据的插件提供者；未注册时按契约返回 null（不伪造空对象）。
        if (_agentRunProvider is null || string.IsNullOrWhiteSpace(agentRunId))
            return Task.FromResult<AgentRunTelemetry?>(null);

        return _agentRunProvider.GetAgentRunTelemetryAsync(agentRunId, ct);
    }

    /// <summary>ChatTurn → 只读 DTO 投影（不含请求/响应体，NFR-5）。</summary>
    private static TurnTelemetryRecord ToRecord(ChatTurn t) => new()
    {
        Id = t.Id,
        ChatSessionId = t.ChatSessionId,
        TurnIndex = t.TurnIndex,
        SessionKey = t.SessionKey ?? string.Empty,
        Style = t.Style ?? string.Empty,
        Model = t.Model ?? string.Empty,
        PromptTokens = t.PromptTokens,
        CompletionTokens = t.CompletionTokens,
        DurationMs = t.DurationMs,
        // A7 补入：延迟分位 / 错误率 / BR-4 失败判定所需三列（宿主 ChatTurn 均已存在）
        FirstTokenMs = t.FirstTokenMs,
        ResponseStatus = t.ResponseStatus,
        ErrorMessage = string.IsNullOrEmpty(t.ErrorMessage) ? null : t.ErrorMessage,
        CreatedTime = t.CreatedTime,
        // A9（U-3）落地前宿主无此列 ⇒ 恒为 null，不伪造（FR-1.4）
        AgentRunId = null,
    };
}
