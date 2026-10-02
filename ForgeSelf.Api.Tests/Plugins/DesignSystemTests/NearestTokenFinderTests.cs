using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 最近令牌测试（AC8 / §F）：六类（颜色/长度/时长/阴影/字体族/字重）各自正确返回最近项与 exact；
/// 并列决胜（semantic 先于 component、deprecated 靠后）；primitive 不被推荐；不可解析给明确错误；无候选回空 + note。
/// 纯函数测试：用自造 Snapshot 建 TokenIndex，不碰 DB。
/// </summary>
public class NearestTokenFinderTests
{
    static TokenIndex Index(params (String Path, String Tier, String Type, String Value, String? ValueJson)[] rows)
    {
        var tokens = rows.Select(r => new ExportService.Snap(r.Path, r.Tier, r.Type, r.Value, r.ValueJson, null, null,
            r.Path.Split('.')[0], null, null, -1, null, null)).ToList();
        var project = new DesignProject { Code = "t", Name = "测试", Version = "1.0.0" };
        var snap = new ExportService.Snapshot(project, "light", tokens, [], [], [], [], [], [], []);
        var export = new ExportService(new TokenRepository(), new DesignProjectService(), new CatalogRepository());
        return TokenIndex.FromSnapshot(export, snap);
    }

    static TokenIndex SampleIndex() => Index(
        ("semantic.brand", "semantic", "color", "#7c3aed", null),
        ("semantic.surface-bg", "semantic", "color", "#ffffff", null),
        ("component.card.background", "component", "color", "#f4f4f5", null),
        ("color.blue.600", "primitive", "color", "#2563eb", null),                    // primitive 不应被推荐
        ("space.4", "semantic", "dimension", "16px", null),
        ("space.5", "semantic", "dimension", "24px", null),
        ("radius.md", "semantic", "dimension", "6px", null),
        ("size.lg", "semantic", "dimension", "18px", null),
        ("border.thick", "semantic", "dimension", "2px", null),
        ("duration.micro", "semantic", "duration", "120ms", null),
        ("duration.base", "semantic", "duration", "200ms", null),
        ("duration.macro", "semantic", "duration", "320ms", null),
        ("duration.macro-reduced", "semantic", "duration", "240ms", null),            // -reduced 不参与
        ("shadow.elevation-1", "semantic", "shadow", "0 4px 12px rgba(0,0,0,.2)",
            """[{"color":"rgba(0,0,0,.2)","alpha":0.2,"inset":false,"offsetX":0,"offsetY":4,"blur":12,"spread":0}]"""),
        ("font.sans", "semantic", "font-family", "Inter, system-ui, sans-serif", null),
        ("font.mono", "semantic", "font-family", "JetBrains Mono, monospace", null),
        ("weight.semibold", "semantic", "number", "600", null),
        ("weight.bold", "semantic", "number", "700", null));

