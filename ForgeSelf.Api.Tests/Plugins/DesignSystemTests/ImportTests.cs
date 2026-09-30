using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M13 导入（回流）判据：外部 DTCG → 库里的事实 → 再导回去逐字一致。
///
/// 三条最要紧的：
/// - **round-trip 逐字一致**（FR-I10）：这是"导入不是另一套实现"的唯一硬证据；
/// - **不搬家**：已有路径写回它原本所在的层，否则一次导入就在主题层长出一批 primitive 复制品；
/// - **反例整批不写**：成环/未知类型不许出现"写了一半"。
/// </summary>
[Collection("XCode")]
public class ImportTests : IDisposable
{
    readonly string _dbDir;
    readonly Int64 _pid;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly AuditEngine _engine;
    readonly ExportService _export;

    public ImportTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsImport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);
        XCode.DataAccessLayer.DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        XCode.EntityFactory.InitConnection(DesignSystemTables.ConnName);

        DesignProject.Meta.Cache.Expire = 0;
        DesignTheme.Meta.Cache.Expire = 0;
        DesignToken.Meta.Cache.Expire = 0;
        DesignShadowLayer.Meta.Cache.Expire = 0;
        DesignAsset.Meta.Cache.Expire = 0;
        DesignScreen.Meta.Cache.Expire = 0;
        DesignFontFace.Meta.Cache.Expire = 0;
        DesignComponent.Meta.Cache.Expire = 0;
        DesignComponentVariant.Meta.Cache.Expire = 0;
        DesignIcon.Meta.Cache.Expire = 0;
        DesignAudit.Meta.Cache.Expire = 0;

        _engine = new AuditEngine(_tokens, _projects, _audits);
        _export = new ExportService(_tokens, _projects, _catalog);
        var p = _projects.Create(new ProjectInput { Code = $"imp-{Guid.NewGuid():N}"[..24], Name = "导入验证系统" });
        _pid = p.Id;
        var generated = DesignGenerator.ApplyToProject(_tokens, _projects, _pid,
            new GenerationRequest { SeedColor = "#7c3aed", Themes = ["light", "dark"] }, false);
        DesignGenerator.SeedBrandCatalog(_catalog, _pid, generated);
        DesignGenerator.SeedComponentCatalog(_catalog, _pid, generated);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>与控制器同一条链：查表 → 解析。测试里自己算 seed，才能验 FR-I6 说的就是"这份字节的哈希"。</summary>
    ImportPlan Parse(String json, String? theme = null, Boolean overwrite = false)
    {
        var themeId = _projects.ResolveThemeId(_pid, theme);
        var (byPath, keys) = _tokens.ImportLookups(_pid, themeId);
        using var doc = JsonDocument.Parse(json);
        return DtcgImporter.Parse(doc.RootElement.Clone(), themeId, byPath, keys, SeedOf(json), overwrite);
    }

    static String SeedOf(String raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    Int32 TokenCount() => DesignToken.FindAll(DesignToken._.ProjectId == _pid).Count;

    DesignToken? Shared(String path) => _tokens.Find(_pid, DesignSystemConstants.SharedThemeId, path);

    [Fact]
    public void 导入新令牌_层级与别名落库_来源与内容哈希可追溯()
    {
        var json = """
            {"colors":{"primary":{"$type":"color","500":{"$value":"#7C3AED"}}},
             "semantic":{"text-1":{"$type":"color","$value":"{colors.primary.500}","$description":"正文主色"}}}
            """;
        var plan = Parse(json);
        plan.Counts.Entries.Should().Be(2);
        plan.Rejected.Should().BeEmpty();

        var r = _tokens.UpsertBatch(_pid, plan.Patches, false);
        r.Succeeded.Should().BeTrue(string.Join(" | ", r.Diagnostics.Select(d => d.Message)));
        (r.Created + r.Updated).Should().Be(2);

        // 字面值按原样存（大小写归一发生在 ColorHex/oklch 侧，不在 Value 列）—— 导入不许悄悄改写用户写的值
        Shared("colors.primary.500")!.Value.Should().Be("#7C3AED");
        Shared("colors.primary.500")!.Tier.Should().Be(TokenTiers.Primitive);

        // semantic.text-1 在库里已有（各主题各一行）：导入要写回它**原本所在**的主题，而不是新建一条
        var semTheme = plan.Patches.Single(p => p.Path == "semantic.text-1").ThemeId;
        var sem = _tokens.Find(_pid, semTheme, "semantic.text-1")!;
        sem.Should().NotBeNull("已有路径必须落在它库里已有的主题上（不搬家）");
        sem.Tier.Should().Be(TokenTiers.Semantic, "库里已有的层级不许被按路径前缀改写");
        sem.AliasPath.Should().Be("colors.primary.500");
        sem.Description.Should().Be("正文主色");
        sem.Generator.Should().Be(TokenGenerators.Imported, "来源用既有词表，不另发明 import:dtcg 字面量");
        sem.GeneratorSeed.Should().Be(SeedOf(json), "同一份文件要能被认出来（FR-I6）");
    }

    [Fact]
    public void 未知type与缺type整条拒写_不降级成字符串()
    {
        var json = """
            {"bad":{"a":{"$type":"nope","$value":"1"},"b":{"$value":"2"}},"ok":{"c":{"$type":"number","$value":"3"}}}
            """;
        var plan = Parse(json);
        plan.Counts.Entries.Should().Be(3);
        plan.Patches.Should().HaveCount(1);
        plan.Rejected.Select(x => x.Path).Should().Equal("bad.a", "bad.b");
        string.Join(" ", plan.Rejected.Select(x => x.Reason)).Should().Contain("nope");

        var before = TokenCount();
        _tokens.UpsertBatch(_pid, plan.Patches, false).Succeeded.Should().BeTrue();
        DesignToken.FindAll(DesignToken._.ProjectId == _pid).Count.Should().Be(before + 1, "被拒的条目不许悄悄落库");
        Shared("bad.a").Should().BeNull();
    }

    [Fact]
    public void 复合值原样存进ValueJson_否则自己的导出文件回不来()
    {
        // 我们导出的 shadow.* 就是对象形状：拒收复合值等于宣布"round-trip 不可能成立"（计划偏离 D2）
        var json = """
            {"shadow":{"raised":{"$type":"shadow","$value":{"color":"#000000","offsetX":"2px","offsetY":"3px","blur":"8px","spread":"1px"}}}}
            """;
        var plan = Parse(json);
        plan.Counts.Composites.Should().Be(1);
        plan.Rejected.Should().BeEmpty();
        _tokens.UpsertBatch(_pid, plan.Patches, false).Succeeded.Should().BeTrue();

        var row = Shared("shadow.raised")!;
        row.ValueJson.Should().NotBeNullOrWhiteSpace();
        JsonNode.Parse(row.ValueJson!)!["offsetY"]!.GetValue<String>().Should().Be("3px");
    }

    [Fact]
    public void 手改保护默认生效_显式覆盖才改值()
    {
        _tokens.UpsertOne(_pid, new TokenPatch { Path = "colors.primary.500", Tier = TokenTiers.Primitive, Type = TokenTypes.Color, Value = "#000000" });
        var json = """{"colors":{"primary":{"$type":"color","500":{"$value":"#7c3aed"}}}}""";

        var plan = Parse(json);
        plan.Counts.Conflicts.Should().Be(1, "preview 阶段就要看见「这条会被手改保护挡掉」");
        plan.ConflictPaths.Should().Equal("colors.primary.500");

        var guarded = _tokens.UpsertBatch(_pid, plan.Patches, false);
        guarded.SkippedProtected.Should().Be(1);
        Shared("colors.primary.500")!.Value.Should().Be("#000000");

        var forced = _tokens.UpsertBatch(_pid, Parse(json, overwrite: true).Patches, true);
        forced.SkippedProtected.Should().Be(0);
        Shared("colors.primary.500")!.Value.Should().Be("#7c3aed");
    }

    [Fact]
    public void 成环样本整批不写_库里行数一行不少不多()
    {
        var json = """
            {"semantic":{"a":{"$type":"color","$value":"{semantic.b}"},"b":{"$type":"color","$value":"{semantic.a}"}}}
            """;
        var before = TokenCount();
        var r = _tokens.UpsertBatch(_pid, Parse(json).Patches, false);
        r.Succeeded.Should().BeFalse("别名成环必须被图校验抓住");
        r.Diagnostics.Should().NotBeEmpty();
        (r.Created + r.Updated).Should().Be(0);
        TokenCount().Should().Be(before, "不许出现写了一半");
    }

    [Fact]
    public void 空文档如实返回零_不报错也不假装成功()
    {
        var plan = Parse("""{"only":{"group":{"nested":{}}}}""");
        plan.Counts.Entries.Should().Be(0);
        plan.Patches.Should().BeEmpty();
        var r = _tokens.UpsertBatch(_pid, plan.Patches, false);
        (r.Created + r.Updated).Should().Be(0);
    }

    [Fact]
    public void 超过条目上限直接拒_上限只定义一次()
    {
        var many = new StringBuilder();
        for (var i = 0; i <= ImportLimits.MaxEntries; i++)
        {
            if (i > 0) many.Append(',');
            many.Append("\"t").Append(i).Append("\":{\"$type\":\"number\",\"$value\":\"1\"}");
        }
        var act = () => Parse("{" + many + "}");
        act.Should().Throw<ImportException>().WithMessage("*超过单次上限*");
    }

    [Fact]
    public void 已有路径不搬家_导入不会把primitive复制进主题层()
    {
        var dtcg = _export.Produce(_pid, ExportFormats.Dtcg, "light").Text;
        var before = TokenCount();
        var plan = Parse(dtcg, "light");
        plan.Patches.Should().NotBeEmpty("我们自己的导出至少要能解析出令牌");

        var r = _tokens.UpsertBatch(_pid, plan.Patches, true);
        r.Succeeded.Should().BeTrue(string.Join(" | ", r.Diagnostics.Select(d => d.Message)));
        r.Created.Should().Be(0, "文件里的路径库里都有 ⇒ 一条都不该新建（新建 = 搬家了）");
        TokenCount().Should().Be(before);
    }

    [Fact]
    public void roundtrip_导出到导入再到导出_两次逐字一致()
    {
        var first = _export.Produce(_pid, ExportFormats.Dtcg, "light").Text;
        var plan = Parse(first, "light");
        _tokens.UpsertBatch(_pid, plan.Patches, true).Succeeded.Should().BeTrue();

        var second = _export.Produce(_pid, ExportFormats.Dtcg, "light").Text;
        second.Should().Be(first, DiffHint(first, second));
    }

    /// <summary>不一致时把前若干处差异行列出来 —— 判据不许退化成"差不多就行"，但报错要能看懂差在哪。</summary>
    static String DiffHint(String a, String b)
    {
        var la = a.Split('\n');
        var lb = b.Split('\n');
        var sb = new StringBuilder("两次导出不逐字一致，前几处差异：");
        for (int i = 0, shown = 0; i < Math.Max(la.Length, lb.Length) && shown < 6; i++)
        {
            var x = i < la.Length ? la[i].Trim() : "<无>";
            var y = i < lb.Length ? lb[i].Trim() : "<无>";
            if (x == y) continue;
            sb.Append($"\n  第 {i + 1} 行：导出A=[{x}] 导出B=[{y}]");
            shown++;
        }
        return sb.ToString();
    }

    [Fact]
    public void 导入的新令牌必须进入同一套门禁判定()
    {
        // 一对几乎同色的 component 前景/背景：对比度门禁按命名约定会自己配对，
        // 库里出现它的 contrast 结论行 = 导入走的确实是同一套判定（不是旁路）
        var json = """
            {"component":{"demo":{"foreground":{"$type":"color","$value":"#7f7f7f"},"background":{"$type":"color","$value":"#808080"}}}}
            """;
        _tokens.UpsertBatch(_pid, Parse(json).Patches, false).Succeeded.Should().BeTrue();
        _engine.Run(_pid);

        _audits.List(_pid, 0, AuditKinds.Contrast, null)
            .Any(a => (a.TargetPath ?? "").Contains("component.demo.foreground", StringComparison.Ordinal))
            .Should().BeTrue("导入若绕过审计，就等于给门禁开了个后门（新令牌永远「没结论」）");
    }

    [Fact]
    public void 文件里没有tier_已有路径的层级以库为准()
    {
        // 回归：DTCG 不带 tier。曾经按"首段不是 semantic/component 就是 primitive"猜，
        // 于是 `chart.series-1`（库里是语义层、别名指向别的语义令牌）被降级成 primitive，
        // 它的别名就"逆向指向上层" → 整批图校验拒绝。round-trip 用例当场红。
        var row = DesignToken.FindAll(DesignToken._.ProjectId == _pid)
            .FirstOrDefault(t => t.Tier == TokenTiers.Semantic && !string.IsNullOrEmpty(t.AliasPath)
                                            && !t.Path.StartsWith("semantic.", StringComparison.Ordinal));
        row.Should().NotBeNull("生成器要产出「首段不是 semantic 的语义层别名令牌」（chart.series-*），这条回归才有得跑");

        var segs = row!.Path.Split('.');
        var plan = Parse(NestedJson(segs, "{" + row.AliasPath + "}"));
        plan.Patches.Should().ContainSingle("路径与库里那条同一条");
        plan.Patches[0].Tier.Should().Be(TokenTiers.Semantic, "库里已有的层级不许被路径前缀改写");
        _tokens.UpsertBatch(_pid, plan.Patches, true).Succeeded.Should().BeTrue("按库为准就不该触发逆向引用");
    }

    /// <summary>把 path 各段拼成 DTCG 的嵌套组形状，叶子用给定的 $value 文本（含花括号即别名）。</summary>
    static String NestedJson(String[] segs, String leafValue)
    {
        var node = "{\"$type\":\"" + TokenTypes.Color + "\",\"$value\":\"" + leafValue + "\"}";
        for (var i = segs.Length - 1; i >= 1; i--) node = "{\"" + segs[i] + "\":" + node + "}";
        return "{\"" + segs[0] + "\":" + node + "}";
    }
}
