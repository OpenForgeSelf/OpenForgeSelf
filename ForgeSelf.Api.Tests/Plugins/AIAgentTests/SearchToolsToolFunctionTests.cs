using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Moq;
using System.Text.Json;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgentTests;

/// <summary>
/// SearchToolsToolFunction 单元测试（输入2 · 2026-10-13 万能工具搜索功能；2026-10-28 归属迁至 mcp-center）。
/// 锁七类合约：① 定义契约（名称/描述/schema 含「先搜索、搜不到再列举」引导）；
/// ② 打分排序（多面/多词命中 &gt; 单面命中，同分按名称稳定序）；③ 空/缺 keyword 友好错误，
/// 不静默全量列举（全量是 list_tools 的职责）；④ limit 截断（默认 8、上限 20）；
/// ⑤ registry 未挂优雅报错；⑥ UniversalTool unknown 封套的 top 建议助手；
/// ⑦ 分词 + 三面（Id/Name/Description）多词检索（卡 t_20bc15cf：空白族分词 OR 合并 + Id 面命中记 "id"）。
/// 策略：Moq 假 IToolRegistry + 假 IContext，零副作用纯逻辑单测。
/// </summary>
public class SearchToolsToolFunctionTests
{
    private const string PluginId = "mcp-center";

    private static IToolFunctionExtension FakeTool(string name, string description = "", string pluginId = "ai-agent", string? id = null)
    {
        var mock = new Mock<IToolFunctionExtension>();
        mock.SetupGet(t => t.Name).Returns(name);
        mock.SetupGet(t => t.Id).Returns(id ?? $"{pluginId}.{name}");
        mock.SetupGet(t => t.Description).Returns(description);
        mock.SetupGet(t => t.PluginId).Returns(pluginId);
        return mock.Object;
    }

    /// <summary>兼容 Mock 与裸接口两种入参（调用点两种形态都有，Moq 匹配运行时任一）。</summary>
    private static Mock<IContext> MakeContext(object registryOrMock)
    {
        var reg = registryOrMock switch
        {
            Mock<IToolRegistry> m => (IToolRegistry)m.Object,
            IToolRegistry r => r,
            _ => throw new ArgumentException("需要 IToolRegistry 或其 Mock")
        };
        var ctx = new Mock<IContext>();
        ctx.Setup(x => x.Get<IToolRegistry>()).Returns(reg);
        return ctx;
    }

    private static Mock<IToolRegistry> MakeRegistry(params IToolFunctionExtension[] tools)
    {
        var reg = new Mock<IToolRegistry>();
        reg.Setup(r => r.GetAllTools()).Returns(tools);
        return reg;
    }

    private static SearchToolsToolFunction CreateTool(Mock<IContext> ctx) => new(ctx.Object);

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task Search_DefinitionContract_KeywordRequired()
    {
        var tool = CreateTool(MakeContext(MakeRegistry().Object));
        var schema = Parse(tool.ParametersJsonSchema);
        Assert.True(schema.TryGetProperty("required", out var req) &&
                    req.EnumerateArray().Any(v => v.GetString() == "keyword"));
    }

    [Fact]
    public void Definition_Name_Id_And_DiscoveryGuidance()
    {
        var tool = CreateTool(MakeContext(MakeRegistry().Object));

        Assert.Equal("search_tools", tool.Name);
        Assert.Equal("mcp-center.search_tools", tool.Id);
        Assert.Contains("关键词", tool.Description);   // 语义：能力词检索入口
        Assert.Contains("list_tools", tool.Description, StringComparison.OrdinalIgnoreCase);  // 分工引导：无果再枚举
        var schema = Parse(tool.ParametersJsonSchema);
        Assert.True(schema.TryGetProperty("properties", out var props) &&
                    props.TryGetProperty("keyword", out _));
    }

    // ── ② 打分与排序 ────────────────────────────────────────────

