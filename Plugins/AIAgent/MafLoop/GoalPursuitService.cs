using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using Microsoft.Extensions.AI;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.MafLoop;

/// <summary>目标评估结果（PILOT-051 FR-3）。</summary>
public sealed class GoalEvaluation
{
    /// <summary>评估判定：全部成功判据已达成。</summary>
    public bool Achieved { get; init; }

    /// <summary>评估置信度（0-1；评估器未给则为 null）。</summary>
    public double? Confidence { get; init; }

    /// <summary>未达成项清单（判据原文或差距描述）。</summary>
    public List<string> Missing { get; init; } = new();

    /// <summary>评估批评/差距分析。</summary>
    public string? Critique { get; init; }

    /// <summary>一句话结论（SSE 展示 + Stuck 报告用）。</summary>
    public string? Summary { get; init; }
}

/// <summary>
/// 目标驱动执行（PILOT-051 FR-3，plan-and-extend 行业模式）的评估器：
/// 计划步骤全部完成后，用一次<b>无工具</b> LLM 调用（经 <see cref="ForgeChatClientAdapter"/>，
/// 与执行同一 provider 面）对照成功判据做结构化判定。
/// </summary>
/// <remarks>
/// 可靠性约定（fail-safe，不假绿）：评估失败重试 1 次，仍失败按「未达成」处理并注明原因；
/// JSON 解析失败同按未达成。绝不把评估不可用误判成「已达成」。
/// </remarks>
public sealed class GoalPursuitService
{
    private const string EvaluatorSystemPrompt =
        """
你是目标达成评估器。给你：任务原文、执行计划（步骤与产出摘要）、成功判据清单。
你的唯一职责：逐条对照成功判据，判定任务是否已经真正达成（以计划产出与步骤声明为证据，不做乐观假设）。
只输出一个 JSON 对象，不要输出任何其他文字、不要用 Markdown 代码块包裹：
{"achieved": true|false, "confidence": 0.0-1.0, "missing": ["未达成的判据或差距", ...], "critique": "差距分析", "summary": "一句话结论"}
""";

    private const string CorrectiveSystemPrompt =
        """
你是执行计划修正器。给你：任务原文、当前计划与产出摘要、评估结论与未达成项。
你的唯一职责：产出「纠正补漏」步骤（只补缺口，不重复已完成的工作），指令必须可执行、可验证。
只输出一个 JSON 数组，不要输出任何其他文字、不要用 Markdown 代码块包裹：
[{"name": "步骤名", "objective": "给执行 Agent 的明确指令（可执行、可验证）"}]
""";

    private readonly IAIProviderRegistry? _providerRegistry;

    public GoalPursuitService(IAIProviderRegistry? providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    /// <summary>评估目标达成情况（失败重试 1 次，仍失败按未达成处理）。</summary>
    public async Task<GoalEvaluation> EvaluateAsync(
        string taskInput, AgentPlan plan, IReadOnlyList<string> criteria,
        string? chatModelId, CancellationToken ct)
    {
        string? lastError = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var text = await CallModelAsync(BuildEvaluatorUserPrompt(taskInput, plan, criteria), chatModelId, ct)
                    .ConfigureAwait(false);
                var parsed = ParseEvaluation(text);
                if (parsed != null)
                {
                    return parsed;
                }
                lastError = $"评估输出不是合法 JSON（长度 {text?.Length ?? 0}）";
                XTrace.Log.Warn("[GoalPursuit] 评估解析失败（第 {0} 次）: {1}", attempt, lastError);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                XTrace.Log.Warn("[GoalPursuit] 评估调用失败（第 {0} 次）: {1}", attempt, ex.Message);
            }
        }

