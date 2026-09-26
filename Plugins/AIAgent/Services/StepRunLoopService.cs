using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;
using NewLife.Log;
// RunAgentLoopAsync 消费 Abstractions.AIChatMessage；Entities 亦有同名实体，显式别名消歧。
using LoopMessage = ForgeSelf.Abstractions.AIChatMessage;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 单步执行结果（StepRunLoopService 出口汇总，供 RunOrchestrator 落库与决策）。
/// </summary>
public class StepRunResult
{
    /// <summary>LLM 调 complete_step 完成。</summary>
    public bool Completed { get; set; }

    /// <summary>complete_step 声明的产出。</summary>
    public string? Output { get; set; }

    /// <summary>LLM 调 request_help 或迭代超限卡住。</summary>
    public bool Stuck { get; set; }

    /// <summary>卡住原因（request_help.reason 原文 / 迭代超限说明）。</summary>
    public string? StuckReason { get; set; }

    /// <summary>运行异常失败。</summary>
    public bool Failed { get; set; }

    /// <summary>失败错误信息。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>本步 token 用量汇总。</summary>
    public long TokensUsed { get; set; }

    /// <summary>本步工具调用轨迹（名/参数摘要/结果摘要/成败）。</summary>
    public List<StepToolCallTrace> ToolCalls { get; set; } = new();

    /// <summary>组装给 LLM 的上下文（InputJson 落库用）。</summary>
    public string? InputJson { get; set; }
}

/// <summary>单步工具调用轨迹项（ToolCallsJson 落库用）。</summary>
public class StepToolCallTrace
{
    public string Name { get; set; } = string.Empty;
    public string? ArgumentsSummary { get; set; }
    public string? ResultSummary { get; set; }
    public bool Success { get; set; } = true;
}

public interface IStepRunLoopService
{
    /// <summary>
    /// 步骤循环（design.md §5 RunStepLoopAsync / research.md R2）：
    /// 组装步骤上下文 → 复用 RunAgentLoopAsync（挂 complete_step/request_help + allowedTools 白名单）→
    /// 消费特殊工具 <c>tool_call</c> 事件产出类型化出口事件并终止本步；透传其余事件给调用方 SSE。
    /// </summary>
    /// <param name="run">执行实例（读 CurrentStepIndex 与前序产出用）。</param>
    /// <param name="plan">执行计划（Plan 全景注入用）。</param>
    /// <param name="step">本步定义。</param>
    /// <param name="result">出口结果容器（方法内填充后返回；调用方据此落库）。</param>
    /// <param name="chatModelId">聊天模型 id；空则回退默认 provider。</param>
    /// <param name="humanOverride">人工补位产出（人工介入 override 后续跑本步时注入）。</param>
    /// <param name="ct">取消令牌。</param>
    IAsyncEnumerable<AgentLoopEvent> RunStepLoopAsync(
        AgentRun run, AgentPlan plan, AgentPlanStep step, StepRunResult result,
        string? chatModelId, string? humanOverride, CancellationToken ct);
}

/// <summary>
/// 计划驱动执行的步骤循环（tasks.md T014）：
/// 每个步骤 = 一次 RunAgentLoopAsync 调用，注入「步骤上下文 + complete_step/request_help 特殊工具 +
/// 步骤级 allowedTools 白名单」。出口判定 = 消费 SSE 事件中的特殊工具 type，不重写模型调用链（R1/R2）。
/// </summary>
public class StepRunLoopService : IStepRunLoopService
{
    private const string CompleteStepToolName = "complete_step";
    private const string RequestHelpToolName = "request_help";

    /// <summary>单步执行超时（秒）：超时视为卡住保留现场（见 RunStepLoopAsync 超时兜底）。</summary>
    private const int StepTimeoutSeconds = 45;

    private readonly IAIAgentService _aiAgentService;
    private readonly RunFlowToolSet _tools;
    private readonly IAgentRegistryService? _agentRegistry;

