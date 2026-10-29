using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 工具搜索（输入2 · 2026-10-13；2026-10-28 工具归属迁移自 AIAgent → mcp-center，裸名为平台保留名）：
/// 按关键词对「工具名 + 描述」打分检索宿主注册表，
/// 返回 top-N 命中（名称/说明/插件来源/匹配面/得分）。定位 = 万能工具的「先搜索、搜不到再列举」入口：
/// 模型侧先 search_tools 找目标工具名，命中后按名调用；search 无果才退回 list_tools 全量枚举。
/// 打分规则：<c>ToolSearchScorer.Score</c>（名称全词=5、名称包含=3、名称前缀+1、描述包含=1；0 分不返回）。
/// 该打分助手同时被 UniversalTool 的 unknown 封套复用（猜错工具名时直接给 top 建议）。
/// </summary>
/// <remarks>
/// 与 ListToolsToolFunction（本插件）同范式：IToolFunctionExtension + 运行期软依赖
/// ctx.Get&lt;IToolRegistry&gt;()（插件装配期 Get 不到，宿主 seed 晚于插件 Apply）。
/// 本工具只检索、不执行业务；结果来自宿主 IToolRegistry.GetAllTools() 实时快照。
/// </remarks>
public class SearchToolsToolFunction : IToolFunctionExtension
{
    /// <summary>默认返回条数（top-N）。</summary>
    public const int DefaultLimit = 8;

    /// <summary>limit 上限（防止一次带回大半个注册表，枚举是 list_tools 的职责）。</summary>
    public const int MaxLimit = 20;

    private readonly IContext _ctx;

    public string Id => "mcp-center.search_tools";
    public string Name => "search_tools";
    public string PluginId => "mcp-center";

    public string Description =>
        "按关键词搜索已注册工具（对工具名与说明打分检索，返回最匹配的 top-N 及其参数形态说明）。" +
        "调用任何工具前不确定工具名时先用本工具检索；检索无果再用 list_tools 全量枚举。";
    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "keyword": {
                    "type": "string",
                    "description": "搜索关键词：能力词或工具名词根（如 file/记忆/工作流/终端/calculate）"
                },
                "limit": {
                    "type": "integer",
                    "description": "可选：最多返回条数（默认 8，上限 20）"
                }
            },
            "required": ["keyword"]
        }
        """;

    public SearchToolsToolFunction(IContext ctx)
    {
        _ctx = ctx;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var root = doc.RootElement;

            var keyword = root.TryGetProperty("keyword", out var k) && k.ValueKind == JsonValueKind.String
                ? (k.GetString() ?? string.Empty).Trim()
                : string.Empty;

            // 空 keyword 是使用错误而非「要看全部」：全量列举走 list_tools，本工具不静默兜底（Success Criteria 2）
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "keyword 必填：请给出要搜索的工具能力关键词（如 file/记忆/工作流/端端）",
                    hint = "示例：search_tools {\"keyword\":\"file\"}。要看全部工具用 list_tools。"
                }));
            }

            var limit = DefaultLimit;
            if (root.TryGetProperty("limit", out var l) && l.TryGetInt32(out var parsed))
            {
                limit = Math.Clamp(parsed, 1, MaxLimit);
            }

            var registry = _ctx.Get<IToolRegistry>()
                ?? throw new InvalidOperationException("IToolRegistry 未注册到插件上下文");

            var matches = ToolSearchScorer.Search(registry.GetAllTools(), keyword, limit);
            var total = matches.Count;

            XTrace.Log.Info("[McpCenter] search_tools \"{0}\" → {1} 条（limit={2}）", keyword, total, limit);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                keyword,
                total,
                tools = matches.Select(m => new
                {
                    name = m.Tool.Name,
                    description = m.Tool.Description,
                    pluginId = m.Tool.PluginId,
                    score = m.Score,
                    matchedIn = m.MatchedIn.ToArray()
                })
            }));
        }
        catch (JsonException jex)
        {
            XTrace.Log.Error("[McpCenter] search_tools 入参解析失败: {0}", jex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                error = "入参必须是 JSON：{keyword: 关键词, limit?: 条数}，" + jex.Message
            }));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] search_tools 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }
}

/// <summary>
/// 工具检索打分助手（internal：UniversalTool unknown 封套复用，避免模型搜错名后只能全量枚举）。
/// 打分：名称全词等于=5、名称包含=3（前缀命中再+1）、描述包含=1；0 分不返回。
/// 排序：score 降序 → 名称稳定序（OrdinalIgnoreCase，保证同分输出确定性，测试可锚定）。
/// </summary>
internal static class ToolSearchScorer
{
    public sealed record Match(IToolFunctionExtension Tool, int Score, List<string> MatchedIn);

    public static List<Match> Search(IEnumerable<IToolFunctionExtension> tools, string keyword, int limit)
    {
        var kw = (keyword ?? string.Empty).Trim();
        var results = new List<Match>();
        if (kw.Length == 0) return results;

        foreach (var tool in tools)
        {
            var score = Score(tool.Name, tool.Description, kw, out var matchedIn);
            if (score > 0)
            {
                results.Add(new Match(tool, score, matchedIn));
            }
        }

        return results
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, int.MaxValue))
            .ToList();
    }

    /// <summary>单工具打分；matchedIn 记录命中面（"name"/"description"，供模型判断该信不该信）。</summary>
    public static int Score(string? name, string? description, string keyword, out List<string> matchedIn)
    {
        matchedIn = new List<string>();
        var score = 0;

        if (!string.IsNullOrWhiteSpace(name))
        {
            if (name.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
                matchedIn.Add("name");
            }
            else
            {
                var idx = name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    score += 3;
                    if (idx == 0) score += 1;   // 前缀命中更可能是目标（read_file 之于 "read"）
                    matchedIn.Add("name");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(description) &&
            description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
            matchedIn.Add("description");
        }

        return score;
    }
}
