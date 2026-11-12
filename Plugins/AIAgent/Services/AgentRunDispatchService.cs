using System.Collections.Concurrent;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// IAgentRunDispatch 的 ai-agent 侧实现（PILOT-079 FR-2/FR-9）：
/// 后台 Task 驱动 <see cref="IRunOrchestratorService.RunAsync"/> / <c>ResumeAsync</c>，
/// 模式照抄 <see cref="ScheduledAgentRunTrigger"/>（迭代事件流、捕首个带 runId 的事件、
/// done/run_stuck/error 终局文本、inflight 防重），同进程直调、不走 HTTP、不关闭审批。
/// </summary>
/// <remarks>
/// 并发规则：同一 SessionId 同时只允许 1 个未终态 Run——内存 inflight 字典（本进程）
/// + ListRunsAsync 开放态查询（跨重启兜底，含 Stuck）双层防线。
/// Find：内存快照优先；miss 且键为纯数字 → 按 RunId GetRunAsync 投影（宿主重启恢复路径）。
/// 契约失败一律 Success=false + Error 中文原文，不抛业务异常。
/// </remarks>
public sealed class AgentRunDispatchService : IAgentRunDispatch
{
    private readonly IRunOrchestratorService _orchestrator;
    private readonly IAgentRegistryService? _registry;

    /// <summary>taskKey → 内存派发记录（进程内快照；上限 100，FIFO 淘汰最旧）。</summary>
    private readonly ConcurrentDictionary<string, DispatchRecord> _records = new();
    private readonly ConcurrentQueue<string> _recordOrder = new();

    /// <summary>SessionId → 在跑 RunId（0 = 已受理未捕到 runId）。后台驱动结束时移除。</summary>
    private readonly ConcurrentDictionary<string, long> _inflight = new();

    private const int MaxMemoryRecords = 100;

    public AgentRunDispatchService(IRunOrchestratorService orchestrator, IAgentRegistryService? registry = null)
    {
        _orchestrator = orchestrator;
        _registry = registry;
    }

    /// <inheritdoc />
    public async Task<AgentRunDispatchOutcome> SubmitAsync(AgentRunDispatchRequest request, CancellationToken ct = default)
    {
        if (request == null)
            return AgentRunDispatchOutcome.Fail("派发请求为空");
        if (string.IsNullOrWhiteSpace(request.TaskInput))
            return AgentRunDispatchOutcome.Fail("任务原文（TaskInput）不能为空");
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return AgentRunDispatchOutcome.Fail("会话标识（SessionId）不能为空");

        var (agentId, agentName, resolveError) = ResolveAgent(request.AgentRef);
        if (resolveError != null)
            return AgentRunDispatchOutcome.Fail(resolveError);

        var sessionId = request.SessionId.Trim();

        // 开放态查询在前（跨重启兜底），inflight TryAdd 在后（本进程并发闸）：两道防线任一命中即拒。
        var open = await FindOpenRunAsync(sessionId);
        if (open != null)
            return AgentRunDispatchOutcome.Fail(
                $"该会话已有任务在执行（#{open.Id}，{MapStatus(open.Status)}），不重复提交。");

        if (!_inflight.TryAdd(sessionId, 0))
        {
            _inflight.TryGetValue(sessionId, out var running);
            return AgentRunDispatchOutcome.Fail(running > 0
                ? $"该会话已有任务在执行（#{running}）"
                : "该会话已有任务在执行");
        }

        var taskKey = Guid.NewGuid().ToString("N");
        var record = new DispatchRecord
        {
            TaskKey = taskKey,
            SessionId = sessionId,
            AgentId = agentId,
            AgentName = agentName,
            Status = "pending",
        };
        StoreRecord(record);

        var runRequest = new RunRequest
        {
            SessionId = sessionId,
            AgentId = agentId ?? string.Empty,   // 空 = orchestrator 既有默认链（同聊天「(默认)」语义）
            TaskInput = ComposePrompt(request.TaskInput),
            ChatModelId = request.ChatModelId,   // 空值交给 orchestrator 既有 IChatModelResolver 链明确失败（PC-013 lens）
        };

        // 后台驱动：不 await，不随本调用取消（ct 是受理语义，不是执行生命周期）
        _ = DriveAsync(record, runRequest, resumeRunId: null);

        XTrace.Log.Info("[AgentRunDispatch] 任务已受理 taskKey={0} Session={1} Agent={2} CreatedBy={3}",
            taskKey, sessionId, agentId ?? "(默认)", request.CreatedBy ?? "-");

        return AgentRunDispatchOutcome.Ok(taskKey, agentId, agentName, "pending");
    }