    public StepRunLoopService(IAIAgentService aiAgentService, RunFlowToolSet tools, IAgentRegistryService? agentRegistry = null)
    {
        _aiAgentService = aiAgentService;
        _tools = tools;
        _agentRegistry = agentRegistry;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentLoopEvent> RunStepLoopAsync(
        AgentRun run, AgentPlan plan, AgentPlanStep step, StepRunResult result,
        string? chatModelId, string? humanOverride,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var messages = BuildStepMessages(run, plan, step, humanOverride);
        result.InputJson = SummarizeContext(messages);

        // 单步超时兜底：上游不可达/冷启动时 LLM 调用可能长时间阻塞（实测 e2e localhost:1234 挂起 >30s）。
        // 单个计划步骤是粒度适中的可独立验证单元，设 45s 上限；超时视为卡住（stuck 保留现场可人工继续，
        // design.md 卡住语义），与「迭代超限未 submit complete_step → 卡住」同路径。上游可用时正常步骤不会触达此限。
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

        var gotExit = false;
        // 手动枚举 + 安全取值：异常包在 MoveNextSafeAsync 内，yield 全部在 try 之外（CS1626 约束），
        // 既保持事件流式透传，又把上游异常归一到 result.Failed（由 RunOrchestrator 落库终态）。
        var enumerator = _aiAgentService.RunAgentLoopAsync(
            messages, chatModelId, run.AgentId, step.AllowedTools, null, true, timeoutCts.Token, _tools.Stepping())
            .GetAsyncEnumerator(timeoutCts.Token);
        await using var _ = enumerator;
        while (true)
        {
            var (moved, ev, error) = await MoveNextSafeAsync(enumerator);
            if (error != null)
            {
                if (error is OperationCanceledException && !ct.IsCancellationRequested)
                {
                    // 单步超时（非用户取消）：视为卡住保留现场，可人工介入继续，不判失败。
                    result.Stuck = true;
                    result.StuckReason = $"步骤执行超过 {StepTimeoutSeconds}s 超时，已暂停等待人工介入";
                    XTrace.Log.Warn("[StepRunLoop] 步骤 {0} 执行超时，置卡住: {1}", step.Id, result.StuckReason);
                    yield return new AgentLoopEvent { Type = RequestHelpToolName, Content = result.StuckReason };
                    break;
                }
                result.Failed = true;
                result.ErrorMessage = error.Message;
                XTrace.Log.Error("[StepRunLoop] 步骤 {0} 执行异常: {1}", step.Id, error.Message);
                break;
            }
            if (!moved) break;

            switch (ev.Type)
            {
                case "tool_call":
                    RecordToolCall(result, ev);
                    if (string.Equals(ev.Name, CompleteStepToolName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Completed = true;
                        result.Output = ParseOutput(ev.Arguments);
                        gotExit = true;
                        yield return new AgentLoopEvent { Type = CompleteStepToolName, Content = result.Output };
                        yield break;
                    }
                    if (string.Equals(ev.Name, RequestHelpToolName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Stuck = true;
                        result.StuckReason = ParseReason(ev.Arguments);
                        gotExit = true;
                        yield return new AgentLoopEvent { Type = RequestHelpToolName, Content = result.StuckReason };
                        yield break;
                    }
                    yield return ev; // 普通工具调用透传
                    break;

                case "tool_result":
                    CompleteToolCall(result, ev);
                    yield return ev;
                    break;

                case "usage":
                    if (ev.Usage != null) result.TokensUsed = ev.Usage.TotalTokens;
                    yield return ev;
                    break;

                default:
                    yield return ev; // content / done / error 透传
                    break;
            }
        }

        // 循环自然结束（LLM 未调 complete_step/request_help，含迭代超限）→ 卡住保留现场（T011）。
        if (!gotExit && !result.Failed)
        {
            result.Stuck = true;
            result.StuckReason = "迭代达到上限仍未调用 complete_step 声明完成，已暂停等待人工介入";
            XTrace.Log.Warn("[StepRunLoop] 步骤 {0} 未提交 complete_step，置卡住: {1}", step.Id, result.StuckReason);
        }
    }

    /// <summary>组装步骤消息：system = Agent 人格 + Plan 全景 + 前序产出摘要 + 本步 objective/expectedOutput + 人工补位。</summary>
    private List<LoopMessage> BuildStepMessages(AgentRun run, AgentPlan plan, AgentPlanStep step, string? humanOverride)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是计划驱动执行引擎的步骤执行者。按照给定的执行计划逐步完成任务。");
        sb.AppendLine();

        // Agent 人格注入（与 RunAgentLoopAsync ResolveSystemPrompt 同源）。
        var agent = _agentRegistry?.GetAgent(run.AgentId);
        if (agent != null)
        {
            sb.AppendLine($"## 角色设定（Agent「{agent.Name}」）");
            if (!string.IsNullOrWhiteSpace(agent.Description))
                sb.AppendLine(agent.Description);
            if (!string.IsNullOrWhiteSpace(agent.SystemPrompt))
                sb.AppendLine(agent.SystemPrompt);
            sb.AppendLine();
        }

        // Plan 全景 + 当前位置标注。
        sb.AppendLine("## 执行计划全景");
        sb.AppendLine($"目标：{plan.Goal}");
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var p = plan.Steps[i];
            var marker = p.Id == step.Id ? " ← 当前步骤" : string.Empty;
            sb.AppendLine($"- [{i + 1}/{plan.Steps.Count}] {p.Name}：{p.Objective}{marker}");
        }
        sb.AppendLine();

        // 前序已完成步骤的产出摘要（resume 后仍进入上下文，T011/D5）。
        var priorOutputs = LoadPriorOutputs(run.Id, step.Id);
        if (priorOutputs.Count > 0)
        {
            sb.AppendLine("## 前序步骤产出（可引用，勿重复执行）");
            foreach (var (stepId, name, output) in priorOutputs)
            {
                sb.AppendLine($"### 步骤 {stepId}（{name}）产出");
                sb.AppendLine(Truncate(output, 2000));
            }
            sb.AppendLine();
        }

        // 本步目标。
        sb.AppendLine("## 当前步骤");
        sb.AppendLine($"步骤名：{step.Name}");
        sb.AppendLine($"目标：{step.Objective}");
        if (!string.IsNullOrWhiteSpace(step.ExpectedOutput))
            sb.AppendLine($"期望产出：{step.ExpectedOutput}");
        if (step.AllowedTools is { Count: > 0 })
            sb.AppendLine($"本步允许工具：{string.Join(", ", step.AllowedTools)}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(humanOverride))
        {
            sb.AppendLine("## 人工补位产出（直接采纳，本步不再产出）");
            sb.AppendLine(humanOverride);
            sb.AppendLine();
        }

        sb.AppendLine("执行要求：");
        sb.AppendLine($"- 只完成当前步骤「{step.Name}」，不要提前执行后续步骤。");
        sb.AppendLine("- 完成后调用 complete_step 工具提交本步产出（output 字段）。");
        sb.AppendLine("- 无法推进时调用 request_help 工具说明卡住原因，等待人工介入。");
        sb.AppendLine("- 不要用自由文本声明完成，必须通过工具提交。");

        var systemPrompt = sb.ToString();
        return new List<LoopMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = $"任务原文：{run.TaskInput}\n\n请开始执行本步「{step.Name}」。完成或求助均通过对应工具提交。" }
        };
    }

