using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 工具搜索（输入2 · 2026-10-13；2026-10-28 工具归属迁移自 AIAgent → mcp-center，裸名为平台保留名）：
/// 按关键词对「工具标识 Id + 名称 + 描述」打分检索宿主注册表（输入2·2026-11-27 卡 t_20bc15cf：
/// keyword 按空白族分词，逐词 OR 合并——任一词任一面命中即候选，命中词越多总分越高排越前），
/// 返回 top-N 命中（名称/说明/插件来源/匹配面/得分）。定位 = 万能工具的「先搜索、搜不到再列举」入口：
/// 模型侧先 search_tools 找目标工具名，命中后按名调用；search 无果才退回 list_tools 全量枚举。
/// 打分规则：<c>ToolSearchScorer.Score</c>（Id/Name/Description 三面同权：全词等于=5、包含=3、前缀命中再+1；各词得分求和；0 分不返回）。
/// 该打分助手同时被 UniversalTool / UniversalToolForwarder 的 unknown 封套复用（猜错工具名时直接给 top 建议）。
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
        "按关键词搜索已注册工具（可多个词以空格分隔，任一词命中即候选；对工具标识 Id、名称与说明打分检索，" +
        "返回最匹配的 top-N 及其参数形态说明）。" +
        "调用任何工具前不确定工具名时先用本工具检索；检索无果再用 list_tools 全量枚举。";
    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "keyword": {
                    "type": "string",
                    "description": "搜索关键词：能力词或工具名词根（如 file/记忆/工作流/终端/calculate）；可多词以空格分隔（含全角空格），任一词命中即候选，命中词越多排序越前"
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
                }, ForgeJson.Default));
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
            }, ForgeJson.Default));
        }
        catch (JsonException jex)
        {
            XTrace.Log.Error("[McpCenter] search_tools 入参解析失败: {0}", jex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                error = "入参必须是 JSON：{keyword: 关键词, limit?: 条数}，" + jex.Message
            }, ForgeJson.Default));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] search_tools 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, ForgeJson.Default));
        }
    }
}

/// <summary>
/// 工具检索打分助手（internal：search_tools 与 UniversalTool/UniversalToolForwarder unknown 封套 hints 的唯一打分实现，
/// 历史三处打分副本已单源到此，防行为分叉；卡 t_20bc15cf D7）。
/// 打分（输入2·2026-11-27 D1-D4）：keyword 按空白族（空格/\t/全角空格\u3000/不换行空格\u00A0）分词，逐词独立打分求和
/// （OR 合并：任一词任一面命中即候选，命中词越多总分越高排越前）；Id/Name/Description 三面同权——
/// 全词等于=5、包含=3、前缀命中再+1；0 分不返回。中文无空格连写不分词（语义检索出界，D9）。
/// matchedIn：命中面去重后按固定顺序 ["id","name","description"] 输出（确定性，测试可锚定）。
/// 排序：score 降序 → 名称稳定序（OrdinalIgnoreCase，保证同分输出确定性，测试可锚定）。
/// </summary>
internal static class ToolSearchScorer
{
    /// <summary>matchedIn 输出固定面序（D4：确定性，测试可锚定）。</summary>
    private static readonly string[] FacetOrder = { "id", "name", "description" };

    public sealed record Match(IToolFunctionExtension Tool, int Score, List<string> MatchedIn);

    public static List<Match> Search(IEnumerable<IToolFunctionExtension> tools, string keyword, int limit)
    {
        var results = new List<Match>();
        var terms = Tokenize(keyword);
        if (terms.Count == 0) return results;

        foreach (var tool in tools)
        {
            var total = 0;
            var facets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var term in terms)
            {
                total += Score(tool.Id, tool.Name, tool.Description, term, out var matchedIn);
                foreach (var facet in matchedIn)
                {
                    facets.Add(facet);
                }
            }

            // 0 分不返回：任一词任一面都没命中的工具不进结果（D5）
            if (total > 0)
            {
                results.Add(new Match(tool, total, FacetOrder.Where(facets.Contains).ToList()));
            }
        }

        return results
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, int.MaxValue))
            .ToList();
    }

    /// <summary>按空白族分词（空格/\t/全角空格\u3000/不换行空格\u00A0，D1）；空串/纯空白 → 空序列（空 keyword 友好错误路径不变，D5）。</summary>
    internal static List<string> Tokenize(string keyword) =>
        (keyword ?? string.Empty)
            .Split([' ', '\t', '\u3000', '\u00A0'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    /// <summary>单工具单词打分（D3）：id/name/description 三面同权——全词等于=5、包含=3、前缀命中再+1；matchedIn 记录该词命中的面。</summary>
    public static int Score(string? id, string? name, string? description, string keyword, out List<string> matchedIn)
    {
        matchedIn = new List<string>();

        return ScoreFacet(id, keyword, "id", matchedIn)
             + ScoreFacet(name, keyword, "name", matchedIn)
             + ScoreFacet(description, keyword, "description", matchedIn);
    }

    /// <summary>单面打分（D3）：全词等于=5、包含=3、包含且位于面首（前缀）再+1；未命中返回 0 并不记面。</summary>
    private static int ScoreFacet(string? text, string keyword, string facet, List<string> matchedIn)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (text.Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            matchedIn.Add(facet);
            return 5;
        }

        var idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return 0;
        }

        matchedIn.Add(facet);
        return idx == 0 ? 4 : 3;   // 前缀命中更可能是目标（read_file 之于 "read"）
    }
}
