using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.CostScope.Services;

/// <summary>瀑布节点类型。</summary>
public enum TraceNodeKind
{
    /// <summary>LLM 调用节点（来自宿主 <c>ChatTurn</c>）。</summary>
    Llm,

    /// <summary>工具/步骤节点（来自 AgentRun 的步骤）。</summary>
    Tool,
}

/// <summary>trace 瀑布的一个节点。</summary>
/// <param name="Kind">节点类型。</param>
/// <param name="Label">标题：LLM 节点用模型名，工具节点用步骤名（缺失时回退为类型名）。</param>
/// <param name="Start">开始时间。</param>
/// <param name="DurationMs">耗时毫秒，<b>保证非负</b>（AC4）。</param>
/// <param name="Model">LLM 节点的模型名；工具节点为 <see langword="null"/>。</param>
/// <param name="Tokens">LLM 节点的 token 合计；工具节点为 <see langword="null"/>。</param>
/// <param name="AgentRunId">所属运行 ID；未能关联时为 <see langword="null"/>（BC-4：显式不挂靠）。</param>
public readonly record struct TraceNode(
    TraceNodeKind Kind,
    string Label,
    DateTime Start,
    long DurationMs,
    string? Model,
    long? Tokens,
    string? AgentRunId);

/// <summary>一次运行的 trace 瀑布结果。</summary>
/// <param name="AgentRunId">关联到的运行 ID；无关联时为 <see langword="null"/>。</param>
/// <param name="Nodes">按开始时间升序的节点序列。</param>
/// <param name="LinkedTurns">成功关联到该运行的 LLM 轮次数。</param>
/// <param name="UnlinkedTurns">未能关联的轮次数（BC-4）。</param>
/// <param name="IsApproximate">
/// 关联是否<b>近似</b>。宿主 <c>ChatTurn</c> 无 <c>AgentRunId</c> 外键（A9 已取消加列），
/// 故关联靠「同一会话键 + 时间窗邻近」推断——这是<b>推断而非事实</b>，必须显式告知下游与页面。
/// </param>
/// <param name="CorrelationNote">关联口径说明（供页面直接展示，避免把近似说成精确）。</param>
public readonly record struct TraceWaterfall(
    string? AgentRunId,
    IReadOnlyList<TraceNode> Nodes,
    int LinkedTurns,
    int UnlinkedTurns,
    bool IsApproximate,
    string CorrelationNote);

/// <summary>
/// trace 关联与 waterfall 投影（FR-4.5 / AC4 / BC-4）。
/// </summary>
/// <remarks>
/// <para><b>架构口径（A9 裁决后）</b>：宿主<b>不加外键列</b>、只负责如实产出轮次数据；
/// 本服务在<b>插件侧</b>把「宿主轮次」与「AgentRun 及其步骤」关联起来——
/// 宿主保持抽象，消费与加工全在插件（与 dsh cordis 的分工一致）。</para>
/// <para><b>关联规则</b>（逐条可单测）：
/// ① 会话键相同（两侧都有会话键时）；② 轮次时间落在运行时间窗 ± 容差内；
/// ③ 运行缺时间信息时<b>降级为仅按会话键</b>并标记近似；④ 任何无法关联的轮次<b>如实计入未关联</b>，
/// 绝不静默挂到某个运行（BC-4）。</para>
/// <para><b>不伪造</b>：耗时负值一律截断为 0 并在节点上反映；关联不上就不给节点。</para>
/// </remarks>
public sealed class TraceProjectionService
{
    /// <summary>未关联到任何运行时的固定说明（BC-4）。</summary>
    public const string UnlinkedNote = "该轮次未关联到任何 AgentRun（主聊天链路或非 Agent 路径）";

    /// <summary>关联口径说明（<b>近似</b>，不得当精确用）。</summary>
    public const string ApproximateNote = "关联为近似：宿主轮次无 AgentRunId 外键，按「同一会话键 + 时间窗邻近」推断";

    /// <summary>默认时间容差。</summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromSeconds(30);

