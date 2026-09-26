using System.Collections.Concurrent;
using System.Text.Json;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>一次权限申请的运行时状态（内存态，落库走 DelegationEvent）。</summary>
public class PendingPermission
{
    /// <summary>任务主键</summary>
    public Int32 TaskId { get; set; }

    /// <summary>申请 id（外部 agent 给的 requestId，或本地生成）</summary>
    public String RequestId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>权限类别（write_file|exec_command|read_file|network|…）</summary>
    public String Kind { get; set; } = "unknown";

    /// <summary>申请内容摘要（文件路径 / 命令）</summary>
    public String? Detail { get; set; }

    /// <summary>申请时间</summary>
    public DateTime RequestTime { get; set; } = DateTime.Now;

    /// <summary>等待答复的任务源（Transport 侧 await 这个）</summary>
    public TaskCompletionSource<PermissionVerdict> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>权限裁决结果。</summary>
public class PermissionVerdict
{
    /// <summary>是否允许</summary>
    public Boolean Allowed { get; set; }

    /// <summary>裁决来源（trusted = 已授信自动放行；human = 人工；timeout = 超时按拒绝）</summary>
    public String Source { get; set; } = "human";

    /// <summary>是否仅本次（不记忆）——A+C 模型中人工放行恒为 true</summary>
    public Boolean OnceOnly { get; set; } = true;

    /// <summary>说明（落事件留痕用）</summary>
    public String? Reason { get; set; }
}

/// <summary>
/// 权限中枢（design §6.1，A+C 模型）。
///
/// 铁律：
/// - 默认 **read-only**，每次审批不记忆（OnceOnly 恒真）；
/// - 只有对单个 agent **显式授信（带范围）** 后才自动放行；
/// - 放行与拒绝**都**落 PermissionRequest 事件 —— 审计不能只记异常；
/// - 审批超时（默认 120s）按**拒绝**处理，绝不默认放行。
/// </summary>
public class PermissionBroker
{
    /// <summary>待审批队列（taskId → requestId → 申请）</summary>
    private readonly ConcurrentDictionary<Int32, ConcurrentDictionary<String, PendingPermission>> _pending = new();

    private readonly IAgentRegistry _registry;

    public PermissionBroker(IAgentRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>当前待审批数量（供 UI 徽标 / stats）</summary>
    public Int32 PendingCount => _pending.Values.Sum(m => m.Count);

    /// <summary>
    /// 请求权限裁决：已授信 → 立即放行；否则挂起等人工（含超时按拒绝）。
    /// </summary>
    /// <param name="taskId">任务主键</param>
    /// <param name="agentId">agent 主键</param>
    /// <param name="kind">权限类别</param>
    /// <param name="detail">申请详情</param>
    /// <param name="timeoutSeconds">审批超时（秒）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>裁决结果</returns>
    public async Task<PermissionVerdict> RequestAsync(
        Int32 taskId, Int32 agentId, String kind, String? detail,
        Int32 timeoutSeconds = 120, CancellationToken ct = default)
    {
        // 1) 授信范围内 → 自动放行（落来源留痕）
        var agent = _registry.Get(agentId);
        if (agent != null && agent.Trusted && IsInScope(agent.TrustedScopes, kind))
        {
            XTrace.Log.Info("[AgentHub] 任务 {0} 权限 {1} 命中授信范围，自动放行", taskId, kind);

            return new PermissionVerdict
            {
                Allowed = true,
                Source = "trusted",
                OnceOnly = false,
                Reason = $"命中 agent「{agent.Name}」授信范围"
            };
        }

        // 2) 未授信 → 挂起等人工（默认路径）
        var pending = new PendingPermission
        {
            TaskId = taskId,
            Kind = kind,
            Detail = detail
        };

        var bucket = _pending.GetOrAdd(taskId, _ => new ConcurrentDictionary<String, PendingPermission>());
        bucket[pending.RequestId] = pending;

        XTrace.Log.Info("[AgentHub] 任务 {0} 发起权限申请 {1}（{2}），等待人工审批（{3}s）",
            taskId, pending.RequestId, kind, timeoutSeconds);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));