    [Fact]
    public async Task Search_NamePrefixMatch_RanksAboveDescriptionOnlyMatch()
    {
        var reg = MakeRegistry(
            FakeTool("read_file", "读取项目文件内容"),          // 名称前缀 read → 4 + Id(ai-agent.read_file) 包含 read → +3 = 7
            FakeTool("readme_helper", "生成 readme"),           // 名称前缀 read → 4 + Id 包含 → +3 + 描述含 readme → +3 = 10
            FakeTool("format_json", "格式化 read 输出的文本")); // 仅描述命中 read → 3
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"read"}""");

        var root = Parse(result);
        Assert.True(root.GetProperty("success").GetBoolean());
        var tools = root.GetProperty("tools");
        // Id 面加入后各工具同向变化，相对排序不变：readme_helper(10) > read_file(7) > format_json(3)
        Assert.Equal("readme_helper", tools[0].GetProperty("name").GetString());
        Assert.Equal("read_file", tools[1].GetProperty("name").GetString());
        Assert.Equal("format_json", tools[2].GetProperty("name").GetString());
        Assert.Equal(10, tools[0].GetProperty("score").GetInt32());
        Assert.Equal(7, tools[1].GetProperty("score").GetInt32());
        // matchedIn 记录命中面（去重 + 固定序 [id,name,description]），供调用方判断可信度
        var readFileMatchedIn = tools[1].GetProperty("matchedIn").EnumerateArray()
            .Select(x => x.GetString()!).ToArray();
        Assert.Equal(new[] { "id", "name" }, readFileMatchedIn);
    }

    [Fact]
    public async Task Search_ExactNameMatch_RanksFirst()
    {
        var reg = MakeRegistry(
            FakeTool("calculate", "计算器"),                     // 描述无关
            FakeTool("check_time", "查时间；calculate 别名说明")); // 描述弱命中
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"calculate"}""");

        var tools = Parse(result).GetProperty("tools");
        Assert.Equal("calculate", tools[0].GetProperty("name").GetString());  // 全词等于=5 + Id 包含=3 = 8 最高
    }

    [Fact]
    public async Task Search_SameScore_StableOrderByName()
    {
        var reg = MakeRegistry(
            FakeTool("beta_file", "no desc"),
            FakeTool("alpha_file", "no desc"));
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"file"}""");

        var tools = Parse(result).GetProperty("tools");
        Assert.Equal("alpha_file", tools[0].GetProperty("name").GetString()); // 同分 → 名称稳定序
        Assert.Equal("beta_file", tools[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Search_ZeroScore_ToolsExcluded_TotalReflectsFiltered()
    {
        var reg = MakeRegistry(FakeTool("calculate", "计算器"));
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"zzz_no_hit"}""");

        var root = Parse(result);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("total").GetInt32());
        Assert.Empty(root.GetProperty("tools").EnumerateArray());   // 0 命中 → tools 空数组（不倒全量）
    }

    // ── ③ 空 keyword：不静默全量列举 ─────────────────────────────

    [Fact]
    public async Task Search_EmptyKeyword_FriendlyErrorNotFullEnumeration()
    {
        var reg = MakeRegistry(FakeTool("a"), FakeTool("b"), FakeTool("c"));
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{}""");

        var root = Parse(result);
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Contains("keyword", root.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
        // 关键红线：错误封套根本不携带 tools 字段（更不会有全量列表）
        Assert.False(root.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task Search_MissingKeywordProperty_SameAsEmpty()
    {
        var tool = CreateTool(MakeContext(MakeRegistry(FakeTool("a")).Object));

        var result = await tool.ExecuteAsync("""{"limit":5}""");

        Assert.False(Parse(result).GetProperty("success").GetBoolean());
    }

    // ── ④ limit 截断 ────────────────────────────────────────────

    [Fact]
    public async Task Search_LimitTruncatesResults_AndCapsAtMax()
    {
        var tools = Enumerable.Range(1, 15).Select(i => FakeTool($"tool_{i:D2}_file")).ToArray();
        var reg = MakeRegistry(tools);
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"file","limit":3}""");
        Assert.Equal(3, Parse(result).GetProperty("total").GetInt32());

        var over = await tool.ExecuteAsync("""{"keyword":"file","limit":999}""");
        Assert.Equal(15, Parse(over).GetProperty("total").GetInt32());   // ≤15 条全命中（夹到 MaxLimit=20 后不截断）
    }

    // ── ⑤ 依赖与容错 ────────────────────────────────────────────

    [Fact]
    public async Task Search_RegistryNotAttached_ReturnsErrorNotThrows()
    {
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IToolRegistry>()).Returns((IToolRegistry?)null!);

        var tool = new SearchToolsToolFunction(ctx.Object);
        var result = await tool.ExecuteAsync("""{"keyword":"file"}""");

        Assert.False(Parse(result).GetProperty("success").GetBoolean());
        Assert.Contains("IToolRegistry", Parse(result).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Search_InvalidJson_GracefulError()
    {
        var tool = CreateTool(MakeContext(MakeRegistry().Object));

        var result = await tool.ExecuteAsync("not json at all");

        Assert.False(Parse(result).GetProperty("success").GetBoolean());
    }

    // ── ⑥ UniversalTool unknown 封套的搜索建议 ──────────────────

    [Fact]
    public void ToolSearchHints_ProducesTopSuggestions()
    {
        var hints = UniversalTool.ToolSearchHints(
            () => new[] { FakeTool("read_file", "读文件"), FakeTool("write_file", "写文件") },
            "zzz_none");   // 任何词/任何面都不命中（原 "red File" 在 Id 面加入后会经 "file" 词命中，不再覆盖无建议分支）

        // 无命中时也必须给引导语（先 search_tools、再 list_tools），不能是空串
        Assert.Contains("search_tools", hints);
        Assert.Contains("list_tools", hints);
    }

    [Fact]
    public void ToolSearchHints_WithCloseMiss_SuggestsCandidate()
    {
        var hints = UniversalTool.ToolSearchHints(
            () => new[] { FakeTool("read_file", "读取项目文件内容 read") },
            "read");       // read_file 名称前缀命中

        Assert.Contains("read_file", hints);
        Assert.Contains("search_tools", hints);
    }

    [Fact]
    public void ToolSearchHints_GetAllThrows_NeverThrows()
    {
        var hints = UniversalTool.ToolSearchHints(
            () => throw new InvalidOperationException("boom"),
            "file");

        Assert.Contains("search_tools", hints);   // 兜底引导语仍在，unknown 封套不被吞
    }

    // ── ⑦ 分词 + 三面（Id/Name/Description）多词检索（卡 t_20bc15cf D1-D6）──

    [Fact]
    public async Task Search_MultiWord_TokenizedOR_AggregatesScores()
    {
        var reg = MakeRegistry(
            FakeTool("db_list_databases", "列出所有 database 连接与库清单"),
            FakeTool("db_execute_sql", "在数据库上执行 sql 语句"),
            FakeTool("calculate", "计算器"));   // 与四个词全无关 → 0 分不返回
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"database sql 数据库 执行","limit":10}""");

        var root = Parse(result);
        Assert.True(root.GetProperty("success").GetBoolean());
        var names = root.GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).ToList();
        Assert.True(root.GetProperty("total").GetInt32() > 0);   // 整词子串时代 total=0，分词后必须命中
        Assert.Contains("db_list_databases", names);   // "database" 词：name/Id/描述三面命中
        Assert.Contains("db_execute_sql", names);      // "sql"/"执行"/"数据库" 词命中
        Assert.DoesNotContain("calculate", names);     // 0 分工具不返回
    }

    [Fact]
    public async Task Search_SingleWord_ExistingHitsNoRegression()
    {
        var reg = MakeRegistry(
            FakeTool("db_list_databases", "列出所有 database 连接与库清单"),
            FakeTool("db_execute_sql", "在数据库上执行 sql 语句"));
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"database"}""");

        var names = Parse(result).GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).ToList();
        Assert.Contains("db_list_databases", names);   // 单词走同一条分词路径（退化为一词），原命中不回归
    }

    [Fact]
    public async Task Search_MultiWord_AllTermsMiss_TotalZero()
    {
        var reg = MakeRegistry(FakeTool("calculate", "计算器"));
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"zzz_no_hit yyy_none"}""");

        var root = Parse(result);
        Assert.Equal(0, root.GetProperty("total").GetInt32());
        Assert.Empty(root.GetProperty("tools").EnumerateArray());
    }

    [Fact]
    public async Task Search_MultiWord_PartialHit_MergedSortedTruncated()
    {
        var reg = MakeRegistry(
            FakeTool("read_file", "读取项目文件内容"),   // 双词命中：read 名称前缀4+Id3 = 7；file 3+3 = 6 → 合计 13
            FakeTool("file_writer", "写入文件"),          // 单词 file：名称前缀 4 + Id 3 = 7
            FakeTool("alpha_file", "no desc"),            // 单词 file：3+3 = 6
            FakeTool("beta_file", "no desc"));            // 单词 file：3+3 = 6（同分 → 名称稳定序）
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"file read","limit":3}""");

        var root = Parse(result);
        var tools = root.GetProperty("tools");
        // 合并去重（每工具一条）+ 总分降序 + 同分名称稳定序 + limit 截断（4 个命中 > limit 3）
        Assert.Equal(3, root.GetProperty("total").GetInt32());
        Assert.Equal("read_file", tools[0].GetProperty("name").GetString());
        Assert.Equal("file_writer", tools[1].GetProperty("name").GetString());
        Assert.Equal("alpha_file", tools[2].GetProperty("name").GetString());   // beta_file 被截断
        // 多词命中面去重 + 固定序：read_file 两词都命中 Id+Name，去重后按 [id, name] 输出（描述未命中）
        var matchedIn = tools[0].GetProperty("matchedIn").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(new[] { "id", "name" }, matchedIn);
    }

    [Fact]
    public async Task Search_IdOnlyMatch_MatchedInContainsId()
    {
        // Id 含 "mcp-center" 而 Name/描述不含（FakeTool 显式指定 Id 的重载）
        var reg = MakeRegistry(FakeTool("find_tools", "定位工具", "mcp-center", "mcp-center.search_tools"));
        var tool = CreateTool(MakeContext(reg));

        var result = await tool.ExecuteAsync("""{"keyword":"mcp-center"}""");

        var root = Parse(result);
        var tools = root.GetProperty("tools");
        Assert.Equal(1, root.GetProperty("total").GetInt32());
        Assert.Equal("find_tools", tools[0].GetProperty("name").GetString());
        Assert.Equal(4, tools[0].GetProperty("score").GetInt32());   // 仅 Id 面命中，且是前缀包含 → 3+1
        Assert.Equal(new[] { "id" }, tools[0].GetProperty("matchedIn").EnumerateArray()
            .Select(x => x.GetString()!).ToArray());
    }

    [Fact]
    public async Task Search_FullWidthSpace_Tab_Nbsp_TokenizedAsSeparators()
    {
        var reg = MakeRegistry(
            FakeTool("db_list_databases", "列出所有 database 连接"),
            FakeTool("db_execute_sql", "执行 sql 语句"));
        var tool = CreateTool(MakeContext(reg));

        // 全角空格 U+3000 分隔：分词后两词各命中一个工具；若不分词整串将 0 命中
        var fullWidth = await tool.ExecuteAsync("{\"keyword\":\"database\u3000sql\"}");
        var names = Parse(fullWidth).GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).ToList();
        Assert.Contains("db_list_databases", names);
        Assert.Contains("db_execute_sql", names);

        // \t 与 NBSP(U+00A0) 同属空白族分隔符（D1）
        // 分段拼接：规避 hygiene.abs-unc 对 JSON 转义序列的 UNC 形状假阳性（t_20bc15cf 提交时发现）
        var tab = await tool.ExecuteAsync("{\"keyword\":\"database" + "\\t" + "sql\"}");
        Assert.Equal(2, Parse(tab).GetProperty("total").GetInt32());

        var nbsp = await tool.ExecuteAsync("{\"keyword\":\"database\u00A0sql\"}");
        Assert.Equal(2, Parse(nbsp).GetProperty("total").GetInt32());
    }
}