    /// <summary>构造指定运行的瀑布；<paramref name="agentRunId"/> 为空时自动选关联轮次最多的运行。</summary>
    public TraceWaterfall Build(
        string? agentRunId,
        IReadOnlyList<TurnTelemetryRecord> turns,
        IReadOnlyList<AgentRunTelemetry> runs,
        TimeSpan? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentNullException.ThrowIfNull(runs);

        var tol = tolerance ?? DefaultTolerance;
        var run = Pick(agentRunId, turns, runs, tol);

        if (run is null)
        {
            // BC-4：明确「无关联 AgentRun」，不静默挂到某个 run；节点只给未关联的 LLM 轮次本身。
            return new TraceWaterfall(
                null,
                turns.Select(t => LlmNode(t, null)).ToList(),
                0,
                turns.Count,
                true,
                UnlinkedNote);
        }

        var linked = turns.Where(t => IsLinked(t, run, tol)).ToList();
        var unlinkedCount = turns.Count - linked.Count;
        var approximate = linked.Count > 0 && !HasTimeAnchor(run);

        var nodes = new List<TraceNode>(linked.Count + run.Steps.Count);
        nodes.AddRange(run.Steps.Select(s => ToolNode(s, run.AgentRunId)));
        nodes.AddRange(linked.Select(t => LlmNode(t, run.AgentRunId)));

        var ordered = nodes
            .OrderBy(n => n.Start)
            .ThenBy(n => n.Kind)
            .ThenBy(n => n.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TraceWaterfall(
            run.AgentRunId, ordered, linked.Count, unlinkedCount,
            approximate,
            approximate ? ApproximateNote : $"{ApproximateNote}（本次运行有时间锚点，匹配置信度较高）");
    }

    /// <summary>只取节点序列（端点层常用形状）。</summary>
    public IReadOnlyList<TraceNode> Nodes(
        string? agentRunId,
        IReadOnlyList<TurnTelemetryRecord> turns,
        IReadOnlyList<AgentRunTelemetry> runs,
        TimeSpan? tolerance = null) =>
        Build(agentRunId, turns, runs, tolerance).Nodes;

    /// <summary>某轮次是否关联到指定运行。</summary>
    public static bool IsLinked(TurnTelemetryRecord turn, AgentRunTelemetry run, TimeSpan? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(run);

        // ① 会话键：两侧都有值时必须相同
        if (!string.IsNullOrWhiteSpace(run.SessionKey) && !string.IsNullOrWhiteSpace(turn.SessionKey)
            && !string.Equals(run.SessionKey, turn.SessionKey, StringComparison.Ordinal))
            return false;

        // ② 时间窗：运行有时间锚点且轮次有时间时才比对
        if (run.StartedTime is null || turn.CreatedTime == default) return true;

        var tol = tolerance ?? DefaultTolerance;
        var start = run.StartedTime.Value - tol;
        var end = (run.EndedTime ?? DateTime.MaxValue) + tol;

        return turn.CreatedTime >= start && turn.CreatedTime <= end;
    }

    /// <summary>运行是否具备时间锚点（决定关联是「时间窗推断」还是纯会话键推断）。</summary>
    public static bool HasTimeAnchor(AgentRunTelemetry run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return run.StartedTime is not null;
    }

    private static AgentRunTelemetry? Pick(
        string? agentRunId,
        IReadOnlyList<TurnTelemetryRecord> turns,
        IReadOnlyList<AgentRunTelemetry> runs,
        TimeSpan tol)
    {
        if (!string.IsNullOrWhiteSpace(agentRunId))
            return runs.FirstOrDefault(r => string.Equals(r.AgentRunId, agentRunId, StringComparison.Ordinal));

        // 未指定 run ⇒ 选「关联轮次最多」的那个；并列时取时间最早（结果稳定、可重复）
        return runs
            .Select(r => (Run: r, Linked: turns.Count(t => IsLinked(t, r, tol))))
            .Where(x => x.Linked > 0)
            .OrderByDescending(x => x.Linked)
            .ThenBy(x => x.Run.StartedTime ?? DateTime.MaxValue)
            .ThenBy(x => x.Run.AgentRunId, StringComparer.Ordinal)
            .Select(x => x.Run)
            .FirstOrDefault();
    }

    private static TraceNode LlmNode(TurnTelemetryRecord t, string? runId) => new(
        TraceNodeKind.Llm,
        string.IsNullOrWhiteSpace(t.Model) ? "(未记录模型)" : t.Model,
        t.CreatedTime,
        Math.Max(0, t.DurationMs),
        string.IsNullOrWhiteSpace(t.Model) ? null : t.Model,
        // token 为真值来源（BR-3）；无 usage 时保持 null，不伪造 0（FR-1.4）
        t.PromptTokens + t.CompletionTokens > 0 ? t.PromptTokens + t.CompletionTokens : null,
        runId);

    private static TraceNode ToolNode(AgentStepTelemetry s, string runId) => new(
        TraceNodeKind.Tool,
        string.IsNullOrWhiteSpace(s.Name) ? s.Kind : s.Name,
        s.StartedAt ?? default,
        Math.Max(0, s.DurationMs),
        null,
        s.Tokens > 0 ? s.Tokens : null,
        runId);
}