            try
            {
                return await pending.Completion.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // 超时 → 按拒绝（安全侧），并留痕
                XTrace.Log.Warn("[AgentHub] 任务 {0} 权限申请 {1} 审批超时，按拒绝处理", taskId, pending.RequestId);

                return new PermissionVerdict
                {
                    Allowed = false,
                    Source = "timeout",
                    OnceOnly = true,
                    Reason = $"审批超时（{timeoutSeconds}s），按拒绝处理"
                };
            }
        }
        finally
        {
            bucket.TryRemove(pending.RequestId, out _);
            if (bucket.IsEmpty) _pending.TryRemove(taskId, out _);
        }
    }

    /// <summary>
    /// 人工答复一个待审批申请（审批台调用）。
    /// </summary>
    /// <param name="taskId">任务主键</param>
    /// <param name="requestId">申请 id</param>
    /// <param name="allowed">是否允许</param>
    /// <param name="note">备注</param>
    /// <returns>是否命中了一个待审批项</returns>
    public Boolean Resolve(Int32 taskId, String requestId, Boolean allowed, String? note = null)
    {
        if (!_pending.TryGetValue(taskId, out var bucket)) return false;
        if (!bucket.TryGetValue(requestId, out var pending)) return false;

        pending.Completion.TrySetResult(new PermissionVerdict
        {
            Allowed = allowed,
            Source = "human",
            OnceOnly = true,          // A+C：人工放行只对本次有效，不记忆
            Reason = note
        });

        XTrace.Log.Info("[AgentHub] 任务 {0} 权限申请 {1} 人工裁决：{2}", taskId, requestId, allowed ? "允许" : "拒绝");

        return true;
    }

    /// <summary>取某任务的待审批列表（审批台轮询/SSE 用）。</summary>
    /// <param name="taskId">任务主键</param>
    /// <returns>待审批列表</returns>
    public IReadOnlyList<PendingPermission> ListPending(Int32 taskId)
        => _pending.TryGetValue(taskId, out var bucket) ? bucket.Values.ToList() : [];

    /// <summary>取消某任务的全部待审批（任务被取消/超时时调用，避免挂死）。</summary>
    /// <param name="taskId">任务主键</param>
    public void CancelAll(Int32 taskId)
    {
        if (!_pending.TryRemove(taskId, out var bucket)) return;

        foreach (var pending in bucket.Values)
        {
            pending.Completion.TrySetResult(new PermissionVerdict
            {
                Allowed = false,
                Source = "cancelled",
                OnceOnly = true,
                Reason = "任务已取消/结束，权限申请作废"
            });
        }
    }

    /// <summary>把裁决转成可落库的事件载荷 JSON。</summary>
    /// <param name="kind">权限类别</param>
    /// <param name="detail">申请详情</param>
    /// <param name="verdict">裁决</param>
    /// <returns>JSON</returns>
    public static String ToEventPayload(String kind, String? detail, PermissionVerdict verdict) =>
        JsonSerializer.Serialize(new
        {
            kind,
            detail,
            allowed = verdict.Allowed,
            source = verdict.Source,
            onceOnly = verdict.OnceOnly,
            reason = verdict.Reason
        });

    /// <summary>授信范围匹配：范围含该类别，或含通配 <c>*</c>。</summary>
    /// <param name="scopes">授信范围</param>
    /// <param name="kind">权限类别</param>
    /// <returns>是否命中</returns>
    private static Boolean IsInScope(IReadOnlyList<String> scopes, String kind)
    {
        if (scopes == null || scopes.Count == 0 || kind.IsNullOrEmpty()) return false;

        foreach (var scope in scopes)
        {
            if (scope.IsNullOrEmpty()) continue;
            if (scope.Trim() == "*") return true;
            if (String.Equals(scope.Trim(), kind.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
