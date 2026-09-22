using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// 事件归一化管道：把各 agent 五花八门的输出（纯文本 / JSONL / 单个 JSON 对象）
/// 映射成统一的 <see cref="AgentEvent"/> 流。
///
/// 设计要点（design §4 + §14）：
/// - 映射规则来自 profile 的 OutputMapping（声明式，不改代码即可扩）；
/// - 映射表缺项 / 命中不了时**降级为 Text 事件**，绝不丢内容、绝不静默失败；
/// - 单事件载荷超限则截断并置 Truncated，前端显式提示（不假装完整）。
/// </summary>
public class EventPipeline
{
    /// <summary>单事件文本上限（字符）——超过则截断，防止一个巨型 JSON 撑爆 DB 与 SSE</summary>
    public const Int32 MaxEventTextLength = 64 * 1024;

    private readonly AgentProfile? _profile;

    /// <summary>构造管道</summary>
    /// <param name="profile">所属 profile（可为 null，此时只能走文本降级）</param>
    public EventPipeline(AgentProfile? profile)
    {
        _profile = profile;
    }

    /// <summary>
    /// 逐行解析流式输出（JsonLines / Text 通用入口）。
    /// </summary>
    /// <param name="lines">文本行序列（通常是 stdout 逐行）</param>
    /// <returns>归一化事件序列</returns>
    public IEnumerable<AgentEvent> ParseLines(IEnumerable<String> lines)
    {
        var format = _profile?.OutputFormat ?? "Text";

        foreach (var raw in lines)
        {
            if (raw.IsNullOrWhiteSpace()) continue;

            if (String.Equals(format, "Text", StringComparison.OrdinalIgnoreCase))
            {
                yield return Truncate(AgentEvent.FromText(raw));
                continue;
            }

            // JsonLines / Json：尝试解析为对象
            JsonNode? node = null;
            JsonException? parseError = null;
            try
            {
                node = JsonNode.Parse(raw);
            }
            catch (JsonException ex)
            {
                parseError = ex;
            }

            if (parseError != null || node == null)
            {
                // 不是 JSON：可能 CLI 在 JSONL 流里混了日志行 —— 降级为 Text，不丢内容
                XTrace.Log.Debug("[AgentHub] 输出行非 JSON，降级为 Text 事件");
                yield return Truncate(AgentEvent.FromText(raw));
            }
            else
            {
                yield return Truncate(MapToEvent(node));
            }
        }
    }

    /// <summary>
    /// 把单个 JSON 对象按 profile 映射表转成归一化事件。
    /// 映射表形如：{ "text": "$.message.content", "type": "$.type", "tool": "$.name" }
    /// </summary>
    /// <param name="node">上游 JSON 对象</param>
    /// <returns>归一化事件</returns>
    public AgentEvent MapToEvent(JsonNode node)
    {
        var map = _profile?.OutputMapping;
        if (map == null || map.Count == 0)
        {
            // 无映射表：整体作为 Meta 事件保留（可复盘），文本尽力提取
            return new AgentEvent
            {
                Type = AgentEventTypes.Meta,
                Text = ExtractObviousText(node),
                Payload = node.DeepClone()
            };
        }

        var rawType = ReadPath(node, GetPath(map, "type"));
        var type = NormalizeType(rawType);

        var evt = new AgentEvent
        {
            Type = type,
            Text = ReadPath(node, GetPath(map, "text")) ?? ExtractObviousText(node),
            Tool = ReadPath(node, GetPath(map, "tool")),
            Payload = node.DeepClone()
        };

        // 退出码（codex / claude 等会在末条事件里带上）
        var exitCode = ReadPath(node, GetPath(map, "exitCode"));
        if (exitCode != null && Int32.TryParse(exitCode, out var code)) evt.ExitCode = code;

        return evt;
    }

