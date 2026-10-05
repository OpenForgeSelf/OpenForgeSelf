using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M2 FR1 / AC1-AC3：内存预览 CSS（`preview-css`）。
///
/// 两条硬判据：
/// ① **零写库**：内存构图 → ToCss 全程不碰库，全部预设 × 明暗调用前后各表行数不变（AC1）。
/// ② **同源**：把同一请求真落库后 `export?format=css`，与内存预览去掉注释规整空白后**逐字相同**（AC2）——
///    这条是"展厅看到的 == 交付拿到的"的唯一证据，一旦快照构造出现第二份逻辑立刻变红。
///
/// 隔离库只建不删（铁律 10）。夹具与 <see cref="QuickCreateServiceTests"/> 同形。
/// </summary>
[Collection("XCode")]
public class PreviewCssTests : IDisposable
{
    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly AuditEngine _auditEngine;
    readonly GenerationService _generation;
    readonly ExportService _export;
    readonly PreviewCssService _preview;

    public PreviewCssTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfPreview_{Guid.NewGuid():N}");
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

        _auditEngine = new AuditEngine(_tokens, _projects, _audits);
        _generation = new GenerationService(_tokens, _projects, _catalog, _auditEngine);
        _export = new ExportService(_tokens, _projects, _catalog);
        _preview = new PreviewCssService(_export);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>全部表行数快照：AC1「调用前后行数不变」的判据。新表必须一起进这张清单（下面那条长度断言会兜住）。</summary>
    static Int64[] Counts()
    {
        var counts = new[]
        {
            DesignProject.FindCount(), DesignTheme.FindCount(), DesignToken.FindCount(), DesignShadowLayer.FindCount(),
            DesignComponent.FindCount(), DesignComponentVariant.FindCount(), DesignIcon.FindCount(), DesignAsset.FindCount(),
            DesignScreen.FindCount(), DesignFontFace.FindCount(), DesignAudit.FindCount(), DesignRelease.FindCount(),
            DesignGuideline.FindCount(),
        };
        // 加了表却没加进行数快照 = "零写库"这条断言悄悄少盯一张表（M3 新增 DesignGuideline 时就是这里）
        counts.Length.Should().Be(DesignSystemTables.EntityTypes.Length, "行数快照必须覆盖全部实体表");
        return counts;
    }

    // ---------- AC1 ----------

    [Fact]
    public void AC1_全预设_明暗_非空且含surfaceBg_且零写库()
    {
        // M3 偏差登记：M2 建这条时目录 8 个并写死 8；目录扩到 13 后循环本来就遍历 All，写死的数字只会遮住"新预设没被扫到"
        StylePresets.All.Count.Should().Be(13);
        var before = Counts();

        foreach (var preset in StylePresets.All)
            foreach (var theme in new[] { "light", "dark" })
            {
                var r = _preview.Preview(preset.Request, theme);
                r.Theme.Should().Be(theme);
                r.Css.Should().NotBeNullOrWhiteSpace();
                r.Css.Should().Contain(":root");
                r.Css.Should().Contain("--ds-semantic-surface-bg", $"{preset.Id}/{theme} 必须给出画布底色变量（展厅据此认账）");
            }

        Counts().Should().Equal(before);   // 零写库：全部表一行没动
    }

    // ---------- AC2 ----------

    [Fact]
    public void AC2_同源_落库导出_等于_内存预览_全预设_明暗()
    {
        var i = 0;
        foreach (var preset in StylePresets.All)
            foreach (var theme in new[] { "light", "dark" })
            {
                var project = _projects.Create(new ProjectInput { Code = $"src-{i++}", Name = "同源校验" });
                // 用同一份请求落库（副本，避免污染预设目录）
                _generation.Run(project.Id, PresetRecommender.Copy(preset.Request), false);

                var exported = _export.Produce(project.Id, ExportFormats.Css, theme).Text;
                var preview = _preview.Preview(preset.Request, theme).Css;

                Normalize(preview).Should().Be(Normalize(exported), $"{preset.Id}/{theme}：内存预览必须与落库导出一致");
            }
    }

    // ---------- AC3 ----------

    [Fact]
    public void AC3_确定性_同请求两次_CSS相同()
    {
        var req = StylePresets.Find("admin-calm")!.Request;
        _preview.Preview(req, "dark").Css.Should().Be(_preview.Preview(req, "dark").Css);
    }

    [Fact]
    public void AC3_theme缺省_light()
    {
        var r = _preview.Preview(StylePresets.Find("admin-calm")!.Request, null);
        r.Theme.Should().Be("light");
        r.Css.Should().Contain("--ds-semantic-surface-bg");
    }

    [Fact]
    public void AC3_theme缺省_请求本身不被改写()
    {
        // Preview 必须克隆请求：直接改 req.Themes 会污染共享的预设目录（StylePresets.All 是单例）
        var req = PresetRecommender.Copy(StylePresets.Find("workbench-focus")!.Request);
        var before = String.Join(",", req.Themes);
        _preview.Preview(req, "light");
        String.Join(",", req.Themes).Should().Be(before);
    }

