using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 从工具调用轨迹提取结构化文件清单（B2① 复盘修复 + B2② 取证复用）。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么不能只靠正则</b>：Run 步骤轨迹的落库形态是「轨迹列表序列化后再套一层 JSON 字符串」——
/// <see cref="RunOrchestratorService"/> 的 <c>MarkStepCompleted</c> 用
/// <c>JsonSerializer.Serialize(result.ToolCalls)</c> 写 <c>AgentStepRun.ToolCallsJson</c>，
/// 而每个 <see cref="StepToolCallTrace.ArgumentsSummary"/> 存的是原始参数 字符串
/// （截 200 字符）。双重编码后内层引号全部转义成 <c>\"</c>，肉眼看到的
/// <c>"path\\":</c> 在字节层面是 <c>path\\\":</c>，旧正则 <c>path["']?\s*[:=]</c>
/// 永远打不中 ⇒ 文件清单恒空（TODO 233 的根因之一）。
/// </para>
/// <para>
/// 提取顺序：① 结构化反序列化 <c>List&lt;StepToolCallTrace&gt;</c> → 逐轨迹把
/// <c>ArgumentsSummary</c> 当 JSON 解析取 path/file_path（主路径，可靠）；
/// ② 主路径拿不到时，把整体串先反转义再跑宽松正则（兜底旧数据/截断残缺形态）。
/// 仅统计「写类工具」（write/create/patch/edit/delete），读类工具不计入改动清单。
/// </para>
/// </remarks>
public static partial class ToolCallFileExtractor
{
    /// <summary>写类工具名（路径参数指向被写入/创建/打补丁/删除的文件）。</summary>
    private static readonly HashSet<string> WriteToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "write_file", "create_file", "patch", "edit_file", "delete", "move"
    };

    /// <summary>参数对象里文件路径键名（按优先级）。</summary>
    private static readonly string[] PathKeys = ["path", "file_path", "filePath", "target_path", "targetPath"];

    /// <summary>兜底正则：反转义后的 <c>path: "..."</c> / <c>"path":"..."</c> 形态。</summary>
    [GeneratedRegex("""["']?path["']?\s*[:=]\s*["'](?<path>[^"'\r\n]+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex PathPattern();

    /// <summary>
    /// 从单次工具调用（tool/call 日志事件）提取文件路径；非写类工具返回空列表。
    /// 开放任务回合收据（run/receipt）取证入口。
    /// </summary>
    public static IReadOnlyList<string> ExtractFromCall(string? toolName, string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(toolName) || !WriteToolNames.Contains(toolName)) return [];
        return ExtractFromArguments(argsJson)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 写类工具的变更标记（run/receipt 与结构化清单共用）：
    /// create/write → A（新增或整写）；patch/edit/move → M；delete → D；非写类工具 → null（不计入改动）。
    /// </summary>
    public static string? ChangeOf(string? toolName) => toolName?.Trim().ToLowerInvariant() switch
    {
        "delete" => "D",
        "patch" or "edit_file" => "M",
        "write_file" or "create_file" or "move" => "A",
        _ => null
    };

    /// <summary>
    /// 从 ToolCallsJson（序列化的轨迹列表）提取本步涉及的文件路径（去重，保持出现序）。
    /// 空/解析失败返回空列表，不抛异常（取证失败不得打断执行流）。
    /// </summary>
    public static IReadOnlyList<string> ExtractFilePaths(string? toolCallsJson)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(toolCallsJson)) return result;

        try
        {
            var traces = System.Text.Json.JsonSerializer.Deserialize<List<StepToolCallTrace>>(toolCallsJson);
            if (traces != null)
            {
                foreach (var trace in traces)
                {
                    if (trace.Name == null || !WriteToolNames.Contains(trace.Name)) continue;
                    result.AddRange(ExtractFromArguments(trace.ArgumentsSummary));
                }
            }
        }
        catch (Exception ex)
        {
            // 轨迹不是合法的轨迹列表 JSON（旧数据/残缺）：降级正则兜底
            XTrace.Log.Warn("[ToolCallFileExtractor] 轨迹结构化解析失败，降级正则: {0}", ex.Message);
        }

        if (result.Count == 0)
        {
            result.AddRange(ExtractByRegex(toolCallsJson));
        }

        return result
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>把单条 ArgumentsSummary（原始参数 JSON，可能被截断）解析出文件路径。</summary>
    private static List<string> ExtractFromArguments(string? argumentsSummary)
    {
        var paths = new List<string>();
        if (string.IsNullOrWhiteSpace(argumentsSummary)) return paths;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(argumentsSummary);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return paths;

            foreach (var key in PathKeys)
            {
                if (doc.RootElement.TryGetProperty(key, out var value)
                    && value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var path = value.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        paths.Add(path);
                    }

                    return paths; // 首个命中的路径键即认（按 PathKeys 优先级）
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // 参数被截断成非法 JSON：正则兜底（反转义后匹配 path 键）
            foreach (Match match in PathPattern().Matches(Unescape(argumentsSummary)))
            {
                paths.Add(match.Groups["path"].Value);
            }
        }

        return paths;
    }

    /// <summary>整体串反转义后的宽松正则（最后兜底）。</summary>
    private static List<string> ExtractByRegex(string toolCallsJson)
    {
        var paths = new List<string>();
        foreach (Match match in PathPattern().Matches(Unescape(toolCallsJson)))
        {
            paths.Add(match.Groups["path"].Value);
        }

        return paths;
    }

    /// <summary>JSON 转义反转义（双编码串 → 可读串；非法转义序列原样保留）。</summary>
    private static string Unescape(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\/", "/");
    }
}