        return new GoalEvaluation
        {
            Achieved = false,
            Missing = new List<string>(criteria),
            Critique = $"评估器不可用（已重试），按未达成处理: {lastError}",
            Summary = "目标评估未完成，不能确认达成"
        };
    }

    /// <summary>
    /// 生成纠正步骤（plan-and-extend）：按未达成项让模型产出补漏步骤；解析失败时回退为
    /// 单条「对照判据补漏」步骤（保证循环总能推进）。
    /// </summary>
    public async Task<List<AgentPlanStep>> GenerateCorrectiveStepsAsync(
        string taskInput, AgentPlan plan, IReadOnlyList<string> criteria, GoalEvaluation evaluation,
        string? chatModelId, CancellationToken ct)
    {
        var fallback = new List<AgentPlanStep>
        {
            new()
            {
                Id = $"fix-{DateTime.Now:HHmmss}",
                Name = "对照成功判据补漏",
                Objective = $"任务「{plan.Goal}」评估未达标。请逐条检查并补齐以下未达成项，完成后如实汇报：\n- "
                    + string.Join("\n- ", evaluation.Missing.Count > 0 ? evaluation.Missing : criteria),
                ExpectedOutput = "未达成项逐条补齐的产出"
            }
        };

        try
        {
            const string JsonExample = """
[{"name": "步骤名", "objective": "给执行 Agent 的明确指令（可执行、可验证）"}]
""";
            var prompt = $"""
任务原文：{taskInput}

当前计划与产出摘要：
{RenderPlanSummary(plan)}

评估结论：{evaluation.Summary}
未达成项：
- {string.Join("\n- ", evaluation.Missing.Count > 0 ? evaluation.Missing : criteria)}

请产出 1-3 条「纠正补漏」步骤（只补缺口，不重复已完成的）。只输出 JSON 数组，不要其他文字、不要代码块包裹：
{JsonExample}
""";
            var text = await CallModelAsync(prompt, CorrectiveSystemPrompt, chatModelId, ct).ConfigureAwait(false);
            var steps = ParseCorrectiveSteps(text);
            return steps.Count > 0 ? steps : fallback;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[GoalPursuit] 纠正步骤生成失败，回退单条补漏步骤: {0}", ex.Message);
            return fallback;
        }
    }

    // ---- 内部 ----

    private async Task<string?> CallModelAsync(string userPrompt, string? chatModelId, CancellationToken ct)
        => await CallModelAsync(userPrompt, EvaluatorSystemPrompt, chatModelId, ct).ConfigureAwait(false);

    private async Task<string?> CallModelAsync(
        string userPrompt, string systemPrompt, string? chatModelId, CancellationToken ct)
    {
        var provider = ResolveProvider(chatModelId, out var upstreamModelId)
            ?? throw new InvalidOperationException("目标评估器未找到可用的 AI 提供方");
        var adapter = new ForgeChatClientAdapter(provider, upstreamModelId);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };
        var response = await adapter.GetResponseAsync(messages, null, ct).ConfigureAwait(false);
        return response.Text;
    }

    private IAIProvider? ResolveProvider(string? chatModelId, out string upstreamModelId)
    {
        upstreamModelId = chatModelId ?? string.Empty;
        if (_providerRegistry == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(chatModelId))
        {
            upstreamModelId = string.Empty;
            return _providerRegistry.GetDefaultProvider();
        }

        var provider = _providerRegistry.GetProviderByChatModelId(chatModelId)
            ?? _providerRegistry.GetProviderByModel(chatModelId);
        var idx = chatModelId.IndexOf(':');
        upstreamModelId = idx > 0 ? chatModelId[(idx + 1)..] : chatModelId;
        return provider;
    }

    private static string BuildEvaluatorUserPrompt(string taskInput, AgentPlan plan, IReadOnlyList<string> criteria)
        => $"""
任务原文：{taskInput}

执行计划与产出摘要：
{RenderPlanSummary(plan)}

成功判据（逐条判定）：
- {string.Join("\n- ", criteria)}

请输出评估 JSON。
""";

    private static string RenderPlanSummary(AgentPlan plan)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("目标：").AppendLine(plan.Goal);
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            sb.Append(i + 1).Append(". ").Append(step.Name)
              .Append(" —— ").AppendLine(step.Objective);
        }
        return sb.ToString();
    }

    /// <summary>防御性解析评估 JSON（容忍 ```json 围栏与前后杂文）。</summary>
    internal static GoalEvaluation? ParseEvaluation(string? text)
    {
        var json = ExtractJson(text);
        if (json == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var achieved = false;
            if (root.TryGetProperty("achieved", out var achievedEl))
            {
                achieved = achievedEl.ValueKind == JsonValueKind.True
                    || (achievedEl.ValueKind == JsonValueKind.String
                        && string.Equals(achievedEl.GetString(), "true", StringComparison.OrdinalIgnoreCase));
            }

            double? confidence = null;
            if (root.TryGetProperty("confidence", out var confidenceEl)
                && confidenceEl.ValueKind == JsonValueKind.Number
                && confidenceEl.TryGetDouble(out var c))
            {
                confidence = c;
            }

            var missing = new List<string>();
            if (root.TryGetProperty("missing", out var missingEl) && missingEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in missingEl.EnumerateArray())
                {
                    var s = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) missing.Add(s!);
                }
            }

            root.TryGetProperty("critique", out var critiqueEl);
            root.TryGetProperty("summary", out var summaryEl);

            return new GoalEvaluation
            {
                Achieved = achieved,
                Confidence = confidence,
                Missing = missing,
                Critique = critiqueEl.ValueKind == JsonValueKind.String ? critiqueEl.GetString() : null,
                Summary = summaryEl.ValueKind == JsonValueKind.String ? summaryEl.GetString() : null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>防御性解析纠正步骤 JSON 数组。</summary>
    internal static List<AgentPlanStep> ParseCorrectiveSteps(string? text)
    {
        var json = ExtractJson(text, wantArray: true);
        var steps = new List<AgentPlanStep>();
        if (json == null) return steps;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return steps;

            var index = 0;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                string? name = null, objective = null;
                if (item.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                {
                    name = nameEl.GetString();
                }
                if (item.TryGetProperty("objective", out var objectiveEl) && objectiveEl.ValueKind == JsonValueKind.String)
                {
                    objective = objectiveEl.GetString();
                }
                if (string.IsNullOrWhiteSpace(objective)) continue;

                index++;
                steps.Add(new AgentPlanStep
                {
                    Id = $"fix-{DateTime.Now:HHmmss}-{index}",
                    Name = string.IsNullOrWhiteSpace(name) ? $"纠正补漏 {index}" : name!,
                    Objective = objective!,
                    ExpectedOutput = "未达成项补齐的产出"
                });
            }
        }
        catch
        {
            // 解析失败返回空 → 调用方回退 fallback 步骤
        }
        return steps;
    }

    /// <summary>从模型输出提取 JSON（首个 { 或 [ 到最后一个 } 或 ]）。</summary>
    private static string? ExtractJson(string? text, bool wantArray = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 去掉 ```json 围栏
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0) trimmed = trimmed[(firstNewline + 1)..].Trim();
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3].Trim();
            }
        }

        var openChar = wantArray ? '[' : '{';
        var closeChar = wantArray ? ']' : '}';
        var start = trimmed.IndexOf(openChar);
        var end = trimmed.LastIndexOf(closeChar);
        if (start < 0 || end <= start) return null;
        return trimmed[start..(end + 1)];
    }
}
