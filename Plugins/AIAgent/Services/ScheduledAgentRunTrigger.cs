using System.Collections.Concurrent;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 调度器触发本机 MAF Run 的薄封装：同进程调用 <see cref="IRunOrchestratorService.RunAsync"/>，
/// 不自调 HTTP，不改审批挂起。同一计划任务已在跑（含等待审批）则跳过。
/// </summary>
public sealed class ScheduledAgentRunTrigger : IScheduledAgentRunTrigger
{
    private readonly IRunOrchestratorService _orchestrator;
    private readonly IContext? _ctx;
    private readonly ConcurrentDictionary<long, long> _inflight = new();

    public ScheduledAgentRunTrigger(IRunOrchestratorService orchestrator, IContext? ctx = null)
    {
        _orchestrator = orchestrator;
        _ctx = ctx;
    }

    /// <inheritdoc />
    public async Task<ScheduledAgentRunResult> TriggerAsync(ScheduledAgentRunRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ScheduledTaskId <= 0)
            return ScheduledAgentRunResult.Fail("计划任务 Id 无效");
        if (string.IsNullOrWhiteSpace(request.AgentId))
            return ScheduledAgentRunResult.Fail("Bot/AgentId 不能为空");
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return ScheduledAgentRunResult.Fail("Prompt 不能为空");
        if (string.IsNullOrWhiteSpace(request.Workspace))
            return ScheduledAgentRunResult.Fail("Workspace 目录不能为空");

        if (!_inflight.TryAdd(request.ScheduledTaskId, 0))
        {
            _inflight.TryGetValue(request.ScheduledTaskId, out var runningId);
            var which = runningId > 0 ? $"（#{runningId}）" : "";
            return ScheduledAgentRunResult.Skip(runningId > 0 ? runningId : null, "Running",
                $"已跳过：计划任务 {request.ScheduledTaskId} 已有本机 Run 在执行{which}。");
        }

        try
        {
            var sessionId = SessionIdFor(request.ScheduledTaskId);
            var open = await FindOpenRunAsync(sessionId);
            if (open != null)
            {
                return ScheduledAgentRunResult.Skip(open.Id, open.Status.ToString(),
                    $"已跳过：计划任务 {request.ScheduledTaskId} 已有未结束的本机 Run #{open.Id}（{open.Status}），不重复触发。写操作仍走现有审批。");
            }

            var workspace = _ctx?.Get<IProjectWorkspaceService>();
            if (workspace == null)
                return ScheduledAgentRunResult.Fail("工作区服务缺席，无法在本机设置 Workspace，已拒绝触发。");
            if (!workspace.TrySetProjectRoot(request.Workspace, out var wsError))
                return ScheduledAgentRunResult.Fail($"Workspace 不可用：{request.Workspace}（{wsError}）");

            var runRequest = new RunRequest
            {
                AgentId = request.AgentId.Trim(),
                SessionId = sessionId,
                TaskInput = ComposePrompt(request.Prompt, request.ApprovalPolicy)
            };

            string? lastType = null;
            string? lastContent = null;
            long runId = 0;
            await foreach (var ev in _orchestrator.RunAsync(runRequest, cancellationToken))
            {
                lastType = ev.Type;
                lastContent = ev.Content;
                var id = TryReadRunId(ev);
                if (id > 0)
                {
                    runId = id;
                    _inflight[request.ScheduledTaskId] = runId;
                }
            }

            if (runId <= 0)
            {
                var listed = await _orchestrator.ListRunsAsync(sessionId, null, 1, 1);
                runId = listed.Items.FirstOrDefault()?.Id ?? 0;
            }

            var run = runId > 0 ? await _orchestrator.GetRunAsync(runId) : null;
            var status = run?.Status.ToString() ?? lastType ?? "unknown";

            if (string.Equals(lastType, "error", StringComparison.OrdinalIgnoreCase))
                return ScheduledAgentRunResult.Fail($"本机 Run {(runId > 0 ? "#" + runId + " " : "")}失败：{Truncate(lastContent)}");

            var summary = lastType switch
            {
                "done" => Truncate(lastContent),
                "run_stuck" => "已挂起，等待现有审批。" + Truncate(ReadReason(lastContent)),
                _ => $"末事件 {lastType ?? "无"}"
            };

            if (runId <= 0)
                return ScheduledAgentRunResult.Fail($"未能创建本机 Run。{summary}");

            return ScheduledAgentRunResult.Ok(runId, status,
                $"本机 Run #{runId} 状态 {status}。{summary} 写操作仍走现有审批，未代跑系统命令。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ScheduledAgentRunResult.Fail($"触发本机 Run 失败：{ex.Message}");
        }
        finally
        {
            _inflight.TryRemove(request.ScheduledTaskId, out _);
        }
    }

    internal static string SessionIdFor(long taskId) => $"scheduler:{taskId}";

    private async Task<AgentRunDto?> FindOpenRunAsync(string sessionId)
    {
        AgentRunStatus[] open =
        [
            AgentRunStatus.Pending,
            AgentRunStatus.Planning,
            AgentRunStatus.Running,
            AgentRunStatus.Stuck
        ];
        foreach (var status in open)
        {
            var page = await _orchestrator.ListRunsAsync(sessionId, status, 1, 1);
            var hit = page.Items.FirstOrDefault();
            if (hit != null) return hit;
        }

        return null;
    }

    private static string ComposePrompt(string prompt, string? policy)
    {
        var note = string.IsNullOrWhiteSpace(policy)
            ? "写操作仍走现有人工审批，不得无人值守直接执行系统命令。仅在本机宿主进程内执行。"
            : policy.Trim();
        return prompt.Trim()
            + "\n\n## 审批策略\n"
            + note
            + "\n本触发不关闭审批、不代跑系统命令，不做云端执行。";
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

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= 500 ? text : text[..500] + "…";
    }
}
