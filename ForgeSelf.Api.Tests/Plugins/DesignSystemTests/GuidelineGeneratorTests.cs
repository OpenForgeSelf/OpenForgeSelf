using System.Text.RegularExpressions;
using Match = System.Text.RegularExpressions.Match;   // 与 Moq.Match 同名，显式消歧
using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC13：默认 UX 规范生成器的结构与文本纪律。
///
/// 这里**不评文案好坏**（那是用户在闸门2 的事，AC26）。机器只核四件可判定的事：
/// ① 目录恰 14 条、code 与分类是契约值；② 确定性；③ 引用的令牌真存在；④ **文本里不许出现数字 + 单位**
/// —— 第 ④ 条是 D5「规范只写令牌路径」的机器化：写进文本的数字就是第二份真相，令牌改了值它不会跟着改。
/// </summary>
public class GuidelineGeneratorTests
{
    /// <summary>项目里通常真有的令牌路径（用生成器产物当输入，不手写清单——手写清单本身就会漂）</summary>
    static List<String> RealPaths(String? density = "default") =>
        DesignGenerator.Generate(new GenerationRequest { Hue = 220, Density = density, Themes = ["light", "dark"] })
            .Shared.Select(p => p.Path)
            .Concat(DesignGenerator.Generate(new GenerationRequest { Hue = 220, Density = density, Themes = ["light", "dark"] })
                .Themed.Values.SelectMany(l => l.Select(p => p.Path)))
            .Distinct(StringComparer.Ordinal).ToList();

    static IReadOnlyList<GuidelineDraft> Drafts(String? kind = null, String? industry = null, String? density = null) =>
        GuidelineGenerator.Generate(kind, industry, density, RealPaths(density));

