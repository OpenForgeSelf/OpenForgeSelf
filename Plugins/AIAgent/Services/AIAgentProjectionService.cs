using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Core;
using NewLife.Log;
using AIChatMessageEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AIChatMessage;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 会话事件日志 → AIAgent 插件 <c>AIChatMessage</c> 只读视图的投影同步器（B4/040）。
/// </summary>
/// <remarks>
/// <para>
/// 与宿主 <c>SessionProjectionService</c> 同一设计（幂等全量重投影 + 调用方返回前 await），
/// 差异仅在目标表：本插件投影到自有 <c>AIChatMessage</c>（AIAgent 连接，独立库），并额外重建
/// 助手行的 <c>ToolCallsJson</c> 工具轨迹（从日志的 tool/call + tool/result 事件配对还原，含耗时与成败）。
/// </para>
/// <para>
/// 投影行只含 user/assistant 两类（与改序前 UI 历史形态一致）；system / tool 角色不进本视图，
/// 但都已在日志里、且参与 <c>DeriveMessages</c> 的模型输入派生。
/// </para>
/// <para>
/// 幂等性：与日志派生行做<b>前缀对齐</b>（Role + Content + ToolCallsJson 逐条比对），
/// 首个差异处截断重写、尾部补齐 —— 同一事件集重跑 N 次行集合不变。
/// </para>
/// </remarks>
public sealed class AIAgentProjectionService
{
    private static readonly JsonSerializerOptions TraceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IContext _ctx;
    private ISessionStore? _store;

    /// <summary>
    /// 宿主会话事件日志：经 Cordis 上下文<b>运行期</b>解析（宿主在 ProvideHostServices 阶段才 seed 进根上下文，
    /// 且插件子容器不含宿主契约 —— 构造注入 ISessionStore 会导致控制器激活 500，e2e 实测）。
    /// </summary>
    private ISessionStore Store => _store ??= _ctx.Get<ISessionStore>()
        ?? throw new InvalidOperationException("宿主未提供 ISessionStore 契约，无法同步会话投影");

