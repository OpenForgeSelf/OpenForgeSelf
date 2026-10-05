using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Controllers;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Microsoft.AspNetCore.Mvc;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC15：UX 规范的 REST 面 —— 读回、过滤、括注当前值、坏引用点名、错误文案与状态码、乐观并发、软归档恢复。
///
/// 这一层的价值在于**它只经控制器**：控制器里的 <see cref="DesignSystemController.GuidelineDto"/> 是唯一
/// 把库里那堆 JSON 袋变成界面能用的东西的地方。服务层测绿了不代表括注/状态码/brokenRefs 对（自查表 #21）。
///
/// 特别钉一条：**括注里的当前值必须与 `tokens/effective` 同一来源**。规范正文写的是令牌路径，
/// 显示的是取值函数现算的结果；如果这里自己再解析一遍令牌，改了值就会出现"规范说 16px、令牌页说 24px"。
///
/// 隔离：本类专属随机临时库目录，只创建不删除（铁律 10）。类级鉴权与"无 DELETE 路由"的反射断言在
/// <see cref="GuidelineSchemaTests"/>，不在这里重复。
/// </summary>
[Collection("XCode")]
public class GuidelineRestTests : IDisposable
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly GuidelineRepository _repo = new();
    readonly GuidelineService _svc;
    readonly ExportService _export;
    readonly AuditEngine _auditEngine;
    readonly GenerationService _generation;
    readonly DesignSystemController _ctl;

    public GuidelineRestTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsGuidelineRest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);
        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);

        DesignProject.Meta.Cache.Expire = 0;
        DesignTheme.Meta.Cache.Expire = 0;
        DesignToken.Meta.Cache.Expire = 0;
        DesignShadowLayer.Meta.Cache.Expire = 0;
        DesignComponent.Meta.Cache.Expire = 0;
        DesignComponentVariant.Meta.Cache.Expire = 0;
        DesignIcon.Meta.Cache.Expire = 0;
        DesignAsset.Meta.Cache.Expire = 0;
        DesignScreen.Meta.Cache.Expire = 0;
        DesignFontFace.Meta.Cache.Expire = 0;
        DesignAudit.Meta.Cache.Expire = 0;
        DesignRelease.Meta.Cache.Expire = 0;
        DesignGuideline.Meta.Cache.Expire = 0;

        _auditEngine = new AuditEngine(_tokens, _projects, _audits);
        _export = new ExportService(_tokens, _projects, _catalog);
        _svc = new GuidelineService(_projects, _tokens, _repo);
        // 与插件 DI 同形：出参取值走 ExportService 的快照视图，导出服务没接规范服务时快照就没有规范行
        _export.Guidelines = _svc;
        _generation = new GenerationService(_tokens, _projects, _catalog, _auditEngine) { Guidelines = _svc };
        _ctl = new DesignSystemController(_projects, _tokens, _catalog, _audits, _auditEngine, _export,
            new ReleaseService(_tokens, _projects, _audits, _auditEngine, new DesignSystemPaths(_dbDir), _catalog),
            _generation, new AgentAccess(new DesignSystemPaths(_dbDir)),
            new DesignReviewService(_export, _tokens, _projects),
            new QuickCreateService(_projects, _generation), null!, new PreviewCssService(_export), _svc);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    Int64 NewGeneratedProject()
    {
        var id = _projects.Create(new ProjectInput { Code = "rest-" + Guid.NewGuid().ToString("N")[..8], Name = "规范 REST" }).Id;
        _ctl.Generate(id, new GenerationRequest { Hue = 210 }, false);
        return id;
    }

    static JsonElement DataOf(IActionResult res)
    {
        var jr = res as JsonResult ?? throw new Xunit.Sdk.XunitException($"期望 200 JsonResult，实际 {res.GetType().Name}（状态码 {(res as ObjectResult)?.StatusCode}）");
        var root = JsonDocument.Parse(JsonSerializer.Serialize(jr.Value, JsonOpts)).RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        return root.GetProperty("data");
    }

    static Int32 StatusOf(IActionResult res) => res switch
    {
        ObjectResult { StatusCode: Int32 code } => code,
        JsonResult => 200,
        _ => throw new Xunit.Sdk.XunitException($"未预期的结果类型 {res.GetType().Name}"),
    };

    static String ErrorOf(IActionResult res)
    {
        var or = res as ObjectResult ?? throw new Xunit.Sdk.XunitException("错误响应必须是 ObjectResult");
        var root = JsonDocument.Parse(JsonSerializer.Serialize(or.Value, JsonOpts)).RootElement;
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        return root.GetProperty("error").GetString()!;
    }

    static List<JsonElement> Rows(JsonElement data) => data.EnumerateArray().ToList();

    [Fact]
    public void AC15_清单返回14条且带分类中文标签()
    {
        var id = NewGeneratedProject();

        var rows = Rows(DataOf(_ctl.ListGuidelines(id, null, null, null)));
        rows.Should().HaveCount(GuidelineGenerator.Codes.Length);
        rows.Select(r => r.GetProperty("code").GetString()).Should().BeEquivalentTo(GuidelineGenerator.Codes);

        var layout = rows.First(r => r.GetProperty("code").GetString() == "layout-grid");
        layout.GetProperty("category").GetString().Should().Be("layout");
        layout.GetProperty("categoryLabel").GetString().Should().Be(GuidelineCategories.Display("layout")).And.NotBeNullOrEmpty();
        layout.GetProperty("source").GetString().Should().Be("generated");
        layout.GetProperty("status").GetString().Should().Be("adopted");
        layout.GetProperty("generatorVersion").GetString().Should().Be(GuidelineGenerator.Version);
    }

    [Fact]
    public void AC15_引用的令牌全部真实存在_括注值与tokens_effective同源()
    {
        var id = NewGeneratedProject();

        var effective = EffectiveValues(id);
        var rows = Rows(DataOf(_ctl.ListGuidelines(id, null, null, null)));
        var annotated = 0;
        foreach (var row in rows)
        {
            var code = row.GetProperty("code").GetString()!;

            // ① 每条规范都必须引用真存在的令牌：chip 值与导出括注都从 tokenRefs 现取，
            //    引用取不到值 = 界面上是一排「令牌已不存在」，规范看着正常其实是空话（M3 实测踩过：
            //    取值函数落在共享层视图时 semantic.*/shadow.* 整片判死）
            var refs = row.GetProperty("tokenRefs").EnumerateArray().Select(e => e.GetString()!).ToList();
            // 出参取值走的是 ExportService 的快照，所以"导出服务接没接规范服务"直接决定括注对不对（装配漏一次就会整片假断链）
            _export.Load(id, "light").GuidelineRows.Should().HaveCount(GuidelineGenerator.Codes.Length,
                "导出侧没接规范服务：快照里没有规范行，取值视图会退回共享层并把语义/阴影引用整片判成断链");
            refs.Should().NotBeEmpty($"{code} 一条令牌都没引用，界面 chip 与导出都是空的");
            var dead = refs.Where(p => !effective.TryGetValue(p, out var v) || String.IsNullOrEmpty(v)).ToList();
            dead.Should().BeEmpty($"{code} 引用了默认主题取不到值的令牌：{String.Join("、", dead)}");
            row.GetProperty("brokenRefs").GetArrayLength().Should().Be(0, $"{code} 是新项目，不该有断链");
            // chip 的取值来源：出参 tokenValues 必须与 tokens/effective 一字不差（界面不再自己查第二遍）
            foreach (JsonElement p in row.GetProperty("tokenRefs").EnumerateArray())
            {
                var path = p.GetString()!;
                if (effective.TryGetValue(path, out var ev) && !String.IsNullOrEmpty(ev))
                    row.GetProperty("tokenValues").GetProperty(path).GetString().Should().Be(ev, $"{code} 的 chip 值与令牌页不同源");
            }

            // ② 文本里出现的具体路径，括注值必须等于 tokens/effective 的同一个值（族引用 `space.*` 不括注）
            var texts = new List<(String Raw, String Shown)>
            {
                (row.GetProperty("bodyRaw").GetString() ?? "", row.GetProperty("body").GetString() ?? ""),
                ("", row.GetProperty("summary").GetString() ?? ""),
            };
            foreach (JsonElement r in row.GetProperty("rules").EnumerateArray())
                texts.Add((r.GetProperty("textRaw").GetString() ?? "", r.GetProperty("text").GetString() ?? ""));

            foreach (var (raw, shown) in texts)
            {
                // 引用集合用生产端同一个扫描函数拿，不在测试里另写一份正则
                foreach (var path in GuidelineRenderer.ConcreteRefs(raw))
                {
                    if (!effective.TryGetValue(path, out var value) || String.IsNullOrEmpty(value)) continue;
                    shown.Should().Contain($"`{path}`（{value}）", $"{code} 的括注值与 tokens/effective 不一致：{path}");
                    if (raw.Length > 0) raw.Should().NotContain($"（{value}）", "原文必须只有路径没有数字，否则数字进了存储层");
                    annotated++;
                }
            }

            // ③ 正文/规则原文里不许出现"已不存在"这种坏引用标注（新项目）
            row.GetProperty("body").GetString()!.Should().NotContain("已不存在", $"{code} 的正文出现了坏引用");
        }

        // 防空转：上面 ② 的比对必须真的发生过（至少一条规范在正文里引用了具体路径）
        annotated.Should().BeGreaterThan(0, "没有任何一条规范在文本里引用具体令牌 = ② 整段是空转");
        rows.Select(r => r.GetProperty("code").GetString()).Should().BeEquivalentTo(GuidelineGenerator.Codes);
    }

    [Fact]
    public void AC15_按分类与状态过滤_默认口径排除归档()
    {
        var id = NewGeneratedProject();

        var a11y = Rows(DataOf(_ctl.ListGuidelines(id, null, "a11y", null)));
        a11y.Should().NotBeEmpty();
        a11y.Select(r => r.GetProperty("category").GetString()).Should().OnlyContain(c => c == "a11y");

        Rows(DataOf(_ctl.ListGuidelines(id, null, "typography", null))).Should().BeEmpty("未知分类应回空而不是全部");

        _ctl.ArchiveGuideline(id, "motion");
        Rows(DataOf(_ctl.ListGuidelines(id, null, null, null))).Select(r => r.GetProperty("code").GetString())
            .Should().NotContain("motion");
        var archived = Rows(DataOf(_ctl.ListGuidelines(id, "archived", null, null)));
        archived.Select(r => r.GetProperty("code").GetString()).Should().Equal("motion");
        Rows(DataOf(_ctl.ListGuidelines(id, "all", null, null))).Should().HaveCount(14, "归档是软删：all 口径必须还看得见");

        // PUT 恢复
        _ctl.SaveGuideline(id, "motion", new GuidelinePatch { Status = "adopted" });
        Rows(DataOf(_ctl.ListGuidelines(id, null, null, null))).Select(r => r.GetProperty("code").GetString())
            .Should().Contain("motion");
    }

    [Fact]
    public void AC15_坏引用逐条点名并标注_不静默()
    {
        var id = NewGeneratedProject();

        // 绕过服务层校验直接写一行"引用已被删掉"的规范（等价于历史数据：写的时候令牌还在，后来没了）
        _repo.Upsert(id, "layout-legacy", new GuidelinePatch
        {
            Title = "历史遗留",
            Body = "卡片内边距用 `space.gone.9`，正常档用 `space.6`",
            TokenRefs = ["space.gone.9"],
        });

        var dto = DataOf(_ctl.GetGuideline(id, "layout-legacy", null));
        var broken = dto.GetProperty("brokenRefs").EnumerateArray().Select(e => e.GetString()).ToList();
        // 取不到值却不点名 = 界面上会显示一条看起来正常的坏规范
        broken.Should().BeEquivalentTo(new[] { "space.gone.9" });
        dto.GetProperty("body").GetString()!.Should().Contain("space.gone.9").And.Contain("已不存在");
        dto.GetProperty("bodyRaw").GetString()!.Should().NotContain("已不存在");

        // 同一条规则里的正常引用不受影响，仍然括注出当前值
        var effective = EffectiveValues(id);
        dto.GetProperty("body").GetString()!.Should().Contain($"`space.6`（{effective["space.6"]}）");
    }

    [Fact]
    public void AC15_校验失败回400并把可用值写进文案()
    {
        var id = NewGeneratedProject();

        var badCategory = _ctl.SaveGuideline(id, "layout-grid", new GuidelinePatch { Category = "typography" });
        StatusOf(badCategory).Should().Be(400);
        ErrorOf(badCategory).Should().Contain("typography").And.Contain("layout");

        var badRule = _ctl.SaveGuideline(id, "layout-grid", new GuidelinePatch
        {
            Rules = [new GuidelineRuleInput("Bad Id", "MUST", "规则文本")],
        });
        StatusOf(badRule).Should().Be(400);
        ErrorOf(badRule).Should().Contain("Bad Id");

        var missingToken = _ctl.SaveGuideline(id, "layout-grid", new GuidelinePatch { TokenRefs = ["color.never.exist"] });
        StatusOf(missingToken).Should().Be(400);
        ErrorOf(missingToken).Should().Contain("color.never.exist");

        // 三条都被拒：库里那一行必须还是生成时那份，没被半截写入污染
        var after = DataOf(_ctl.GetGuideline(id, "layout-grid", null));
        after.GetProperty("source").GetString().Should().Be("generated");
        after.GetProperty("tokenRefs").GetArrayLength().Should().BeGreaterThan(0);
        after.GetProperty("brokenRefs").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void AC15_code不存在回404_并发冲突回409_项目不存在沿用控制器既有异常口径()
    {
        var id = NewGeneratedProject();

        StatusOf(_ctl.GetGuideline(id, "no-such-code", null)).Should().Be(404);

        // 项目 id 不存在：沿用全控制器既有形态 —— RequireProject 在 Guard 之外抛 KeyNotFoundException。
        // 宿主没有全局异常中间件，HTTP 侧因此是 500 而不是 404（M1/M2 起就如此；改它=改全站状态码口径，
        // 已记 TODO 待拍板，M3 不给规范端点单独开小灶）。
        Record.Exception(() => _ctl.ListGuidelines(id + 99999, null, null, null)).Should().BeOfType<KeyNotFoundException>();
        Record.Exception(() => _ctl.ArchiveGuideline(id + 99999, "layout-grid")).Should().BeOfType<KeyNotFoundException>();

        var saved = DataOf(_ctl.SaveGuideline(id, "layout-grid", new GuidelinePatch { Title = "第一版" }));
        var stamp = saved.GetProperty("updatedAt").GetDateTime();
        var conflict = _ctl.SaveGuideline(id, "layout-grid", new GuidelinePatch { Title = "第二版", ExpectUpdatedAt = stamp.AddMinutes(-3) });
        StatusOf(conflict).Should().Be(409);
        ErrorOf(conflict).Should().Contain("layout-grid");
        DataOf(_ctl.GetGuideline(id, "layout-grid", null)).GetProperty("title").GetString().Should().Be("第一版");

        StatusOf(_ctl.SaveGuideline(id, "layout-grid", new GuidelinePatch { Title = "第二版", ExpectUpdatedAt = stamp })).Should().Be(200);
    }

    [Fact]
    public void AC15_generate端点回传四类计数_不是光秃秃的总数()
    {
        var id = _projects.Create(new ProjectInput { Code = "gen-" + Guid.NewGuid().ToString("N")[..8], Name = "只生成规范" }).Id;
        DesignGenerator.ApplyToProject(_tokens, _projects, id, new GenerationRequest { Hue = 210 }, false);

        var first = DataOf(_ctl.GenerateGuidelines(id, false));
        first.GetProperty("created").GetArrayLength().Should().Be(14);
        first.GetProperty("skipped").GetArrayLength().Should().Be(0);
        first.GetProperty("skippedProtected").GetArrayLength().Should().Be(0);
        first.GetProperty("overwritten").GetArrayLength().Should().Be(0);
        first.GetProperty("total").GetInt32().Should().Be(14);

        _ctl.SaveGuideline(id, "buttons", new GuidelinePatch { Title = "手改" });
        var second = DataOf(_ctl.GenerateGuidelines(id, false));
        second.GetProperty("created").GetArrayLength().Should().Be(0);
        second.GetProperty("skipped").GetArrayLength().Should().Be(13);
        second.GetProperty("skippedProtected").EnumerateArray().Select(e => e.GetString()).Should().Equal("buttons");

        var third = DataOf(_ctl.GenerateGuidelines(id, true));
        third.GetProperty("overwritten").GetArrayLength().Should().Be(14);
        third.GetProperty("created").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void AC15_meta供给规范词表与能力位_前端不抄第二份()
    {
        var meta = DataOf(_ctl.Meta());

        meta.GetProperty("capabilities").EnumerateArray().Select(e => e.GetString()).Should().Contain("guidelines");
        meta.GetProperty("guidelineCategories").EnumerateArray().Select(e => e.GetString())
            .Should().Equal(GuidelineCategories.All);
        meta.GetProperty("guidelineLevels").EnumerateArray().Select(e => e.GetString())
            .Should().Equal(GuidelineCategories.Levels);

        // 风格轴（M3 A 片）同源供给：字段名必须与 GenerationRequest 的真实属性一致，界面按它渲染控件
        var axes = meta.GetProperty("styleAxes").EnumerateArray().ToList();
        axes.Should().HaveCount(StyleAxes.Order.Length);
        axes.Select(a => a.GetProperty("axis").GetString()).Should().Equal(StyleAxes.Order);
    }

    /// <summary>项目默认主题编码（与控制器 ValueResolver 同一口径：IsDefault 优先，否则首个主题）</summary>
    String DefaultTheme(Int64 id) =>
        (_projects.ListThemes(id).FirstOrDefault(t => t.IsDefault) ?? _projects.ListThemes(id).FirstOrDefault())?.Code ?? "light";

    /// <summary>走 `tokens/effective` 同一控制器方法取 path→value（同源比对的基准；主题必须与规范出参一致）</summary>
    Dictionary<String, String?> EffectiveValues(Int64 id, String? theme = null)
    {
        var data = DataOf(_ctl.EffectiveTokens(id, theme ?? DefaultTheme(id)));
        var map = new Dictionary<String, String?>(StringComparer.Ordinal);
        foreach (JsonElement item in data.GetProperty("items").EnumerateArray())
            map[item.GetProperty("path").GetString()!] = item.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : v.ValueKind == JsonValueKind.Null ? null : v.GetRawText();
        map.Should().ContainKey("space.6", "effective 视图里没有间距令牌 = 这个基准函数本身没产出，比对是空的");
        return map;
    }
}
