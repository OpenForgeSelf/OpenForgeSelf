using System.Text;

namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>
/// 回粘文本格式化（PILOT-053 02-spec FR-4）：纯函数。
/// 两种模式都以同一对标记包住，**被拒/未识别/未解析的条目也必须进来**（FR-4.3、图 5 缺口⑩）——
/// 否则 AI 收到"什么都没有"会原地重复同一次调用，这个评估工具就失效了。
/// </summary>
public static class ResultFormatter
{
    public const string ModeJson = "json";
    public const string ModePlain = "plain";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Build(IReadOnlyList<ToolResult> results, IReadOnlyList<UnknownCall> unknown,
        IReadOnlyList<UnparsedFragment> unparsed, string mode) =>
        mode == ModePlain
            ? BuildPlain(results, unknown, unparsed)
            : BuildJson(results, unknown, unparsed);

    /// <summary>单行 JSON 夹在标记之间 ⇒ 标记之间可直接 <c>JsonDocument.Parse</c>（AC9）。</summary>
    public static string BuildJson(IReadOnlyList<ToolResult> results, IReadOnlyList<UnknownCall> unknown,
        IReadOnlyList<UnparsedFragment> unparsed)
    {
        var envelope = new JsonObject
        {
            ["tool_bridge_results"] = new JsonArray(results.Select(ToJson).ToArray()),
            ["unrecognized"] = new JsonArray(unknown.Select(u => (JsonNode)new JsonObject
            {
                ["raw_name"] = u.RawName,
                ["reason"] = u.Reason,
                ["suggestion"] = u.Suggestion,
                ["fragment"] = u.Fragment
            }).ToArray()),
            ["unparsed"] = new JsonArray(unparsed.Select(p => (JsonNode)new JsonObject
            {
                ["fragment"] = p.Fragment,
                ["reason"] = p.Reason
            }).ToArray())
        };

        var sb = new StringBuilder();
        sb.Append(PromptBuilder.ResultMarker).Append(' ').Append(ToolSpec.SpecVersion).Append(" mode=json\n");
        // 不转义非 ASCII：中文内容原样回给 AI 才可读（JSON 依然合法）。
        sb.Append(envelope.ToJsonString(JsonOptions));
        sb.Append('\n').Append(PromptBuilder.ResultEndMarker);
        return sb.ToString();
    }

    /// <summary>
    /// 可读块模式：内容/stdout 原文单独成段，不缩进、不转义（AC9 plain 判据）。
    /// 行尾一律显式 <c>\n</c>——不用 <c>AppendLine()</c>（Windows 下它是 CRLF，
    /// 会把原文段的字节序列污染成"看起来一样、实际多了 \r"，AC9 的逐字节相等就守不住了）。
    /// </summary>
    public static string BuildPlain(IReadOnlyList<ToolResult> results, IReadOnlyList<UnknownCall> unknown,
        IReadOnlyList<UnparsedFragment> unparsed)
    {
        var sb = new StringBuilder();
        void Line(string text = "") => sb.Append(text).Append('\n');

        Line($"{PromptBuilder.ResultMarker} {ToolSpec.SpecVersion} mode=plain");

        foreach (var r in results)
        {
            Line(r.Ok ? $"- {r.Tool} ok" : $"- {r.Tool} 未执行 error={r.Error}");
            if (!string.IsNullOrEmpty(r.Reason)) Line($"  reason: {r.Reason}");
            Line($"  durationMs: {r.DurationMs}");

            if (r.Truncated)
            {
                Line(r.OriginalBytes.HasValue
                    ? $"  truncated: true（原长 {r.OriginalBytes} 字节）"
                    : "  truncated: true");
            }

            if (r.Result is JsonObject obj)
            {
                AppendScalar(sb, "path", obj);
                AppendScalar(sb, "cwd", obj);
                AppendScalar(sb, "exitCode", obj);
                AppendScalar(sb, "bytes", obj);
                AppendScalar(sb, "bytesWritten", obj);
                AppendScalar(sb, "count", obj);
                AppendRaw(sb, "content", obj);
                AppendRaw(sb, "stdout", obj);
                AppendRaw(sb, "stderr", obj);
                AppendStructured(sb, "entries", obj);
                AppendStructured(sb, "truncatedNote", obj);
            }
        }

        foreach (var u in unknown)
        {
            Line($"- 未识别的工具名 {u.RawName} error=unknown_tool");
            Line($"  reason: {u.Reason}");
            if (!string.IsNullOrEmpty(u.Suggestion)) Line($"  suggestion: {u.Suggestion}");
        }

        foreach (var p in unparsed)
        {
            Line("- 未解析片段");
            Line($"  reason: {p.Reason}");
            if (!string.IsNullOrEmpty(p.Fragment)) Line($"  fragment: {p.Fragment}");
        }

        if (results.Count == 0 && unknown.Count == 0 && unparsed.Count == 0)
        {
            Line("- 本轮没有任何调用（粘贴内容为空或未包含结构化调用）");
        }

        sb.Append(PromptBuilder.ResultEndMarker);
        return sb.ToString();
    }

    private static JsonNode ToJson(ToolResult r) => new JsonObject
    {
        ["tool"] = r.Tool,
        ["raw_name"] = r.RawName,
        ["ok"] = r.Ok,
        ["args"] = r.Args.Count == 0 ? null : new JsonObject(r.Args.ToDictionary(kv => kv.Key, kv => (JsonNode?)kv.Value.DeepClone())),
        ["result"] = r.Result?.DeepClone(),
        ["error"] = r.Error,
        ["reason"] = r.Reason,
        ["truncated"] = r.Truncated,
        ["originalBytes"] = r.OriginalBytes,
        ["durationMs"] = r.DurationMs
    };

    private static void AppendScalar(StringBuilder sb, string key, JsonObject obj)
    {
        if (obj.TryGetPropertyValue(key, out var node) && node != null && node.GetValueKind() != JsonValueKind.Null)
        {
            sb.Append($"  {key}: {node.ToJsonString(new JsonSerializerOptions { WriteIndented = false })}").Append('\n');
        }
    }

    private static void AppendRaw(StringBuilder sb, string key, JsonObject obj)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node == null || node.GetValueKind() != JsonValueKind.String) return;
        var value = node.GetValue<string>();
        sb.Append($"  ---- {key} ----").Append('\n');
        sb.Append(value).Append('\n'); // 原文一个字节都不改，尾后这个换行是分隔符本身
        sb.Append($"  ---- end {key} ----").Append('\n');
    }

    private static void AppendStructured(StringBuilder sb, string key, JsonObject obj)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node == null || node.GetValueKind() == JsonValueKind.Null) return;
        if (node is JsonArray array)
        {
            sb.Append($"  {key}:").Append('\n');
            foreach (var item in array)
            {
                sb.Append($"    {item?.ToJsonString()}").Append('\n');
            }
            return;
        }
        sb.Append($"  {key}: {node.ToJsonString()}").Append('\n');
    }
}