    public AIAgentProjectionService(IContext ctx)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }

    /// <summary>
    /// 从日志幂等重投影到 <c>AIChatMessage</c> 只读视图。调用方必须在响应返回前 await。
    /// </summary>
    /// <param name="sessionId">会话 ID。</param>
    /// <param name="ct">取消令牌（XCode 写入为同步 API，取消在调用方入口生效）。</param>
    public Task SyncAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ct.ThrowIfCancellationRequested();

        var rows = BuildProjectionRows(Store.Replay(sessionId));

        var existing = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == sessionId)
            .OrderBy(m => m.Id)
            .ToList();

        // 前缀对齐：首个差异下标
        var i = 0;
        while (i < rows.Count && i < existing.Count
               && string.Equals(existing[i].Role, rows[i].Role, StringComparison.OrdinalIgnoreCase)
               && string.Equals(existing[i].Content, rows[i].Content, StringComparison.Ordinal)
               && string.Equals(existing[i].ToolCallsJson, rows[i].ToolCallsJson ?? string.Empty, StringComparison.Ordinal))
        {
            i++;
        }

        for (var j = existing.Count - 1; j >= i; j--)
        {
            existing[j].Delete();
        }

        for (var k = i; k < rows.Count; k++)
        {
            var now = DateTime.Now;
            new AIChatMessageEntity
            {
                SessionId = sessionId,
                Role = rows[k].Role,
                Content = rows[k].Content,
                ToolCallsJson = rows[k].ToolCallsJson ?? string.Empty,
                CreateTime = now,
                UpdateTime = now,
            }.Insert();
        }

        XTrace.Log.Debug("[AIAgentPlugin] 会话投影同步完成，SessionId: {0}, 日志派生 {1} 条，保留前缀 {2} 条",
            sessionId, rows.Count, i);

        // XCode 写入为同步 API；异步签名仅为调用方 await 语义（杜绝 async void）
        return Task.CompletedTask;
    }

    /// <summary>投影行（角色 + 内容 + 工具轨迹）。</summary>
    private sealed record ProjectionRow(string Role, string Content, string? ToolCallsJson);

    /// <summary>
    /// 从日志重建本会话最后一条助手行的工具轨迹 JSON（供响应体即时返回）。
    /// 持久化值由 <see cref="SyncAsync"/> 从同一份日志重建落库 —— 单一真源，无两套轨迹逻辑。
    /// </summary>
    /// <param name="sessionId">会话 ID。</param>
    /// <returns>工具轨迹 JSON；本轮无工具调用时为 null。</returns>
    public string? BuildLastToolCallsJson(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        return BuildProjectionRows(Store.Replay(sessionId))
            .LastOrDefault(r => r.Role == "assistant")?.ToolCallsJson;
    }

    /// <summary>
    /// 从日志事件重建投影行：user/assistant 直映；assistant 行携带「此前累计、尚未消费」的工具轨迹；
    /// tool/call + tool/result 按 CallId 配对还原成败与耗时。
    /// </summary>
    private static List<ProjectionRow> BuildProjectionRows(IReadOnlyList<SessionEvent> events)
    {
        var rows = new List<ProjectionRow>();
        var pending = new List<ChatToolCallTrace>();
        var openCalls = new Dictionary<string, ChatToolCallTrace>(StringComparer.Ordinal);
        // 乱序兜底：result 先到、call 后到时，先建「孤儿结果轨迹」挂在这里，等 call 到达再回填 Args 并转入 openCalls
        var lateResults = new Dictionary<string, ChatToolCallTrace>(StringComparer.Ordinal);

        foreach (var evt in events)
        {
            switch (evt)
            {
                case UserMessageEvent u:
                    rows.Add(new ProjectionRow("user", u.Content, null));
                    break;

                case ToolCallEvent call:
                {
                    // 同一 CallId 只记一次（流式增量不会出现在日志里，防御性去重）
                    if (openCalls.ContainsKey(call.CallId)) break;

                    // 乱序：result 先到过 → 复用那条轨迹，补上 Args（不再产生「一条调用两条轨迹」）
                    if (lateResults.TryGetValue(call.CallId, out var late))
                    {
                        late.Name = call.ToolName;
                        late.Args = call.ArgsJson;
                        openCalls[call.CallId] = late;
                        lateResults.Remove(call.CallId);
                        break;
                    }

                    var trace = new ChatToolCallTrace { Name = call.ToolName, Args = call.ArgsJson };
                    openCalls[call.CallId] = trace;
                    pending.Add(trace);
                    break;
                }

                case ToolResultEvent result:
                {
                    if (openCalls.TryGetValue(result.CallId, out var trace))
                    {
                        trace.Result = result.ResultJson;
                        trace.Success = result.Outcome == ToolOutcome.Ok;
                        trace.DurationMs = result.DurationMs;
                        openCalls.Remove(result.CallId);
                        break;
                    }

                    if (lateResults.ContainsKey(result.CallId)) break; // 重复的孤立结果，已留痕过

                    // B5（041）收口：孤立/乱序 result 过去是静默丢弃（轨迹凭空消失，复盘时最难查的一类）。
                    // 现在显式告警并保留成一条轨迹（Success 按 outcome 判定），不再吞掉；
                    // 若其后同名 CallId 的 call 到达（乱序场景），会在上面回填 Args 合成一条完整轨迹。
                    XTrace.Log.Warn(
                        "[AIAgentPlugin] 孤立工具结果（无配对 tool/call），已保留为独立轨迹: SessionId={0}, CallId={1}, Tool={2}, Outcome={3}",
                        result.SessionId, result.CallId, result.ToolName, result.Outcome);
                    var orphan = new ChatToolCallTrace
                    {
                        Name = result.ToolName,
                        Args = string.Empty,
                        Result = result.ResultJson,
                        Success = result.Outcome == ToolOutcome.Ok,
                        DurationMs = result.DurationMs
                    };
                    lateResults[result.CallId] = orphan;
                    pending.Add(orphan);
                    break;
                }

                case AssistantMessageEvent assistant:
                    // 助手行消费「此前累计」的轨迹（一轮 POST 内工具调用发生在最终回复之前）；
                    // 落行前先把仍未闭合的 call 标失败（取消/异常中断导致没有配对 result），
                    // 保证本行序列化的轨迹里不会出现「Success=true 但无结果」的假成功。
                    MarkUnclosedFailed(pending);
                    rows.Add(new ProjectionRow("assistant", assistant.Content, SerializeTrace(pending)));
                    pending.Clear();
                    break;

                default:
                    // system / tool / 结构 / 请求信封类：不进本只读视图（但已落日志、参与模型输入派生）
                    break;
            }
        }

        // 收尾：日志末尾仍未闭合的 call（回合未以助手行结束，如被取消）同样标失败
        MarkUnclosedFailed(pending);

        return rows;
    }

    /// <summary>
    /// B5（041）收口：把「已发起但无配对 tool/result」的轨迹显式标失败并告警。
    /// 过去它们保持 <c>Success=true</c> 默认值（未执行却显示成功，排查时被误导）。
    /// </summary>
    private static void MarkUnclosedFailed(List<ChatToolCallTrace> pending)
    {
        foreach (var unclosed in pending.Where(t => t.Result == null))
        {
            XTrace.Log.Warn("[AIAgentPlugin] 未闭合工具调用（无 tool/result 配对），轨迹标记失败: Tool={0}, Args={1}",
                unclosed.Name, unclosed.Args);
            unclosed.Result = "{\"success\":false,\"error\":\"未闭合：无配对结果\"}";
            unclosed.Success = false;
        }
    }

    /// <summary>序列化工具轨迹为 camelCase JSON；无轨迹时返回 null（不写列）。</summary>
    private static string? SerializeTrace(List<ChatToolCallTrace> traces)
    {
        if (traces.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(traces, TraceJsonOptions);
    }
}