    /// <inheritdoc />
    public async Task<AgentRunDispatchSnapshot?> FindAsync(string taskKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
            return null;

        var record = FindRecord(taskKey);
        if (record != null)
            return record.ToSnapshot();

        // 宿主重启后的恢复路径：内存快照随进程消失，键为纯数字时按 RunId 直查落库事实投影
        if (long.TryParse(taskKey.Trim(), out var runId) && runId > 0)
            return await ProjectFromDbAsync(taskKey.Trim(), runId);

        return null;
    }

    /// <inheritdoc />
    public async Task<AgentRunDispatchOutcome> ResumeAsync(string taskKey, string? note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
            return AgentRunDispatchOutcome.Fail("任务键（taskKey）不能为空");

        var key = taskKey.Trim();
        var record = FindRecord(key);
        long runId = record?.RunId ?? 0;

        // 内存 miss（宿主重启）且键为纯数字 → 按 RunId 落库恢复
        if (runId <= 0 && long.TryParse(key, out var numeric) && numeric > 0)
            runId = numeric;

        if (runId <= 0)
            return AgentRunDispatchOutcome.Fail("任务不存在或尚未产生执行记录（仍在规划期），无法续跑");

        var dto = await _orchestrator.GetRunAsync(runId);
        if (dto == null)
            return AgentRunDispatchOutcome.Fail($"任务不存在或已失效（执行记录 {runId} 查无）");

        var status = MapStatus(dto.Status);
        if (dto.Status is not (AgentRunStatus.Stuck or AgentRunStatus.Failed))
            return AgentRunDispatchOutcome.Fail($"当前状态 {status} 不可续跑（仅 Stuck/Failed 允许）");

        // 归位 inflight：stuck/failed 后台驱动已结束时移除过，续跑重新占位（同会话并发闸继续生效）
        var sessionId = dto.SessionId ?? record?.SessionId ?? $"imtask:unknown-{runId}";
        if (!_inflight.TryAdd(sessionId, runId))
        {
            _inflight.TryGetValue(sessionId, out var running);
            return AgentRunDispatchOutcome.Fail(running > 0 && running != runId
                ? $"该会话已有任务在执行（#{running}）"
                : "该会话已有任务在执行");
        }

        if (record == null)
        {
            record = new DispatchRecord
            {
                TaskKey = key,
                SessionId = sessionId,
                AgentId = NullIfEmpty(dto.AgentId),
                AgentName = dto.AgentName,
                RunId = runId,
                Status = "running",
                StartedAt = dto.CreateTime,
            };
            StoreRecord(record);
        }
        else
        {
            record.RunId = runId;
            record.Status = "running";
        }

        XTrace.Log.Info("[AgentRunDispatch] 续跑 taskKey={0} Run={1} 原状态={2} Note={3}",
            key, runId, status, string.IsNullOrWhiteSpace(note) ? "-" : note);

        _ = DriveAsync(record, runRequest: null, resumeRunId: runId);

        return AgentRunDispatchOutcome.Ok(key, record.AgentId, record.AgentName, "running", runId);
    }

    // ---- 后台驱动（ScheduledAgentRunTrigger 同款事件流迭代） ----

