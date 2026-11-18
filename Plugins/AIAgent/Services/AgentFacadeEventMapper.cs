using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>把编排/对话运行时事件转成 /api/agent 统一事件信封。</summary>
internal static class AgentFacadeEventMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEnumerable<(string Type, object? Payload, long? NativeRunId)> MapLoopEvent(AgentLoopEvent ev)
    {
        switch (ev.Type)
        {
            case "content":
                if (!string.IsNullOrEmpty(ev.Content))
                    yield return ("message.delta", new { text = ev.Content }, null);
                break;
            case "tool_call":
                yield return ("tool.started", new { name = ev.Name, summary = Summarize(ev.Arguments) }, TryRunId(ev.Content));
                break;
            case "tool_result":
                yield return ("tool.completed", new
                {
                    name = ev.Name,
                    success = ev.Success ?? true,
                    summary = Summarize(ev.Result)
                }, TryRunId(ev.Content));
                break;
            case "step_started":
            {
                var step = Read(ev.Content);
                yield return ("step.started", new
                {
                    stepId = Text(step, "stepId") ?? Text(step, "id"),
                    name = Text(step, "name"),
                    objective = Text(step, "objective"),
                    stepIndex = Num(step, "stepIndex")
                }, Num(step, "runId"));
                break;
            }
            case "step_completed":
            {
                var step = Read(ev.Content);
                yield return ("step.completed", new
                {
                    stepId = Text(step, "stepId") ?? Text(step, "id"),
                    stepIndex = Num(step, "stepIndex"),
                    output = Text(step, "output"),
                    status = "completed"
                }, Num(step, "runId"));
                break;
            }
            case "run_stuck":
            {
                var stuck = Read(ev.Content);
                yield return ("run.stuck", new
                {
                    reason = Text(stuck, "reason") ?? ev.Content,
                    stepIndex = Num(stuck, "stepIndex")
                }, Num(stuck, "runId"));
                break;
            }
            case "plan_created":
                // 确认后的执行不应再规划；若旧路径误入，只记更新，不当作新提案。
                yield return ("plan.updated", ev.Content, TryRunId(ev.Content));
                break;
            case "done":
                if (!string.IsNullOrWhiteSpace(ev.Content))
                    yield return ("message.completed", new { text = ev.Content }, null);
                yield return ("run.completed", new { text = ev.Content }, null);
                break;
            case "error":
                yield return ("error", new { message = ev.Content }, null);
                yield return ("run.failed", new { message = ev.Content }, null);
                break;
            default:
                break;
        }
    }

    public static IEnumerable<(string Type, object? Payload)> MapFrame(TurnFrame frame, string? toolResult = null, string? toolName = null)
    {
        switch (frame)
        {
            case AssistantDelta delta when !string.IsNullOrEmpty(delta.Content):
                yield return ("message.delta", new { text = delta.Content });
                break;
            case ToolStarted started:
                yield return ("tool.started", new { name = started.ToolName, callId = started.CallId, summary = Summarize(started.ArgsJson) });
                break;
            case ToolCompleted done:
                yield return ("tool.completed", new
                {
                    callId = done.CallId,
                    name = toolName,
                    success = done.Outcome == ToolOutcome.Ok,
                    summary = Summarize(toolResult),
                    outcome = done.Outcome.ToString()
                });
                break;
            case StepStarted step:
                yield return ("step.started", new { stepId = step.StepId, stepIndex = step.Index });
                break;
            case StepCompleted step:
                yield return ("step.completed", new { stepId = step.StepId, status = step.Reason.ToString() });
                break;
            case TurnFailed failed:
                yield return ("error", new { message = failed.Error });
                yield return ("run.failed", new { message = failed.Error });
                break;
            default:
                break;
        }
    }

    public static string Summarize(string? raw, int max = 240)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var text = raw.Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }

    public static string? ExtractText(object? payload)
    {
        if (payload is string s) return s;
        if (payload is null) return null;
        foreach (var name in new[] { "text", "Text", "content", "Content", "message", "Message" })
        {
            var prop = payload.GetType().GetProperty(name);
            var value = prop?.GetValue(payload)?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static JsonElement? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement? el, string name)
    {
        if (el is not { } e) return null;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p))
            return p.ValueKind is JsonValueKind.String or JsonValueKind.Number ? p.ToString() : p.GetRawText();
        return null;
    }

    private static long? Num(JsonElement? el, string name)
    {
        if (el is not { } e) return null;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n)) return n;
            if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var s)) return s;
        }
        return null;
    }

    private static long? TryRunId(string? json) => Num(Read(json), "runId");
}
