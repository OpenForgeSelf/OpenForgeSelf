using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 「本工具内置 AI Agent」执行接缝实现（<see cref="IBuiltInAgentExecution"/>）。
///
/// 包住既有「计划驱动执行」（<see cref="IRunOrchestratorService"/>）的后台形态：
/// <list type="bullet">
/// <item><b>StartAsync</b>：后台消费 <c>RunAsync</c> SSE 迭代器（事件丢弃，仅让 Run 落库跑完），
/// 用 <see cref="TaskCompletionSource{Int64}"/> 在首个 <c>step_started</c> 事件捕获 runId，立即返回；</item>
/// <item><b>FindAsync</b>：<c>GetRunDetailAsync</c> → 快照映射（状态词表直转；结果摘要取末步骤
/// complete_step 声明产出；文件变更从步骤工具轨迹尽力提取路径）；</item>
/// <item><b>ListAgents</b>：七角色（协调者/分析师/评论家/通用助手/写作者/研究员/程序员）。</item>
/// </list>
/// 消费方（todo-tracker）经 <c>ctx.Get&lt;IBuiltInAgentExecution&gt;()</c> 每次用每次取。
/// </summary>
public class BuiltInAgentExecutionService : IBuiltInAgentExecution
{
    /// <summary>
    /// 内部默认聊天模型：ornith-1.0-9b（9B，实测唯一走通工具循环的本地模型）。
    /// qwen3.5-4b（4B）工具调用不可靠（不调工具直接收束 → run_stuck），不在此处兜底；
    /// 默认模型引导记 TODO（AIAgent 设置页）。
    /// </summary>
    public const string DefaultChatModelId = "default:ornith-1.0-9b";

    private readonly IRunOrchestratorService _orchestrator;
    private readonly IAgentRegistryService _agentRegistry;
    private readonly IContext? _ctx;

    public BuiltInAgentExecutionService(IRunOrchestratorService orchestrator, IAgentRegistryService agentRegistry, IContext? ctx = null)
    {
        _orchestrator = orchestrator;
        _agentRegistry = agentRegistry;
        _ctx = ctx;
    }