    /// <summary>
    /// 把上游的类型字符串归一到 <see cref="AgentEventTypes"/> 词表。
    /// 命中不了返回 Meta —— 前端按「未知类型」原样展示，不吞。
    /// </summary>
    /// <param name="rawType">上游类型串</param>
    /// <returns>归一化类型</returns>
    public static String NormalizeType(String? rawType)
    {
        if (rawType.IsNullOrWhiteSpace()) return AgentEventTypes.Text;

        return rawType.Trim().ToLowerInvariant() switch
        {
            "text" or "message" or "assistant" or "assistant_message" or "agent_message" or "content" => AgentEventTypes.Text,
            "thought" or "reasoning" or "thinking" => AgentEventTypes.Thought,
            "tool_call" or "tool_use" or "toolcall" or "function_call" or "command" => AgentEventTypes.ToolCall,
            "tool_result" or "function_result" or "tool_output" => AgentEventTypes.ToolResult,
            "file_change" or "file_write" or "write_file" or "patch" => AgentEventTypes.FileChange,
            "diff" => AgentEventTypes.Diff,
            "permission_request" or "permission" or "request_permission" => AgentEventTypes.PermissionRequest,
            "error" or "failure" or "failed" => AgentEventTypes.Error,
            "exit" or "done" or "complete" or "completed" or "turn_complete" => AgentEventTypes.Exit,
            "meta" or "session" or "session_start" or "init" or "usage" or "system" => AgentEventTypes.Meta,
            _ => AgentEventTypes.Meta
        };
    }

    /// <summary>从映射表里取路径（键不存在返回 null）。</summary>
    private static String? GetPath(Dictionary<String, String> map, String key)
        => map.TryGetValue(key, out var v) ? v : null;

    /// <summary>
    /// 极简 JSONPath 读取：支持 <c>$.a.b.c</c> 与 <c>$.a[0].b</c>。
    /// 不引入完整 JSONPath 依赖（首版够用，复杂需求走 L2 扩展点）。
    /// </summary>
    /// <param name="node">根节点</param>
    /// <param name="path">路径（null/空 返回 null）</param>
    /// <returns>字符串值（非字符串标量转 JSON 字面量）；取不到返回 null</returns>
    public static String? ReadPath(JsonNode? node, String? path)
    {
        if (node == null || path.IsNullOrWhiteSpace()) return null;

        var p = path.Trim();
        if (p.StartsWith("$")) p = p[1..];

        var current = node;
        foreach (var segment in SplitPath(p))
        {
            if (current == null) return null;

            if (Int32.TryParse(segment, out var index))
            {
                if (current is not JsonArray arr || index < 0 || index >= arr.Count) return null;
                current = arr[index];
            }
            else
            {
                if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out var next)) return null;
                current = next;
            }
        }

        if (current == null) return null;

        // 标量 → 字符串；复杂节点 → JSON 字面量
        return current is JsonValue val
            ? val.TryGetValue<String>(out var s) ? s : val.ToJsonString().Trim('"')
            : current.ToJsonString();
    }

    /// <summary>切分路径段：a.b[0].c → [a, b, 0, c]</summary>
    private static IEnumerable<String> SplitPath(String path)
    {
        var buffer = new System.Text.StringBuilder();

        foreach (var ch in path)
        {
            switch (ch)
            {
                case '.':
                    if (buffer.Length > 0) { yield return buffer.ToString(); buffer.Clear(); }
                    break;

                case '[':
                    if (buffer.Length > 0) { yield return buffer.ToString(); buffer.Clear(); }
                    break;

                case ']':
                    if (buffer.Length > 0) { yield return buffer.ToString(); buffer.Clear(); }
                    break;

                default:
                    buffer.Append(ch);
                    break;
            }
        }

        if (buffer.Length > 0) yield return buffer.ToString();
    }

    /// <summary>
    /// 映射表没配 text 路径时的兜底：猜最可能的文本字段。
    /// 只认常见键名，猜不到返回 null（宁可少显示，不拼错内容）。
    /// </summary>
    private static String? ExtractObviousText(JsonNode node)
    {
        if (node is JsonValue val)
        {
            return val.TryGetValue<String>(out var s) ? s : val.ToJsonString().Trim('"');
        }

        if (node is not JsonObject obj) return null;

        foreach (var key in new[] { "text", "content", "message", "output", "result", "delta" })
        {
            if (!obj.TryGetPropertyValue(key, out var child) || child == null) continue;

            if (child is JsonValue cv && cv.TryGetValue<String>(out var cs)) return cs;
            if (child is JsonObject co)
            {
                foreach (var sub in new[] { "text", "content" })
                {
                    if (co.TryGetPropertyValue(sub, out var sc) && sc is JsonValue sv && sv.TryGetValue<String>(out var ss))
                        return ss;
                }
            }
        }

        return null;
    }

    /// <summary>超长文本截断（置 Truncated 让前端显式提示）。</summary>
    private static AgentEvent Truncate(AgentEvent evt)
    {
        if (evt.Text != null && evt.Text.Length > MaxEventTextLength)
        {
            evt.Text = evt.Text[..MaxEventTextLength];
            evt.Truncated = true;
        }

        return evt;
    }
}