    private async Task DriveAsync(DispatchRecord record, RunRequest? runRequest, long? resumeRunId)
    {
        try
        {
            record.StartedAt ??= DateTime.Now;

            string? lastType = null;
            string? lastContent = null;
            long runId = 0;

            var stream = resumeRunId is > 0
                ? _orchestrator.ResumeAsync(resumeRunId.Value, CancellationToken.None)
                : _orchestrator.RunAsync(runRequest!, CancellationToken.None);

            await foreach (var ev in stream)
            {
                lastType = ev.Type;
                lastContent = ev.Content;

                var id = TryReadRunId(ev);
                if (id > 0)
                {
                    runId = id;
                    record.RunId = id;
                    _inflight[record.SessionId] = id;   // 受理时占位 0 → 现在归位真实 RunId
                    if (record.Status is "pending" or "planning")
                        record.Status = "running";
                }

                switch (ev.Type)
                {
                    case "done":
                        MarkTerminal(record, "completed", ev.Content);
                        break;
                    case "error":
                        MarkTerminal(record, "failed", ev.Content);
                        break;
                    case "step_started":
                    case "step_completed":
                        // T16：步骤进度进快照（进展推送指纹用）
                        record.CurrentStepIndex = TryReadStepIndex(ev.Content) ?? record.CurrentStepIndex;
                        break;
                    case "run_stuck":
                        record.Status = "stuck";        // stuck 非终态：可经审批/人工续跑回 running
                        record.ResultText = ReadReason(ev.Content);
                        break;
                }
            }

            // 事件流走完仍非终态：以落库 Run 状态收口（done/error/run_stuck 已在循环内处理）
            if (!IsTerminal(record.Status))
            {
                if (runId <= 0)
                {
                    var listed = await _orchestrator.ListRunsAsync(record.SessionId, null, 1, 1);
                    runId = listed.Items.FirstOrDefault()?.Id ?? 0;
                    if (runId > 0) record.RunId = runId;
                }

                var run = runId > 0 ? await _orchestrator.GetRunAsync(runId) : null;
                if (run != null)
                {
                    var status = MapStatus(run.Status);
                    if (IsTerminal(status))
                        MarkTerminal(record, status, ProjectResultText(run));
                    else if (status is "pending" or "planning" or "running")
                        record.Status = status;          // 极端：流被取消但 Run 未终态，交给 Watcher 继续跟
                }
                else
                {
                    record.Status = lastType switch
                    {
                        "error" => "failed",
                        "done" => "completed",
                        _ => string.IsNullOrEmpty(lastType) ? "unknown" : record.Status,
                    };
                    if (IsTerminal(record.Status))
                        record.EndedAt = DateTime.Now;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 后台驱动不受调用方 ct 取消；此处仅防旁路取消把记录打成失败
            XTrace.Log.Warn("[AgentRunDispatch] 后台驱动被取消 taskKey={0}", record.TaskKey);
        }
        catch (Exception ex)
        {
            MarkTerminal(record, "failed", $"后台驱动异常：{ex.Message}");
        }
        finally
        {
            _inflight.TryRemove(record.SessionId, out _);
        }
    }

    private static void MarkTerminal(DispatchRecord record, string status, string? resultText)
    {
        record.Status = status;
        if (!string.IsNullOrWhiteSpace(resultText))
            record.ResultText = resultText;
        record.EndedAt = DateTime.Now;
    }

    // ---- 查询与投影 ----

    private DispatchRecord? FindRecord(string taskKey)
    {
        if (_records.TryGetValue(taskKey, out var hit)) return hit;
        // 大小写兜底：taskKey 是 GUID N，理论全小写；防御消费方传大写
        var lower = taskKey.ToLowerInvariant();
        return lower != taskKey && _records.TryGetValue(lower, out hit) ? hit : null;
    }

    private async Task<AgentRunDispatchSnapshot?> ProjectFromDbAsync(string taskKey, long runId)
    {
        var dto = await _orchestrator.GetRunAsync(runId);
        if (dto == null) return null;

        var status = MapStatus(dto.Status);
        return new AgentRunDispatchSnapshot
        {
            TaskKey = taskKey,
            RunId = dto.Id,
            Status = status,
            Terminal = IsTerminal(status),
            ResultText = ProjectResultText(dto),
            AgentName = dto.AgentName,
            StartedAt = dto.CreateTime,
            EndedAt = IsTerminal(status) ? dto.UpdateTime : null,
        };
    }

    /// <summary>DB 投影结果文本：stuck → StuckReason；completed → 收据 summary；尽力而为。</summary>
    internal static string? ProjectResultText(AgentRunDto dto)
    {
        // T10-A：completed 态只投影收据 summary——stuck→resume→complete 场景 StuckReason
        // 是中间态残留（历史行未清时兜底），不得作为完成结果下发用户。
        if (dto.Status == AgentRunStatus.Completed)
        {
            if (!string.IsNullOrWhiteSpace(dto.ResultReceipt))
            {
                try
                {
                    using var doc = JsonDocument.Parse(dto.ResultReceipt);
                    if (doc.RootElement.TryGetProperty("summary", out var summary))
                        return summary.GetString();
                }
                catch (JsonException)
                {
                    // 收据不是 JSON 时忽略（历史数据兼容）→ completed 态宁可空也不回退 StuckReason
                }
            }
            return null;
        }
        if (!string.IsNullOrWhiteSpace(dto.StuckReason))
            return dto.StuckReason;
        if (!string.IsNullOrWhiteSpace(dto.ResultReceipt))
        {
            try
            {
                using var doc = JsonDocument.Parse(dto.ResultReceipt);
                if (doc.RootElement.TryGetProperty("summary", out var summary))
                    return summary.GetString();
            }
            catch (JsonException)
            {
                // 收据不是 JSON 时忽略（历史数据兼容）
            }
        }
        return null;
    }

    /// <summary>开放态 Run（同会话并发闸第二道防线，含 Stuck——与 ScheduledAgentRunTrigger.FindOpenRunAsync 同语义）。</summary>
    private async Task<AgentRunDto?> FindOpenRunAsync(string sessionId)
    {
        AgentRunStatus[] open =
        [
            AgentRunStatus.Pending,
            AgentRunStatus.Planning,
            AgentRunStatus.Running,
            AgentRunStatus.Stuck,
        ];
        foreach (var status in open)
        {
            var page = await _orchestrator.ListRunsAsync(sessionId, status, 1, 1);
            var hit = page.Items.FirstOrDefault();
            if (hit != null) return hit;
        }
        return null;
    }

    // ---- AgentRef 解析（GetAgent → 按名精确匹配 → miss 列候选） ----

    private (string? AgentId, string? AgentName, string? Error) ResolveAgent(string? agentRef)
    {
        if (string.IsNullOrWhiteSpace(agentRef))
            return (null, null, null); // 空 = 交给 orchestrator 既有默认链

        var @ref = agentRef.Trim();
        var agent = _registry?.GetAgent(@ref);
        if (agent is { IsEnabled: true })
            return (agent.Id, agent.Name, null);

        var byName = _registry?.GetAllAgents()
            .FirstOrDefault(a => string.Equals(a.Name, @ref, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
            return (byName.Id, byName.Name, null);

        var candidates = _registry == null
            ? "Agent 注册表不可用"
            : string.Join("、", _registry.GetAllAgents().Select(a => $"{a.Id}（{a.Name}）"));
        return (null, null, $"未找到 Agent「{@ref}」（不存在或已停用）。可用 Agent：{candidates}");
    }

    // ---- 静态助手（与 ScheduledAgentRunTrigger 同语义的小工具，私有复制避免改既有文件） ----

    internal static string ComposePrompt(string taskInput)
    {
        return taskInput.Trim()
            + "\n\n## 审批策略\n"
            + "写操作仍走现有人工审批，不得无人值守直接执行系统命令。仅在本机宿主进程内执行。\n"
            + "本派发不关闭审批、不代跑系统命令，不做云端执行。";
    }

    internal static string MapStatus(AgentRunStatus status) => status switch
    {
        AgentRunStatus.Pending => "pending",
        AgentRunStatus.Planning => "planning",
        AgentRunStatus.Running => "running",
        AgentRunStatus.Completed => "completed",
        AgentRunStatus.Stuck => "stuck",
        AgentRunStatus.Failed => "failed",
        AgentRunStatus.Cancelled => "cancelled",
        _ => "unknown",
    };

    /// <summary>终态判定：completed/failed/cancelled。stuck 可续跑回 running，不算终态。</summary>
    internal static bool IsTerminal(string status) =>
        status is "completed" or "failed" or "cancelled";

    /// <summary>步骤事件 payload 里的 StepIndex（T16 进展指纹；解析失败返回 null）。</summary>
    private static int? TryReadStepIndex(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("stepIndex", out var idx) && idx.TryGetInt32(out var v))
                return v;
        }
        catch (JsonException)
        {
        }
        return null;
    }

    private static long TryReadRunId(AgentLoopEvent ev)
    {
        if (string.IsNullOrWhiteSpace(ev.Content)) return 0;
        if (ev.Type is not ("step_started" or "step_completed" or "run_stuck" or "goal_round_started" or "goal_evaluated"))
            return 0;
        try
        {
            using var doc = JsonDocument.Parse(ev.Content);
            if (doc.RootElement.TryGetProperty("runId", out var id) && id.TryGetInt64(out var n))
                return n;
        }
        catch (JsonException)
        {
            // 载荷不是 JSON 时忽略，改由会话回读
        }
        return 0;
    }

    private static string? ReadReason(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("reason", out var reason))
                return reason.GetString();
        }
        catch (JsonException)
        {
            return json;
        }
        return json;
    }

    private static string? NullIfEmpty(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text;

    private void StoreRecord(DispatchRecord record)
    {
        _records[record.TaskKey] = record;
        _recordOrder.Enqueue(record.TaskKey);
        while (_records.Count > MaxMemoryRecords && _recordOrder.TryDequeue(out var oldest))
            _records.TryRemove(oldest, out _);
    }

    /// <summary>进程内派发记录（可变快照源；Find 投影时取当下值）。</summary>
    internal sealed class DispatchRecord
    {
        public string TaskKey { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
        public string? AgentId { get; set; }
        public string? AgentName { get; set; }
        public long? RunId { get; set; }
        public string Status { get; set; } = "pending";
        public string? ResultText { get; set; }
        public int? CurrentStepIndex { get; set; }
        public int? StepCount { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public AgentRunDispatchSnapshot ToSnapshot() => new()
        {
            TaskKey = TaskKey,
            RunId = RunId,
            Status = Status,
            Terminal = IsTerminal(Status),
            ResultText = ResultText,
            AgentName = AgentName,
            StartedAt = StartedAt,
            EndedAt = EndedAt,
        CurrentStepIndex = CurrentStepIndex,
        StepCount = StepCount,
        };
    }
}