    [Fact]
    public void 颜色_恰好等于品牌色_exact且replace可直接替换()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "#7c3aed", null, 3);
        r.Error.Should().BeNull();
        var m = r.Matches.First();
        m.Path.Should().Be("semantic.brand");
        m.Tier.Should().Be("semantic");
        m.CssVar.Should().Be("--ds-semantic-brand");
        m.Exact.Should().BeTrue();
        m.Distance.Should().Be(0);
        m.Replace.Should().Be("var(--ds-semantic-brand)");
    }

    [Fact]
    public void 颜色_primitive不被推荐()
    {
        // 只有 primitive 色令牌 → 无候选 + note
        var idx = Index(("color.blue.600", "primitive", "color", "#2563eb", null));
        var r = NearestTokenFinder.Find(idx, "#2563eb", null, 3);
        r.Error.Should().BeNull();
        r.Matches.Should().BeEmpty();
        r.Note.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void 颜色_并列决胜_semantic先于component()
    {
        // 两个同距离色令牌：semantic 与 component，semantic 应排前
        var idx = Index(
            ("component.card.background", "component", "color", "#7c3aed", null),
            ("semantic.surface-bg", "semantic", "color", "#7c3aed", null));
        var r = NearestTokenFinder.Find(idx, "#7c3aed", null, 2);
        r.Matches[0].Tier.Should().Be("semantic");
        r.Matches[0].Path.Should().Be("semantic.surface-bg");
        r.Matches[1].Path.Should().Be("component.card.background");
    }

    [Fact]
    public void 颜色_deprecated排后()
    {
        var idx = Index(
            ("semantic.brand-old", "semantic", "color", "#7c3aed", null),
            ("semantic.brand", "semantic", "color", "#7c3aed", null))
            .WithLifecycles(new Dictionary<String, String> { ["semantic.brand-old"] = "deprecated" });
        var r = NearestTokenFinder.Find(idx, "#7c3aed", null, 2);
        r.Matches[0].Path.Should().Be("semantic.brand");
        r.Matches[0].Deprecated.Should().BeFalse();
        r.Matches[1].Path.Should().Be("semantic.brand-old");
        r.Matches[1].Deprecated.Should().BeTrue();
    }

    [Fact]
    public void 颜色_rgb与oklch与命名色可解析()
    {
        var idx = SampleIndex();
        NearestTokenFinder.Find(idx, "rgb(124, 58, 237)", null, 1).Matches[0].Path.Should().Be("semantic.brand");
        NearestTokenFinder.Find(idx, "rgb(124 58 237)", null, 1).Matches[0].Path.Should().Be("semantic.brand");
        NearestTokenFinder.Find(idx, "oklch(0.5 0.2 300)", null, 1).Should().NotBeNull();
        // 命名色 black → surface-bg（白）距离较远，但能解析不报错
        var named = NearestTokenFinder.Find(idx, "black", null, 1);
        named.Error.Should().BeNull();
        named.Matches.Should().NotBeEmpty();
    }

    [Fact]
    public void 长度_恰好等于space档_exact且标category()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "16px", null, 3);
        r.Error.Should().BeNull();
        var m = r.Matches.First();
        m.Path.Should().Be("space.4");
        m.Exact.Should().BeTrue();
        m.Category.Should().Be("space");
    }

    [Fact]
    public void 长度_rem按16换算()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "1rem", "padding", 3);
        r.Matches.First().Path.Should().Be("space.4");
        r.Matches.First().Exact.Should().BeTrue();
        r.Matches.First().Category.Should().Be("space");
    }

    [Fact]
    public void 长度_无线索四类全搜_并按类决胜()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "6px", null, 5);
        r.Matches.Select(m => m.Category).Distinct().Should().Contain(new[] { "space", "radius", "size", "border" });
        // 6px 恰好 = radius.md，distance=0 排最前
        r.Matches.First().Path.Should().Be("radius.md");
    }

    [Fact]
    public void 时长_200ms命中base且reduced派生不参与()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "200ms", null, 3);
        r.Matches.First().Path.Should().Be("duration.base");
        r.Matches.First().Exact.Should().BeTrue();
        r.Matches.Select(m => m.Path).Should().NotContain("duration.macro-reduced");
    }

    [Fact]
    public void 时长_0_2s等价200ms()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "0.2s", null, 3);
        r.Matches.First().Path.Should().Be("duration.base");
    }

    [Fact]
    public void 阴影_第一层距离匹配且note只比较第一层()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "0 4px 12px rgba(0,0,0,.2)", null, 3);
        r.Error.Should().BeNull();
        r.Matches.First().Path.Should().Be("shadow.elevation-1");
        r.Matches.First().Exact.Should().BeTrue();
        r.Note.Should().Contain("只比较第一层");
    }

    [Fact]
    public void 字体族_等宽含mono优先匹配font_mono()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "\"Fira Code\", monospace", "font-family", 3);
        r.Matches.First().Path.Should().Be("font.mono");
    }

    [Fact]
    public void 字体族_无mono匹配font_sans()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "\"Helvetica Neue\", Arial", "font-family", 3);
        r.Matches.First().Path.Should().Be("font.sans");
    }

    [Fact]
    public void 字重_600命中semibold()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "600", "font-weight", 3);
        r.Matches.First().Path.Should().Be("weight.semibold");
        r.Matches.First().Exact.Should().BeTrue();
    }

    [Fact]
    public void 字重_bold映射700()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "bold", "font-weight", 3);
        r.Matches.First().Path.Should().Be("weight.bold");
    }

    [Fact]
    public void 不可解析_给明确错误()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "not-a-value!", null, 3);
        r.Error.Should().NotBeNull();
        r.Error.Should().Contain("无法识别的值 not-a-value!");
        r.Error.Should().Contain("颜色（hex/rgb/hsl/oklch）");
    }

    [Fact]
    public void 无候选_回空与note()
    {
        var idx = Index(("space.4", "semantic", "dimension", "16px", null));
        var r = NearestTokenFinder.Find(idx, "200ms", null, 3);
        r.Error.Should().BeNull();
        r.Matches.Should().BeEmpty();
        r.Note.Should().Contain("duration");
    }

    [Fact]
    public void 空输入_给明确错误()
    {
        var r = NearestTokenFinder.Find(SampleIndex(), "   ", null, 3);
        r.Error.Should().NotBeNull();
    }
}