    /// <summary>
    /// 数字守卫：规则/正文里出现「数字 + 单位」或 <c>#hex</c> 即不合规。
    /// 例外只有 WCAG 比值（<c>4.5:1</c> / <c>3:1</c> / <c>7:1</c>）——它们是**阈值口径**，不是本项目的尺度值。
    /// 抽成方法是为了让反向探针能直接调它（守卫不空转）。
    /// </summary>
    internal static List<String> NumberViolations(IEnumerable<String> texts)
    {
        var bad = new List<String>();
        var unit = new Regex(@"\d+(\.\d+)?(px|rem|em|ms|s)\b", RegexOptions.Compiled);
        var hex = new Regex(@"#[0-9a-fA-F]{3,8}\b", RegexOptions.Compiled);
        var wcag = new Regex(@"\b(\d+(\.\d+)?):1\b", RegexOptions.Compiled);
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text)) continue;
            var stripped = wcag.Replace(text, "RATIO");   // 先把比值口径摘掉，避免把它误判成数字
            foreach (Match m in unit.Matches(stripped)) bad.Add($"{m.Value} ← {Trim(m, text)}");
            foreach (Match m in hex.Matches(stripped)) bad.Add($"{m.Value} ← {Trim(m, text)}");
        }

        return bad;

        static String Trim(Match m, String text)
        {
            var start = Math.Max(0, m.Index - 18);
            return "…" + text[start..Math.Min(text.Length, m.Index + m.Length + 12)] + "…";
        }
    }

    [Fact]
    public void AC13_目录恰好十四条且code与分类是契约值()
    {
        var drafts = Drafts();
        drafts.Select(d => d.Code).Should().Equal(GuidelineGenerator.Codes,
            "§G2 的 14 个 code 是契约（工具、界面、导出都按它认条目）");
        drafts.Select(d => d.Code).Distinct(StringComparer.Ordinal).Should().HaveCount(14);

        foreach (var d in drafts)
        {
            GuidelineCategories.Has(d.Category).Should().BeTrue($"{d.Code} 分类 {d.Category} 不在词表内");
            d.Title.Should().NotBeNullOrWhiteSpace($"{d.Code} 缺标题");
            d.Summary.Should().NotBeNullOrWhiteSpace($"{d.Code} 缺摘要");
            d.Body.Should().NotBeNullOrWhiteSpace($"{d.Code} 缺正文");
            d.AppliesTo.Should().NotBeEmpty($"{d.Code} 没有声明适用用途，界面无法筛");
            foreach (var a in d.AppliesTo)
                new[] { "product", "console", "brand", "marketing", "system" }.Should().Contain(a, $"{d.Code}.appliesTo 出现未知用途 {a}");
        }
    }

    [Fact]
    public void AC13_每条至少一个MUST且规则id条内唯一kebab()
    {
        foreach (var d in Drafts())
        {
            d.Rules.Count.Should().BeInRange(3, 6, $"{d.Code} 规则数须落在 §G2 约定的 3~6 条");
            d.Rules.Count(r => r.Level == "MUST").Should().BeGreaterThanOrEqualTo(1, $"{d.Code} 必须至少有一条 MUST");
            foreach (var r in d.Rules)
            {
                GuidelineCategories.HasLevel(r.Level).Should().BeTrue($"{d.Code}/{r.Id} 级别 {r.Level} 非法");
                r.Id.Should().MatchRegex("^[a-z0-9][a-z0-9-]{0,59}$", $"{d.Code} 规则 id 不合 kebab：{r.Id}");
                r.Text.Should().NotBeNullOrWhiteSpace();
                // MUST 要能被审查判级：不能是"建议/最好/可以考虑"这类商量话（那是 SHOULD/MAY 的语气）
                if (r.Level == "MUST")
                    foreach (var hedge in new[] { "建议", "宜先", "最好", "可以考虑" })
                        r.Text.Should().NotContain(hedge, $"{d.Code}/{r.Id} 标成 MUST 却用的是商量语气");
            }
            d.Rules.Select(r => r.Id).Distinct(StringComparer.Ordinal).Should().HaveCount(d.Rules.Count, $"{d.Code} 规则 id 重复");
        }
    }

    [Fact]
    public void AC13_确定性_同输入两次逐字相同()
    {
        // 比"逐字"而不是比对象相等：record 的集合成员按引用比较，比不出结构差异
        var a = Json(Drafts("console", "finance", "compact"));
        var b = Json(Drafts("console", "finance", "compact"));
        b.Should().Be(a, "同一组输入两次生成不同 = 生成器含随机或时钟，规范无法复现");
        GuidelineGenerator.SeedFor("console", "finance", "compact")
            .Should().Be(GuidelineGenerator.SeedFor("console", "finance", "compact"));
    }

    [Fact]
    public void AC13_引用的令牌全部真存在_不存在的被剔掉而不是留着骗人()
    {
        var paths = RealPaths();
        var drafts = GuidelineGenerator.Generate("product", "general", "default", paths);
        foreach (var d in drafts)
            foreach (var t in d.TokenRefs)
                paths.Should().Contain(t, $"{d.Code} 引用了项目里不存在的令牌 {t}");

        // 反过来：把引用路径全部拿掉，规范条目还在（文本不依赖数字），但引用必须变空——不能留着假引用
        var empty = GuidelineGenerator.Generate("product", "general", "default", []);
        empty.Should().HaveCount(14, "剔引用不该顺带删条目");
        empty.Should().OnlyContain(d => d.TokenRefs.Count == 0);
    }

    [Fact]
    public void AC13_数字守卫_全部生成文本零命中()
    {
        var texts = new List<String>();
        foreach (var kind in new[] { "console", "marketing", "product" })
            foreach (var density in new[] { "default", "compact", "comfortable" })
                foreach (var industry in new[] { "general", "finance", "healthcare", "devtools", "media", "commerce", "education" })
                {
                    foreach (var d in Drafts(kind, industry, density))
                    {
                        texts.Add(d.Title); texts.Add(d.Summary); texts.Add(d.Body);
                        texts.AddRange(d.Rules.Select(r => r.Text));
                    }
                }

        texts.Count.Should().BeGreaterThan(300, "没扫到足够文本 = 守卫在空转");
        NumberViolations(texts).Should().BeEmpty("生成文本里出现数字单位或字面色值（第二份真相）：\n" + String.Join("\n", NumberViolations(texts)));
    }

    [Fact]
    public void 反向探针_数字守卫必须响()
    {
        // 守卫不响 = AC13 那条判据是空的。先证明它会红，再说它绿有意义。
        NumberViolations(["正文字号不小于 16px"]).Should().NotBeEmpty();
        NumberViolations(["行高 1.5em 起"]).Should().NotBeEmpty();
        NumberViolations(["时长 200ms 以内"]).Should().NotBeEmpty();
        NumberViolations(["底色取 #0f172a"]).Should().NotBeEmpty();
        // WCAG 比值是阈值口径，不是本项目的尺度值 → 不该报
        NumberViolations(["正文对比度不低于 4.5:1、大字不低于 3:1"]).Should().BeEmpty();
    }

    static String Json(IReadOnlyList<GuidelineDraft> drafts) =>
        System.Text.Json.JsonSerializer.Serialize(drafts, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

    [Theory]
    [InlineData("console")]
    [InlineData("marketing")]
    [InlineData("product")]
    public void AC13_kind差异符合G3约定(string kind)
    {
        var by = Drafts(kind).ToDictionary(d => d.Code);
        var patterns = by["page-patterns"];
        var layout = by["layout-grid"];
        var nav = by["navigation"];
        var responsive = by["responsive"];

        if (kind == "console")
        {
            patterns.Rules.Select(r => r.Text).Append(patterns.Body)
                .Any(t => t.Contains("列表") && t.Contains("详情"))
                .Should().BeTrue("控制台模式必须含列表/详情/表单/仪表盘四类");
            layout.Rules.Should().NotContain(r => r.Id == "narrow-first", "桌面优先的条目不出现窄屏优先");
            nav.Rules.Should().Contain(r => r.Text.Contains("面包屑"));
        }
        if (kind == "marketing")
        {
            patterns.Rules.Should().Contain(r => r.Id == "hero-first", "营销页必须有首屏段");
            layout.Rules.Should().Contain(r => r.Id == "max-width" && r.Text.Contains("breakpoint.4"), "营销类才约束内容最大宽");
            nav.Rules.Should().Contain(r => r.Text.Contains("抽屉") || r.Text.Contains("标签"));
            responsive.Summary.Should().Contain("移动");
        }
        if (kind == "product")
        {
            patterns.Rules.Should().Contain(r => r.Id == "three-blocks");
            responsive.Rules.Should().Contain(r => r.Id == "priority" && r.Text.Contains("移动优先"));
        }
    }

    [Theory]
    [InlineData("compact", "space.5", "space.2")]
    [InlineData("default", "space.6", "space.3")]
    [InlineData("comfortable", "space.8", "space.4")]
    public void AC13_density差异符合G3约定(string density, string margin, string formGap)
    {
        var by = Drafts("product", "general", density).ToDictionary(d => d.Code);
        by["layout-grid"].Rules.First(r => r.Id == "page-margin").Text.Should().Contain(margin, $"密度 {density} 的页边距应引用 {margin}");
        by["forms"].Rules.First(r => r.Id == "field-gap").Text.Should().Contain(formGap, $"密度 {density} 的字段间距应引用 {formGap}");
        by["layout-grid"].TokenRefs.Should().Contain(margin);
        by["forms"].TokenRefs.Should().Contain(formGap);
    }

    [Fact]
    public void AC13_density还影响按钮默认尺寸建议()
    {
        Drafts("product", "general", "comfortable")
            .First(d => d.Code == "buttons").Rules.First(r => r.Id == "size-choice").Text.Should().Contain("lg");
        Drafts("product", "general", "compact")
            .First(d => d.Code == "buttons").Rules.First(r => r.Id == "size-choice").Text.Should().Contain("sm");
    }

    [Theory]
    [InlineData("finance")]
    [InlineData("healthcare")]
    [InlineData("devtools")]
    [InlineData("media")]
    [InlineData("commerce")]
    [InlineData("education")]
    public void AC13_industry差异各自多出条目(string industry)
    {
        var general = Drafts("product", "general").ToDictionary(d => d.Code);
        var special = Drafts("product", industry).ToDictionary(d => d.Code);
        var added = special.SelectMany(kv => kv.Value.Rules.Select(r => $"{kv.Key}/{r.Id}"))
            .Except(general.SelectMany(kv => kv.Value.Rules.Select(r => $"{kv.Key}/{r.Id}")))
            .ToList();

        // 每个行业都必须至少留一处可观察差异，否则「按行业生成」是装饰
        added.Should().NotBeEmpty($"industry={industry} 与 general 的产物完全相同 = 行业参数没起作用");
    }

    [Fact]
    public void AC13_industry差异落在约定的那几条上()
    {
        Drafts("product", "finance").First(d => d.Code == "data-display").Rules.Should().Contain(r => r.Text.Contains("千分位"));
        Drafts("product", "healthcare").First(d => d.Code == "a11y").Rules.Should().Contain(r => r.Text.Contains("临床"));
        Drafts("product", "devtools").First(d => d.Code == "data-display").Rules.Should().Contain(r => r.Text.Contains("font.mono"));
        Drafts("product", "media").First(d => d.Code == "content").Rules.Should().Contain(r => r.Text.Contains("breakpoint.4"));
        Drafts("product", "commerce").First(d => d.Code == "content").Rules.Should().Contain(r => r.Text.Contains("原价"));
        Drafts("product", "education").First(d => d.Code == "feedback").Rules.Should().Contain(r => r.Text.Contains("鼓励"));
        // general 不该带上任何行业专项条目
        var general = Drafts("product", "general");
        general.First(d => d.Code == "data-display").Rules.Should().NotContain(r => r.Text.Contains("千分位"));
    }

    [Fact]
    public void AC13_未知kind与industry有回落不抛()
    {
        var drafts = Drafts("不存在的用途", "不存在的行业");
        drafts.Should().HaveCount(14);
        // kind 未知 → 产品类模板；industry 未知 → general（无行业专项条目）
        drafts.First(d => d.Code == "page-patterns").Rules.Should().Contain(r => r.Id == "three-blocks");
        drafts.First(d => d.Code == "data-display").Rules.Should().NotContain(r => r.Text.Contains("千分位"));
    }

    /// <summary>
    /// AC26 清单产出器（测试资产，不是临时脚本）：默认什么都不做，只有 <c>DS_DUMP_GUIDELINES=1</c> 才写盘
    /// <c>.temp/ds-m3/guidelines-catalogue.md</c> —— 14 条规范 × 3 种用途的**全文**。
    ///
    /// 为什么要它：闸门2 要用户逐字审措辞，而"审的东西"必须和生成器产出**同一份**（手抄进文档就等于给了第二份真相）。
    /// 与黄金基线录制器同一套纪律：默认不产出、无 Skip 用例、跑一次拿文件。
    /// </summary>
    [Fact]
    public void 清单产出器_仅DS_DUMP_GUIDELINES为1时写盘()
    {
        if (Environment.GetEnvironmentVariable("DS_DUMP_GUIDELINES") != "1") return;   // 默认不产出（与黄金基线录制器同形）

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# UX 规范全文清单（生成器 v{GuidelineGenerator.Version}）");
        sb.AppendLine();
        sb.AppendLine("> 由 `GuidelineGeneratorTests.清单产出器` 直接调生成器产出（不是手抄）。三种用途各一份全文，");
        sb.AppendLine("> 行业专项条目（finance/healthcare/devtools/media/commerce/education）另列在每份末尾。");
        sb.AppendLine("> 文本里只有**令牌路径**，具体数值由渲染端现查当前令牌（决策 D5）。");

        foreach (var kind in new[] { "console", "marketing", "product" })
        {
            sb.AppendLine();
            sb.AppendLine($"## 用途 kind = {kind}（density=default）");
            foreach (var d in Drafts(kind))
            {
                sb.AppendLine();
                sb.AppendLine($"### {d.Title}（`{d.Code}` · {GuidelineCategories.Display(d.Category)}）");
                sb.AppendLine();
                sb.AppendLine($"> {d.Summary}");
                sb.AppendLine();
                foreach (var r in d.Rules) sb.AppendLine($"- **{r.Level}** {r.Text}");
                sb.AppendLine();
                sb.AppendLine(d.Body);
                sb.AppendLine();
                sb.AppendLine($"- 引用令牌：{(d.TokenRefs.Count == 0 ? "（无）" : string.Join("、", d.TokenRefs.Select(p => $"`{p}`")))}");
                sb.AppendLine($"- 适用用途：{(d.AppliesTo.Count == 0 ? "全部" : string.Join("/", d.AppliesTo))}");
            }
        }

        var dir = Path.Combine(FindRepoRoot(), ".temp", "ds-m3");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "guidelines-catalogue.md");
        File.WriteAllText(file, sb.ToString());
        File.Exists(file).Should().BeTrue();
    }

    static String FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return dir!.FullName;
    }
}