    [Fact]
    public void AC3_未知theme_回落浅色角色_不抛()
    {
        var r = _preview.Preview(new GenerationRequest(), "不存在的主题");
        r.Theme.Should().Be("不存在的主题");          // 原样回显，由前端按可用主题回落
        r.Css.Should().NotBeNullOrWhiteSpace();       // 未知主题按浅色角色解析（IsDark=false），不出错
    }

    [Fact]
    public void AC3_未知industry_回落general_不抛()
    {
        var r = _preview.Preview(new GenerationRequest { Industry = "不存在的行业" }, "light");
        r.Industry.Should().Be("general");
        r.Css.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AC3_null请求_等价空请求()
    {
        _preview.Preview(null, "light").Css.Should().Be(_preview.Preview(new GenerationRequest(), "light").Css);
    }

    // ---------- AC3「非法参数 400」（D1 处置：入口显式拒绝） ----------
    // 生成器本身对任意数值输入不抛（未知 industry 回落 general、未知 theme 走浅色），
    // 但 spec 要求非法参数显式 400。故预览服务在入口校验数值域，抛 ArgumentException，
    // 控制器 Guard 统一映射 400 —— 这层校验刻意**只属于 preview-css**（既有 generate/preview 不动）。

    [Theory]
    [InlineData(nameof(GenerationRequest.Hue), -1.0)]
    [InlineData(nameof(GenerationRequest.Hue), 360.0)]
    [InlineData(nameof(GenerationRequest.Hue), 999.0)]
    [InlineData(nameof(GenerationRequest.Chroma), 0.0)]
    [InlineData(nameof(GenerationRequest.Chroma), -0.1)]
    [InlineData(nameof(GenerationRequest.TypeRatio), 0.0)]
    [InlineData(nameof(GenerationRequest.TypeRatio), -1.0)]
    [InlineData(nameof(GenerationRequest.TypeBasePx), 0.0)]
    [InlineData(nameof(GenerationRequest.TypeBasePx), -1.0)]
    [InlineData(nameof(GenerationRequest.RadiusBase), -1.0)]
    [InlineData(nameof(GenerationRequest.MotionScale), 0.0)]
    [InlineData(nameof(GenerationRequest.MotionScale), -0.5)]
    public void AC3_非法数值参数_入口拒绝_抛ArgumentException映射400(String field, Double value)
    {
        var req = new GenerationRequest();
        typeof(GenerationRequest).GetProperty(field)!.SetValue(req, value);
        var act = () => _preview.Preview(req, "light");
        act.Should().Throw<ArgumentException>($"字段 {field}={value} 是非法参数，preview-css 必须 400（AC3）");
    }

    [Fact]
    public void AC3_合法边界值_不误伤()
    {
        var req = new GenerationRequest { Hue = 0, Chroma = 0.05, TypeRatio = 0.5, TypeBasePx = 1, RadiusBase = 0, MotionScale = 0.1 };
        _preview.Preview(req, "light").Css.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 03 §A3：`shadowStrength` 是各层 alpha 的乘数，**0 = 阴影不可见但令牌仍在**。
    /// 老投影把 `alpha=0` 判成"不用 color-mix"→ 直接输出实心色，于是"强度调到 0"反而拿到一条最重的黑投影
    /// （M3 视觉矩阵 V3 的 `m3v3-shadowStrength-0` 实测：computed box-shadow = `rgb(15, 23, 42) 0px 2px 9.1px -1px`）。
    /// 两端（默认 1 / 拉满 2）同时核，防止这条判据只在 0 那一侧成立、公式其实整条没接上。
    /// </summary>
    [Fact]
    public void AC3_阴影强度0_投影必须是transparent_而不是实心色()
    {
        var zero = ShadowLine(_preview.Preview(new GenerationRequest { ShadowStrength = 0 }, "light").Css);
        zero.Should().Contain("transparent", "alpha=0 的层必须投影成 transparent（不可见）");
        zero.Should().NotContain("color-mix", "0% 的 color-mix 是废话，直接给 transparent 才不依赖消费端算色");

        var one = ShadowLine(_preview.Preview(new GenerationRequest(), "light").Css);
        var strong = ShadowLine(_preview.Preview(new GenerationRequest { ShadowStrength = 2 }, "light").Css);
        one.Should().Contain("color-mix(", "默认强度的阴影仍是半透明（这条判据不许把正常路径也改成 transparent）");
        MixPercent(strong).Should().BeGreaterThan(MixPercent(one), "强度越大 alpha 越高——同一条公式的两端都要跟着动");
    }

    static String ShadowLine(String css)
    {
        var m = Regex.Match(css, @"--ds-shadow-elevation-3:\s*([^;]+);");
        m.Success.Should().BeTrue("预览 CSS 里必须有 elevation-3（拿不到 = 上面的断言全在空转）");
        return m.Groups[1].Value;
    }

    static Double MixPercent(String shadowCss)
    {
        var m = Regex.Match(shadowCss, @"color-mix\(in oklab,[^)]*?([\d.]+)%");
        m.Success.Should().BeTrue();
        return Double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>去掉 `/* … */` 注释并规整空白（Output 契约的"同形"判据；注释里含项目 code/version，天然会不同）。</summary>
    static String Normalize(String css)
    {
        var stripped = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var lines = stripped.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
        return String.Join("\n", lines);
    }
}