    /// <inheritdoc />
    public async Task<BuiltInAgentOutcome> StartAsync(BuiltInAgentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return BuiltInAgentOutcome.Fail("任务原文（Prompt）不能为空");

        // 工作目录：消费方传了项目根就先把当前工作区切过去（内置引擎的文件/终端工具都挂在 workspace 上）。
        // 已知取舍：workspace 是全局单例，todo 委派覆盖会顺带切换 AIAgent 页面的工作目录——per-run 隔离属后续改造（记 TODO）。
        if (!string.IsNullOrWhiteSpace(request.Cwd))
        {
            var workspace = _ctx?.Get<IProjectWorkspaceService>();
            if (workspace == null)
            {
                XTrace.Log.Warn("[BuiltInAgentExecution] 工作区服务缺席，无法设置工作目录 {0}", request.Cwd);
                return BuiltInAgentOutcome.Fail("工作区服务缺席：无法设置工作目录，请先关联项目后再试。");
            }

            if (!workspace.TrySetProjectRoot(request.Cwd!, out var wsError))
            {
                XTrace.Log.Warn("[BuiltInAgentExecution] 设置工作目录失败（{0}）：{1}", request.Cwd, wsError);
                return BuiltInAgentOutcome.Fail($"工作目录不可用：{request.Cwd}（{wsError}）。请先在任务上关联项目。");
            }
        }

        // 角色缺省：todo 下发 = 干活场景，默认程序员；非法角色 id 由 AIAgent 侧裁决
        var roleId = string.IsNullOrWhiteSpace(request.AgentRoleId) ? "agent.programmer" : request.AgentRoleId!;
        var runRequest = new RunRequest
        {
            AgentId = roleId,
            TaskInput = request.Prompt,
            ChatModelId = string.IsNullOrWhiteSpace(request.ChatModelId) ? DefaultChatModelId : request.ChatModelId
        };

        // 后台消费迭代器（事件丢弃，仅让 Run 落库跑完）；step_started 携带 runId
        var runIdTcs = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var ev in _orchestrator.RunAsync(runRequest, CancellationToken.None))
                {
                    if (runIdTcs.Task.IsCompleted) continue;
                    if (ev.Type == "step_started" && ev.Content != null)
                    {
                        try
                        {
                            var payload = JsonSerializer.Deserialize<StepStartedPayload>(ev.Content,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (payload?.RunId > 0) runIdTcs.TrySetResult(payload.RunId);
                        }
                        catch (JsonException) { /* 载荷异常不致命，继续等 */ }
                    }
                }
            }
            catch (Exception ex)
            {
                runIdTcs.TrySetException(ex);
            }
        });

        try
        {
            // 规划阶段（submit_plan 是独立模型回合）慢属常态：9B 本地模型实测可达数十秒，窗口放宽到 60s。
            var runId = await runIdTcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
            var agent = _agentRegistry.GetAgent(roleId);
            return BuiltInAgentOutcome.Ok(runId, agent?.Name ?? roleId, "Pending");
        }
        catch (TimeoutException)
        {
            return BuiltInAgentOutcome.Fail("本工具 AI Agent 启动偏慢：60 秒内未进入执行阶段（模型通道繁忙或规划失败；后台可能仍在执行，可到「AI Agent → 运行记录」查看）");
        }
        catch (Exception ex)
        {
            return BuiltInAgentOutcome.Fail($"本工具 AI Agent 启动失败：{ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<BuiltInAgentSnapshot?> FindAsync(long runId, CancellationToken ct = default)
    {
        var detail = await _orchestrator.GetRunDetailAsync(runId);
        if (detail == null) return null;

        var run = detail.Run;
        var statusText = run.Status.ToString().ToLowerInvariant();
        var terminal = run.Status is AgentRunStatus.Completed or AgentRunStatus.Failed or AgentRunStatus.Cancelled;

        var snapshot = new BuiltInAgentSnapshot
        {
            RunId = run.Id,
            Status = statusText,
            Terminal = terminal,
            ElapsedMs = (long)(run.UpdateTime - run.CreateTime).TotalMilliseconds,
            AgentName = run.AgentName,
            ErrorMessage = run.StuckReason
        };

        // 结果摘要：末条 Completed 步骤的 complete_step 声明产出；失败取错误原文；卡住取原因
        var steps = detail.Steps.OrderBy(s => s.StepIndex).ToList();
        var lastCompleted = steps.LastOrDefault(s => s.Status == AgentStepStatus.Completed && !string.IsNullOrWhiteSpace(s.OutputJson));
        if (lastCompleted != null)
        {
            snapshot.ResultSummary = lastCompleted.OutputJson?.Trim();
            if (snapshot.ResultSummary?.Length > 1200) snapshot.ResultSummary = snapshot.ResultSummary[..1200] + "…（已截断）";
        }
        else if (run.Status == AgentRunStatus.Failed)
        {
            var err = steps.LastOrDefault(s => !string.IsNullOrWhiteSpace(s.ErrorMessage))?.ErrorMessage ?? run.StuckReason;
            snapshot.ResultSummary = $"执行失败：{err}";
        }
        else if (run.Status == AgentRunStatus.Stuck)
        {
            snapshot.ResultSummary = $"已卡住，等待人工介入：{run.StuckReason}";
        }
        else if (run.Status == AgentRunStatus.Cancelled)
        {
            snapshot.ResultSummary = "执行已取消";
        }
        else if (steps.Count == 0)
        {
            snapshot.ResultSummary = "执行中（规划阶段）";
        }
        else
        {
            snapshot.ResultSummary = $"执行中（步骤 {steps.Count(s => s.Status == AgentStepStatus.Completed)}/{steps.Count}）";
        }

        // 文件变更：从各步骤工具轨迹尽力提取路径（write_file / patch 等写类工具的 path 参数）
        snapshot.FilesChanged = ExtractFilePaths(steps);

        return snapshot;
    }

    /// <inheritdoc />
    public IReadOnlyList<BuiltInAgentOption> ListAgents()
    {
        try
        {
            return _agentRegistry.GetAllAgents()
                .Select(a => new BuiltInAgentOption { RoleId = a.Id, Name = a.Name, Vendor = "builtin" })
                .ToList();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AIAgentPlugin] ListAgents 失败: {0}", ex.Message);
            return [];
        }
    }

    private static readonly Regex FilePathPattern = new(
        @"path[\""']?\s*[:=]\s*[\""']([^\"",'}\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>从步骤工具轨迹 JSON 文本里提取写类工具的目标路径（尽力而为，解析不到返回空）。</summary>
    private static List<string> ExtractFilePaths(List<AgentStepRunDto> steps)
    {
        var found = new List<string>();
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.ToolCallsJson)) continue;
            if (!step.ToolCallsJson.Contains("write_file", StringComparison.OrdinalIgnoreCase)
                && !step.ToolCallsJson.Contains("patch", StringComparison.OrdinalIgnoreCase)
                && !step.ToolCallsJson.Contains("create_file", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (Match m in FilePathPattern.Matches(step.ToolCallsJson))
            {
                var p = m.Groups[1].Value.Trim();
                if (p.Length > 0 && !found.Contains(p)) found.Add(p);
            }
        }
        return found;
    }
}