    /// <summary>读取前序已完成步骤的产出（StepRun.Status=Completed，按 StepIndex 升序）。</summary>
    private static List<(string StepId, string Name, string Output)> LoadPriorOutputs(long runId, string currentStepId)
    {
        var result = new List<(string, string, string)>();
        try
        {
            var steps = AgentStepRun.FindAllByRunId(runId)
                .Where(s => s.Status == (int)AgentStepStatus.Completed && s.StepId != currentStepId)
                .OrderBy(s => s.StepIndex);
            foreach (var s in steps)
            {
                if (string.IsNullOrWhiteSpace(s.OutputJson)) continue;
                result.Add((s.StepId ?? string.Empty, s.Name ?? string.Empty, s.OutputJson));
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[StepRunLoop] 读取前序产出失败: {0}", ex.Message);
        }
        return result;
    }

    /// <summary>入参摘要：截断 system + user 文本（防超长落库）。</summary>
    private static string SummarizeContext(List<LoopMessage> messages)
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var m in messages)
            {
                sb.Append($"[{m.Role}] {Truncate(m.Content, 3000)}\n");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"上下文组装失败: {ex.Message}";
        }
    }

    private static void RecordToolCall(StepRunResult result, AgentLoopEvent ev)
    {
        result.ToolCalls.Add(new StepToolCallTrace
        {
            Name = ev.Name ?? string.Empty,
            ArgumentsSummary = Truncate(ev.Arguments, 500)
        });
    }

    private static void CompleteToolCall(StepRunResult result, AgentLoopEvent ev)
    {
        var last = result.ToolCalls.LastOrDefault(t => t.Name == ev.Name);
        if (last == null) return;
        last.ResultSummary = Truncate(ev.Result, 500);
        last.Success = ev.Success ?? true;
    }

    /// <summary>安全推进枚举器：异常统一归一为 error 元组返回，不让 yield 出现在 try/catch 内（CS1626）。</summary>
    private static async Task<(bool Moved, AgentLoopEvent? Ev, Exception? Error)> MoveNextSafeAsync(
        IAsyncEnumerator<AgentLoopEvent> enumerator)
    {
        try
        {
            var moved = await enumerator.MoveNextAsync();
            return (moved, moved ? enumerator.Current : null, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex);
        }
    }

    /// <summary>complete_step Arguments → output。</summary>
    private static string? ParseOutput(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            return doc.RootElement.TryGetProperty("output", out var o) ? o.GetString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>request_help Arguments → reason。</summary>
    private static string? ParseReason(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return "（未说明原因）";
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            return doc.RootElement.TryGetProperty("reason", out var r) && !string.IsNullOrWhiteSpace(r.GetString())
                ? r.GetString()
                : "（未说明原因）";
        }
        catch
        {
            return "（参数解析失败）";
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }
}
